package tests

import (
	"testing"

	"github.com/psavelis/mechanical-sympathy-poc/go/internal/domain"
)

func TestOrderCreation(t *testing.T) {
	domain.ResetOrderIDCounter()

	order := domain.NewOrder(1, domain.Buy, domain.Limit, 100.0, 50.0, 123)

	if order.ID != 1 {
		t.Errorf("Expected order ID 1, got %d", order.ID)
	}
	if order.InstrumentID != 1 {
		t.Errorf("Expected instrument ID 1, got %d", order.InstrumentID)
	}
	if order.Side != domain.Buy {
		t.Errorf("Expected Buy side, got %v", order.Side)
	}
	if order.Type != domain.Limit {
		t.Errorf("Expected Limit type, got %v", order.Type)
	}
	if order.Price != 100.0 {
		t.Errorf("Expected price 100.0, got %f", order.Price)
	}
	if order.Quantity != 50.0 {
		t.Errorf("Expected quantity 50.0, got %f", order.Quantity)
	}
	if order.OriginalQuantity != 50.0 {
		t.Errorf("Expected original quantity 50.0, got %f", order.OriginalQuantity)
	}
	if order.Status != domain.New {
		t.Errorf("Expected New status, got %v", order.Status)
	}
}

func TestOrderReduceQuantity(t *testing.T) {
	order := domain.NewOrderWithID(1, 1, domain.Buy, domain.Limit, 100.0, 50.0, 123)

	// Partial fill
	order.ReduceQuantity(20.0)
	if order.Quantity != 30.0 {
		t.Errorf("Expected quantity 30.0, got %f", order.Quantity)
	}
	if order.Status != domain.PartiallyFilled {
		t.Errorf("Expected PartiallyFilled status, got %v", order.Status)
	}

	// Complete fill
	order.ReduceQuantity(30.0)
	if order.Quantity != 0.0 {
		t.Errorf("Expected quantity 0.0, got %f", order.Quantity)
	}
	if order.Status != domain.Filled {
		t.Errorf("Expected Filled status, got %v", order.Status)
	}
}

func TestOrderIsFilled(t *testing.T) {
	order := domain.NewOrderWithID(1, 1, domain.Buy, domain.Limit, 100.0, 50.0, 123)

	if order.IsFilled() {
		t.Error("New order should not be filled")
	}

	order.ReduceQuantity(50.0)

	if !order.IsFilled() {
		t.Error("Order with 0 quantity should be filled")
	}
}

func TestTradeCreation(t *testing.T) {
	domain.ResetTradeIDCounter()

	trade := domain.NewTrade(1, 100, 200, 99.50, 25.0)

	if trade.ID != 1 {
		t.Errorf("Expected trade ID 1, got %d", trade.ID)
	}
	if trade.InstrumentID != 1 {
		t.Errorf("Expected instrument ID 1, got %d", trade.InstrumentID)
	}
	if trade.BuyOrderID != 100 {
		t.Errorf("Expected buy order ID 100, got %d", trade.BuyOrderID)
	}
	if trade.SellOrderID != 200 {
		t.Errorf("Expected sell order ID 200, got %d", trade.SellOrderID)
	}
	if trade.Price != 99.50 {
		t.Errorf("Expected price 99.50, got %f", trade.Price)
	}
	if trade.Quantity != 25.0 {
		t.Errorf("Expected quantity 25.0, got %f", trade.Quantity)
	}
}

func TestPriceLevel(t *testing.T) {
	level := domain.NewPriceLevel(100.0)

	if level.Price != 100.0 {
		t.Errorf("Expected price 100.0, got %f", level.Price)
	}
	if !level.IsEmpty() {
		t.Error("New price level should be empty")
	}

	// Add orders
	order1 := domain.NewOrderWithID(1, 1, domain.Buy, domain.Limit, 100.0, 50.0, 1)
	order2 := domain.NewOrderWithID(2, 1, domain.Buy, domain.Limit, 100.0, 30.0, 2)

	level.AddOrder(order1)
	level.AddOrder(order2)

	if level.OrderCount() != 2 {
		t.Errorf("Expected 2 orders, got %d", level.OrderCount())
	}
	if level.TotalQuantity() != 80.0 {
		t.Errorf("Expected total quantity 80.0, got %f", level.TotalQuantity())
	}

	// Remove first
	removed := level.RemoveFirst()
	if removed.ID != 1 {
		t.Errorf("Expected removed order ID 1, got %d", removed.ID)
	}
	if level.OrderCount() != 1 {
		t.Errorf("Expected 1 order remaining, got %d", level.OrderCount())
	}
}

func TestOrderBookBasic(t *testing.T) {
	ob := domain.NewOrderBook(1)

	if ob.InstrumentID != 1 {
		t.Errorf("Expected instrument ID 1, got %d", ob.InstrumentID)
	}

	// Add buy orders
	buyOrder1 := domain.NewOrderWithID(1, 1, domain.Buy, domain.Limit, 100.0, 50.0, 1)
	buyOrder2 := domain.NewOrderWithID(2, 1, domain.Buy, domain.Limit, 99.0, 30.0, 2)
	ob.AddOrder(buyOrder1)
	ob.AddOrder(buyOrder2)

	// Add sell orders
	sellOrder1 := domain.NewOrderWithID(3, 1, domain.Sell, domain.Limit, 101.0, 40.0, 3)
	ob.AddOrder(sellOrder1)

	// Check best bid/ask
	bestBid, hasBid := ob.BestBid()
	if !hasBid || bestBid != 100.0 {
		t.Errorf("Expected best bid 100.0, got %f", bestBid)
	}

	bestAsk, hasAsk := ob.BestAsk()
	if !hasAsk || bestAsk != 101.0 {
		t.Errorf("Expected best ask 101.0, got %f", bestAsk)
	}

	// Check spread
	spread, hasSpread := ob.Spread()
	if !hasSpread || spread != 1.0 {
		t.Errorf("Expected spread 1.0, got %f", spread)
	}

	// Check totals
	if ob.TotalBidOrders() != 2 {
		t.Errorf("Expected 2 bid orders, got %d", ob.TotalBidOrders())
	}
	if ob.TotalAskOrders() != 1 {
		t.Errorf("Expected 1 ask order, got %d", ob.TotalAskOrders())
	}
}

func TestOrderBookMatching(t *testing.T) {
	domain.ResetOrderIDCounter()
	domain.ResetTradeIDCounter()

	ob := domain.NewOrderBook(1)

	// Add resting sell order at 100
	sellOrder := domain.NewOrderWithID(1, 1, domain.Sell, domain.Limit, 100.0, 50.0, 1)
	ob.AddOrder(sellOrder)

	// Incoming buy order at 100 (should match)
	buyOrder := domain.NewOrderWithID(2, 1, domain.Buy, domain.Limit, 100.0, 30.0, 2)
	trades, remaining := ob.MatchOrder(buyOrder)

	if len(trades) != 1 {
		t.Fatalf("Expected 1 trade, got %d", len(trades))
	}

	trade := trades[0]
	if trade.Quantity != 30.0 {
		t.Errorf("Expected trade quantity 30.0, got %f", trade.Quantity)
	}
	if trade.Price != 100.0 {
		t.Errorf("Expected trade price 100.0, got %f", trade.Price)
	}
	if trade.BuyOrderID != 2 {
		t.Errorf("Expected buy order ID 2, got %d", trade.BuyOrderID)
	}
	if trade.SellOrderID != 1 {
		t.Errorf("Expected sell order ID 1, got %d", trade.SellOrderID)
	}

	// Buy order should be fully filled
	if remaining != nil {
		t.Error("Buy order should be fully filled")
	}

	// Sell order should have remaining quantity
	if sellOrder.Quantity != 20.0 {
		t.Errorf("Expected sell order remaining quantity 20.0, got %f", sellOrder.Quantity)
	}
}

func TestOrderBookNoMatch(t *testing.T) {
	ob := domain.NewOrderBook(1)

	// Add sell order at 101
	sellOrder := domain.NewOrderWithID(1, 1, domain.Sell, domain.Limit, 101.0, 50.0, 1)
	ob.AddOrder(sellOrder)

	// Buy order at 100 (should not match)
	buyOrder := domain.NewOrderWithID(2, 1, domain.Buy, domain.Limit, 100.0, 30.0, 2)
	trades, remaining := ob.MatchOrder(buyOrder)

	if len(trades) != 0 {
		t.Errorf("Expected 0 trades, got %d", len(trades))
	}

	if remaining == nil {
		t.Error("Buy order should have remaining quantity")
	}
	if remaining.Quantity != 30.0 {
		t.Errorf("Expected remaining quantity 30.0, got %f", remaining.Quantity)
	}
}

func TestSideString(t *testing.T) {
	if domain.Buy.String() != "Buy" {
		t.Errorf("Expected 'Buy', got '%s'", domain.Buy.String())
	}
	if domain.Sell.String() != "Sell" {
		t.Errorf("Expected 'Sell', got '%s'", domain.Sell.String())
	}
}

func TestOrderTypeString(t *testing.T) {
	if domain.Limit.String() != "Limit" {
		t.Errorf("Expected 'Limit', got '%s'", domain.Limit.String())
	}
	if domain.Market.String() != "Market" {
		t.Errorf("Expected 'Market', got '%s'", domain.Market.String())
	}
}

func TestOrderStatusString(t *testing.T) {
	statuses := []struct {
		status   domain.OrderStatus
		expected string
	}{
		{domain.New, "New"},
		{domain.PartiallyFilled, "PartiallyFilled"},
		{domain.Filled, "Filled"},
		{domain.Cancelled, "Cancelled"},
		{domain.Rejected, "Rejected"},
	}

	for _, s := range statuses {
		if s.status.String() != s.expected {
			t.Errorf("Expected '%s', got '%s'", s.expected, s.status.String())
		}
	}
}
