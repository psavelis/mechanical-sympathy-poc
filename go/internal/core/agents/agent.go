// Package agents implements the Single Writer Principle using Go channels.
//
// The Single Writer Principle ensures that all writes to shared state occur
// through a dedicated single goroutine (the "agent"). Other goroutines
// communicate with the agent by sending messages through a channel.
//
// Benefits:
// - Eliminates mutex contention and lock-based coordination
// - Provides predictable latency (no lock waiting spikes)
// - Enables natural batching of messages
// - Simplifies reasoning about concurrent state
//
// Reference: https://martinfowler.com/articles/mechanical-sympathy-principles.html
package agents

import (
	"context"
	"sync"
	"sync/atomic"
	"time"

	"github.com/psavelis/mechanical-sympathy-poc/go/internal/domain"
)

// AgentHandle provides a thread-safe way to send messages to an agent.
// Multiple goroutines can safely use the same handle to send messages.
type AgentHandle[T any] struct {
	inbox chan T
}

// Send sends a message to the agent (blocking if channel is full)
func (h *AgentHandle[T]) Send(msg T) {
	h.inbox <- msg
}

// SendContext sends a message with context cancellation support
func (h *AgentHandle[T]) SendContext(ctx context.Context, msg T) error {
	select {
	case h.inbox <- msg:
		return nil
	case <-ctx.Done():
		return ctx.Err()
	}
}

// TrySend attempts to send a message without blocking
// Returns true if successful, false if the channel is full
func (h *AgentHandle[T]) TrySend(msg T) bool {
	select {
	case h.inbox <- msg:
		return true
	default:
		return false
	}
}

// Close closes the agent's inbox channel
func (h *AgentHandle[T]) Close() {
	close(h.inbox)
}

// Pending returns the number of messages waiting in the inbox
func (h *AgentHandle[T]) Pending() int {
	return len(h.inbox)
}

// StatsMessage represents a message sent to the StatsAgent
type StatsMessage struct {
	Value int64
}

// StatsAgent collects statistics using the Single Writer Principle.
// All state mutations happen on a single goroutine - no locks needed!
type StatsAgent struct {
	inbox     chan StatsMessage
	total     int64
	count     int64
	min       int64
	max       int64
	sum       float64
	sumSq     float64
	processed int64
}

// StatsSnapshot represents a point-in-time snapshot of statistics
type StatsSnapshot struct {
	Total     int64
	Count     int64
	Min       int64
	Max       int64
	Average   float64
	StdDev    float64
	Processed int64
}

// NewStatsAgent creates a new StatsAgent with the given channel capacity
func NewStatsAgent(capacity int) (*StatsAgent, *AgentHandle[StatsMessage]) {
	inbox := make(chan StatsMessage, capacity)
	agent := &StatsAgent{
		inbox: inbox,
		min:   1<<63 - 1, // Max int64
		max:   -1 << 63,  // Min int64
	}
	return agent, &AgentHandle[StatsMessage]{inbox: inbox}
}

// Run processes messages until the context is cancelled or channel is closed
func (a *StatsAgent) Run(ctx context.Context) {
	for {
		select {
		case <-ctx.Done():
			// Drain remaining messages before exiting
			a.drain()
			return
		case msg, ok := <-a.inbox:
			if !ok {
				return // Channel closed
			}
			a.processMessage(msg)
		}
	}
}

// drain processes any remaining messages in the inbox
func (a *StatsAgent) drain() {
	for {
		select {
		case msg, ok := <-a.inbox:
			if !ok {
				return
			}
			a.processMessage(msg)
		default:
			return
		}
	}
}

// processMessage handles a single message - NO LOCKS NEEDED!
// This is the key insight: all state mutations happen on one goroutine
func (a *StatsAgent) processMessage(msg StatsMessage) {
	a.total += msg.Value
	a.count++
	a.sum += float64(msg.Value)
	a.sumSq += float64(msg.Value) * float64(msg.Value)

	if msg.Value < a.min {
		a.min = msg.Value
	}
	if msg.Value > a.max {
		a.max = msg.Value
	}

	a.processed++
}

// Snapshot returns a point-in-time snapshot of the statistics
// Note: This should only be called after the agent has stopped
func (a *StatsAgent) Snapshot() StatsSnapshot {
	var avg, stdDev float64
	if a.count > 0 {
		avg = a.sum / float64(a.count)
		if a.count > 1 {
			variance := (a.sumSq - (a.sum * a.sum / float64(a.count))) / float64(a.count-1)
			if variance > 0 {
				stdDev = sqrt(variance)
			}
		}
	}

	return StatsSnapshot{
		Total:     a.total,
		Count:     a.count,
		Min:       a.min,
		Max:       a.max,
		Average:   avg,
		StdDev:    stdDev,
		Processed: a.processed,
	}
}

// Total returns the total of all values
func (a *StatsAgent) Total() int64 {
	return a.total
}

// Count returns the number of messages processed
func (a *StatsAgent) Count() int64 {
	return a.count
}

// sqrt computes the square root (simple Newton's method implementation)
func sqrt(x float64) float64 {
	if x <= 0 {
		return 0
	}
	z := x / 2
	for i := 0; i < 10; i++ {
		z = z - (z*z-x)/(2*z)
	}
	return z
}

// OrderCommand represents commands sent to the OrderMatchingAgent
type OrderCommand interface {
	isOrderCommand()
}

// PlaceOrderCommand requests placing a new order
type PlaceOrderCommand struct {
	Order *domain.Order
}

func (PlaceOrderCommand) isOrderCommand() {}

// CancelOrderCommand requests cancelling an order
type CancelOrderCommand struct {
	OrderID      int64
	InstrumentID int64
}

func (CancelOrderCommand) isOrderCommand() {}

// GetSnapshotCommand requests an order book snapshot
type GetSnapshotCommand struct {
	InstrumentID int64
	Reply        chan<- *domain.OrderBookSnapshot
}

func (GetSnapshotCommand) isOrderCommand() {}

// OrderMatchingAgent processes orders using the Single Writer Principle.
// All order book state is exclusively owned by this agent.
type OrderMatchingAgent struct {
	inbox       chan OrderCommand
	orderBooks  map[int64]*domain.OrderBook // Exclusively owned - no locks!
	tradeOutput chan *domain.Trade
	totalOrders int64
	totalTrades int64
}

// NewOrderMatchingAgent creates a new order matching agent
func NewOrderMatchingAgent(capacity int, tradeOutput chan *domain.Trade) (*OrderMatchingAgent, *AgentHandle[OrderCommand]) {
	inbox := make(chan OrderCommand, capacity)
	agent := &OrderMatchingAgent{
		inbox:       inbox,
		orderBooks:  make(map[int64]*domain.OrderBook),
		tradeOutput: tradeOutput,
	}
	return agent, &AgentHandle[OrderCommand]{inbox: inbox}
}

// Run processes commands until the context is cancelled or channel is closed
func (a *OrderMatchingAgent) Run(ctx context.Context) {
	for {
		select {
		case <-ctx.Done():
			return
		case cmd, ok := <-a.inbox:
			if !ok {
				return
			}
			a.processCommand(cmd)
		}
	}
}

// processCommand handles a single command - NO LOCKS NEEDED!
func (a *OrderMatchingAgent) processCommand(cmd OrderCommand) {
	switch c := cmd.(type) {
	case PlaceOrderCommand:
		a.processPlaceOrder(c.Order)
	case CancelOrderCommand:
		a.processCancelOrder(c.OrderID, c.InstrumentID)
	case GetSnapshotCommand:
		a.processGetSnapshot(c.InstrumentID, c.Reply)
	}
}

func (a *OrderMatchingAgent) processPlaceOrder(order *domain.Order) {
	orderBook := a.getOrCreateOrderBook(order.InstrumentID)

	// Match against existing orders
	trades, remaining := orderBook.MatchOrder(order)

	// Output trades
	for _, trade := range trades {
		select {
		case a.tradeOutput <- trade:
			a.totalTrades++
		default:
			// Trade output channel full - could log or handle differently
		}
	}

	// Add remaining quantity to book if limit order
	if remaining != nil && remaining.Type == domain.Limit {
		orderBook.AddOrder(remaining)
	}

	a.totalOrders++
}

func (a *OrderMatchingAgent) processCancelOrder(orderID, instrumentID int64) {
	orderBook, exists := a.orderBooks[instrumentID]
	if !exists {
		return
	}

	// Find and remove the order (would need to track orders by ID in production)
	// For demo purposes, we skip implementation
	_ = orderBook
	_ = orderID
}

func (a *OrderMatchingAgent) processGetSnapshot(instrumentID int64, reply chan<- *domain.OrderBookSnapshot) {
	orderBook, exists := a.orderBooks[instrumentID]
	if !exists {
		reply <- nil
		return
	}
	reply <- orderBook.Snapshot()
}

func (a *OrderMatchingAgent) getOrCreateOrderBook(instrumentID int64) *domain.OrderBook {
	orderBook, exists := a.orderBooks[instrumentID]
	if !exists {
		orderBook = domain.NewOrderBook(instrumentID)
		a.orderBooks[instrumentID] = orderBook
	}
	return orderBook
}

// TotalOrders returns the total number of orders processed
func (a *OrderMatchingAgent) TotalOrders() int64 {
	return a.totalOrders
}

// TotalTrades returns the total number of trades executed
func (a *OrderMatchingAgent) TotalTrades() int64 {
	return a.totalTrades
}

// Pending returns the number of commands waiting to be processed
func (a *OrderMatchingAgent) Pending() int {
	return len(a.inbox)
}

// SingleWriterDemoResult holds the results of a single writer demonstration
type SingleWriterDemoResult struct {
	MutexDuration   time.Duration
	ChannelDuration time.Duration
	Speedup         float64
	Messages        int
	Producers       int
}

// RunSingleWriterDemo demonstrates the performance difference between
// mutex-based and channel-based (single writer) approaches.
func RunSingleWriterDemo(messages, producers int) SingleWriterDemoResult {
	// Test mutex-based approach
	mutexDuration := runMutexTest(messages, producers)

	// Test channel-based (single writer) approach
	channelDuration := runChannelTest(messages, producers)

	speedup := float64(mutexDuration) / float64(channelDuration)

	return SingleWriterDemoResult{
		MutexDuration:   mutexDuration,
		ChannelDuration: channelDuration,
		Speedup:         speedup,
		Messages:        messages,
		Producers:       producers,
	}
}

func runMutexTest(messages, producers int) time.Duration {
	var mu sync.Mutex
	var counter int64
	perProducer := messages / producers

	start := time.Now()

	var wg sync.WaitGroup
	for p := 0; p < producers; p++ {
		wg.Add(1)
		go func() {
			defer wg.Done()
			for i := 0; i < perProducer; i++ {
				mu.Lock()
				counter++
				mu.Unlock()
			}
		}()
	}
	wg.Wait()

	return time.Since(start)
}

func runChannelTest(messages, producers int) time.Duration {
	inbox := make(chan int64, 4096)
	var counter int64
	perProducer := messages / producers

	start := time.Now()

	// Consumer (Single Writer)
	var consumerDone sync.WaitGroup
	consumerDone.Add(1)
	go func() {
		defer consumerDone.Done()
		for val := range inbox {
			counter += val // No lock needed - single writer!
		}
	}()

	// Producers
	var producerDone sync.WaitGroup
	for p := 0; p < producers; p++ {
		producerDone.Add(1)
		go func() {
			defer producerDone.Done()
			for i := 0; i < perProducer; i++ {
				inbox <- 1
			}
		}()
	}

	producerDone.Wait()
	close(inbox)
	consumerDone.Wait()

	return time.Since(start)
}

// CounterAgent is a simple counter using the Single Writer Principle
type CounterAgent struct {
	inbox   chan int64
	counter int64
}

// NewCounterAgent creates a new counter agent
func NewCounterAgent(capacity int) (*CounterAgent, *AgentHandle[int64]) {
	inbox := make(chan int64, capacity)
	agent := &CounterAgent{inbox: inbox}
	return agent, &AgentHandle[int64]{inbox: inbox}
}

// Run processes increment messages
func (a *CounterAgent) Run(ctx context.Context) {
	for {
		select {
		case <-ctx.Done():
			return
		case val, ok := <-a.inbox:
			if !ok {
				return
			}
			a.counter += val // Single writer - no lock!
		}
	}
}

// Counter returns the current counter value
func (a *CounterAgent) Counter() int64 {
	return atomic.LoadInt64(&a.counter)
}
