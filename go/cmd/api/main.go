// HTTP API server for Mechanical Sympathy demonstration.
//
// This API demonstrates the Single Writer Principle by using a channel-based
// order matching agent that processes all orders on a single goroutine.
//
// Uses Go 1.23+ enhanced routing with net/http standard library only.
package main

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"os"
	"os/signal"
	"runtime"
	"strconv"
	"strings"
	"sync/atomic"
	"syscall"
	"time"

	"github.com/psavelis/mechanical-sympathy-poc/go/internal/core/agents"
	"github.com/psavelis/mechanical-sympathy-poc/go/internal/domain"
)

// AppState holds the application state
type AppState struct {
	orderHandle *agents.AgentHandle[agents.OrderCommand]
	tradeOutput chan *domain.Trade
	startTime   time.Time

	// Atomic counters for thread-safe reads
	ordersReceived int64
	tradesExecuted int64
}

func main() {
	port := getEnv("PORT", "8080")

	// Create trade output channel
	tradeOutput := make(chan *domain.Trade, 10000)

	// Create order matching agent (Single Writer Principle)
	matchingAgent, orderHandle := agents.NewOrderMatchingAgent(8192, tradeOutput)

	state := &AppState{
		orderHandle: orderHandle,
		tradeOutput: tradeOutput,
		startTime:   time.Now(),
	}

	// Start the matching agent
	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	go matchingAgent.Run(ctx)

	// Start trade consumer (just counts trades for demo)
	go func() {
		for trade := range tradeOutput {
			atomic.AddInt64(&state.tradesExecuted, 1)
			_ = trade // In production, would persist or forward
		}
	}()

	// Setup HTTP routes
	mux := http.NewServeMux()

	// Health endpoints
	mux.HandleFunc("GET /health", healthHandler)
	mux.HandleFunc("GET /health/ready", readinessHandler(state))
	mux.HandleFunc("GET /health/live", livenessHandler)

	// Order endpoints
	mux.HandleFunc("POST /api/orders", placeOrderHandler(state))
	mux.HandleFunc("GET /api/orders/stats", statsHandler(state))

	// Order book endpoint
	mux.HandleFunc("GET /api/orderbook/{instrumentId}", orderBookHandler(state))

	// System info endpoint
	mux.HandleFunc("GET /api/system", systemInfoHandler(state))

	// Create server
	server := &http.Server{
		Addr:         ":" + port,
		Handler:      loggingMiddleware(mux),
		ReadTimeout:  10 * time.Second,
		WriteTimeout: 10 * time.Second,
		IdleTimeout:  60 * time.Second,
	}

	// Graceful shutdown
	go func() {
		sigChan := make(chan os.Signal, 1)
		signal.Notify(sigChan, syscall.SIGINT, syscall.SIGTERM)
		<-sigChan

		log.Println("Shutting down server...")
		cancel()
		close(tradeOutput)

		shutdownCtx, shutdownCancel := context.WithTimeout(context.Background(), 10*time.Second)
		defer shutdownCancel()

		if err := server.Shutdown(shutdownCtx); err != nil {
			log.Printf("Server shutdown error: %v", err)
		}
	}()

	log.Printf("Starting Mechanical Sympathy API server on :%s", port)
	log.Printf("Using Single Writer Principle for order matching")

	if err := server.ListenAndServe(); err != http.ErrServerClosed {
		log.Fatalf("Server error: %v", err)
	}

	log.Println("Server stopped")
}

// loggingMiddleware logs all requests
func loggingMiddleware(next http.Handler) http.Handler {
	return http.HandlerFunc(func(w http.ResponseWriter, r *http.Request) {
		start := time.Now()
		next.ServeHTTP(w, r)
		log.Printf("%s %s %s", r.Method, r.URL.Path, time.Since(start))
	})
}

// healthHandler returns basic health status
func healthHandler(w http.ResponseWriter, r *http.Request) {
	writeJSON(w, http.StatusOK, map[string]string{
		"status": "healthy",
	})
}

// readinessHandler checks if the service is ready to accept traffic
func readinessHandler(state *AppState) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		// Check if the order queue is not overwhelmed
		pending := state.orderHandle.Pending()
		isReady := pending < 5000

		status := http.StatusOK
		message := "Ready to accept orders"

		if !isReady {
			status = http.StatusServiceUnavailable
			message = fmt.Sprintf("Order queue overwhelmed: %d pending", pending)
		}

		writeJSON(w, status, map[string]interface{}{
			"ready":   isReady,
			"message": message,
			"pending": pending,
		})
	}
}

// livenessHandler returns if the service is alive
func livenessHandler(w http.ResponseWriter, r *http.Request) {
	writeJSON(w, http.StatusOK, map[string]string{
		"status": "alive",
	})
}

// PlaceOrderRequest is the request body for placing an order
type PlaceOrderRequest struct {
	InstrumentID  int64   `json:"instrumentId"`
	Side          string  `json:"side"` // "buy" or "sell"
	Type          string  `json:"type"` // "limit" or "market"
	Price         float64 `json:"price"`
	Quantity      float64 `json:"quantity"`
	ClientID      int64   `json:"clientId,omitempty"`
	ClientOrderID string  `json:"clientOrderId,omitempty"`
}

// PlaceOrderResponse is the response for a placed order
type PlaceOrderResponse struct {
	OrderID int64  `json:"orderId"`
	Status  string `json:"status"`
}

// placeOrderHandler handles order placement requests
func placeOrderHandler(state *AppState) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		var req PlaceOrderRequest
		if err := json.NewDecoder(r.Body).Decode(&req); err != nil {
			writeJSON(w, http.StatusBadRequest, map[string]string{
				"error": "Invalid request body: " + err.Error(),
			})
			return
		}

		// Validate request
		if req.Quantity <= 0 {
			writeJSON(w, http.StatusBadRequest, map[string]string{
				"error": "Quantity must be positive",
			})
			return
		}

		// Parse side
		var side domain.Side
		switch strings.ToLower(req.Side) {
		case "buy":
			side = domain.Buy
		case "sell":
			side = domain.Sell
		default:
			writeJSON(w, http.StatusBadRequest, map[string]string{
				"error": "Side must be 'buy' or 'sell'",
			})
			return
		}

		// Parse order type
		var orderType domain.OrderType
		switch strings.ToLower(req.Type) {
		case "limit", "":
			orderType = domain.Limit
			if req.Price <= 0 {
				writeJSON(w, http.StatusBadRequest, map[string]string{
					"error": "Price must be positive for limit orders",
				})
				return
			}
		case "market":
			orderType = domain.Market
		default:
			writeJSON(w, http.StatusBadRequest, map[string]string{
				"error": "Type must be 'limit' or 'market'",
			})
			return
		}

		// Create order
		order := domain.NewOrder(
			req.InstrumentID,
			side,
			orderType,
			req.Price,
			req.Quantity,
			req.ClientID,
		)

		if req.ClientOrderID != "" {
			order.ClientOrderID = req.ClientOrderID
		}

		// Send to matching agent (Single Writer Principle)
		ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
		defer cancel()

		if err := state.orderHandle.SendContext(ctx, agents.PlaceOrderCommand{Order: order}); err != nil {
			writeJSON(w, http.StatusServiceUnavailable, map[string]string{
				"error": "Failed to submit order: " + err.Error(),
			})
			return
		}

		atomic.AddInt64(&state.ordersReceived, 1)

		writeJSON(w, http.StatusAccepted, PlaceOrderResponse{
			OrderID: order.ID,
			Status:  "accepted",
		})
	}
}

// StatsResponse contains trading statistics
type StatsResponse struct {
	OrdersReceived int64  `json:"ordersReceived"`
	TradesExecuted int64  `json:"tradesExecuted"`
	PendingOrders  int    `json:"pendingOrders"`
	Uptime         string `json:"uptime"`
}

// statsHandler returns trading statistics
func statsHandler(state *AppState) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		writeJSON(w, http.StatusOK, StatsResponse{
			OrdersReceived: atomic.LoadInt64(&state.ordersReceived),
			TradesExecuted: atomic.LoadInt64(&state.tradesExecuted),
			PendingOrders:  state.orderHandle.Pending(),
			Uptime:         time.Since(state.startTime).String(),
		})
	}
}

// orderBookHandler returns an order book snapshot
func orderBookHandler(state *AppState) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		instrumentIDStr := r.PathValue("instrumentId")
		instrumentID, err := strconv.ParseInt(instrumentIDStr, 10, 64)
		if err != nil {
			writeJSON(w, http.StatusBadRequest, map[string]string{
				"error": "Invalid instrument ID",
			})
			return
		}

		// Create reply channel
		reply := make(chan *domain.OrderBookSnapshot, 1)

		// Send snapshot request to agent
		ctx, cancel := context.WithTimeout(r.Context(), 5*time.Second)
		defer cancel()

		if err := state.orderHandle.SendContext(ctx, agents.GetSnapshotCommand{
			InstrumentID: instrumentID,
			Reply:        reply,
		}); err != nil {
			writeJSON(w, http.StatusServiceUnavailable, map[string]string{
				"error": "Failed to get order book: " + err.Error(),
			})
			return
		}

		// Wait for response
		select {
		case snapshot := <-reply:
			if snapshot == nil {
				writeJSON(w, http.StatusNotFound, map[string]string{
					"error": fmt.Sprintf("Order book not found for instrument %d", instrumentID),
				})
				return
			}
			writeJSON(w, http.StatusOK, snapshot)
		case <-ctx.Done():
			writeJSON(w, http.StatusGatewayTimeout, map[string]string{
				"error": "Request timeout",
			})
		}
	}
}

// SystemInfoResponse contains system information
type SystemInfoResponse struct {
	ProcessorCount int    `json:"processorCount"`
	GoMaxProcs     int    `json:"goMaxProcs"`
	NumGoroutine   int    `json:"numGoroutine"`
	GoVersion      string `json:"goVersion"`
	OS             string `json:"os"`
	Arch           string `json:"arch"`
	HeapAllocMB    uint64 `json:"heapAllocMB"`
	HeapSysMB      uint64 `json:"heapSysMB"`
	NumGC          uint32 `json:"numGC"`
	CacheLineSize  int    `json:"cacheLineSize"`
	MechanicalSymp string `json:"mechanicalSympathy"`
}

// systemInfoHandler returns system information
func systemInfoHandler(state *AppState) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		var memStats runtime.MemStats
		runtime.ReadMemStats(&memStats)

		writeJSON(w, http.StatusOK, SystemInfoResponse{
			ProcessorCount: runtime.NumCPU(),
			GoMaxProcs:     runtime.GOMAXPROCS(0),
			NumGoroutine:   runtime.NumGoroutine(),
			GoVersion:      runtime.Version(),
			OS:             runtime.GOOS,
			Arch:           runtime.GOARCH,
			HeapAllocMB:    memStats.HeapAlloc / 1024 / 1024,
			HeapSysMB:      memStats.HeapSys / 1024 / 1024,
			NumGC:          memStats.NumGC,
			CacheLineSize:  64,
			MechanicalSymp: "Single Writer Principle via Go channels",
		})
	}
}

// writeJSON writes a JSON response
func writeJSON(w http.ResponseWriter, status int, data interface{}) {
	w.Header().Set("Content-Type", "application/json")
	w.WriteHeader(status)
	if err := json.NewEncoder(w).Encode(data); err != nil {
		log.Printf("Error encoding JSON response: %v", err)
	}
}

// getEnv returns an environment variable or a default value
func getEnv(key, fallback string) string {
	if value := os.Getenv(key); value != "" {
		return value
	}
	return fallback
}
