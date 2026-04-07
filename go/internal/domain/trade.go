package domain

import (
	"sync/atomic"
	"time"
)

var nextTradeID int64

// Trade represents an executed trade between two orders
type Trade struct {
	ID           int64
	InstrumentID int64
	BuyOrderID   int64
	SellOrderID  int64
	Price        float64
	Quantity     float64
	ExecutedAt   time.Time
}

// NewTrade creates a new trade with auto-generated ID
func NewTrade(instrumentID, buyOrderID, sellOrderID int64, price, quantity float64) *Trade {
	return &Trade{
		ID:           atomic.AddInt64(&nextTradeID, 1),
		InstrumentID: instrumentID,
		BuyOrderID:   buyOrderID,
		SellOrderID:  sellOrderID,
		Price:        price,
		Quantity:     quantity,
		ExecutedAt:   time.Now(),
	}
}

// NewTradeWithID creates a trade with a specific ID (for testing)
func NewTradeWithID(id, instrumentID, buyOrderID, sellOrderID int64, price, quantity float64) *Trade {
	return &Trade{
		ID:           id,
		InstrumentID: instrumentID,
		BuyOrderID:   buyOrderID,
		SellOrderID:  sellOrderID,
		Price:        price,
		Quantity:     quantity,
		ExecutedAt:   time.Now(),
	}
}

// NotionalValue returns the total value of the trade
func (t *Trade) NotionalValue() float64 {
	return t.Price * t.Quantity
}

// ResetTradeIDCounter resets the trade ID counter (for testing)
func ResetTradeIDCounter() {
	atomic.StoreInt64(&nextTradeID, 0)
}
