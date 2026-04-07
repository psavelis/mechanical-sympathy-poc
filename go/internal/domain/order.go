package domain

import (
	"sync/atomic"
	"time"
)

var nextOrderID int64

// Order represents a trading order with cache-friendly field layout.
// Hot path fields (accessed during matching) are placed first to fit in one cache line.
type Order struct {
	// HOT PATH - First cache line (64 bytes)
	// These fields are accessed on every matching operation
	ID               int64       // 8 bytes - unique order ID
	InstrumentID     int64       // 8 bytes - which market/symbol
	Price            float64     // 8 bytes - limit price
	Quantity         float64     // 8 bytes - remaining quantity
	OriginalQuantity float64     // 8 bytes - original quantity for fills
	Side             Side        // 1 byte - Buy or Sell
	Type             OrderType   // 1 byte - Limit or Market
	Status           OrderStatus // 1 byte - New, Filled, etc.
	_                [5]byte     // 5 bytes padding to align to 8 bytes

	// COLD PATH - Second cache line
	// These fields are accessed less frequently
	ClientID      int64
	ClientOrderID string
	CreatedAt     time.Time
	UpdatedAt     *time.Time
}

// NewOrder creates a new order with auto-generated ID
func NewOrder(instrumentID int64, side Side, orderType OrderType, price, quantity float64, clientID int64) *Order {
	return &Order{
		ID:               atomic.AddInt64(&nextOrderID, 1),
		InstrumentID:     instrumentID,
		Side:             side,
		Type:             orderType,
		Price:            price,
		Quantity:         quantity,
		OriginalQuantity: quantity,
		ClientID:         clientID,
		Status:           New,
		CreatedAt:        time.Now(),
	}
}

// NewOrderWithID creates an order with a specific ID (for testing)
func NewOrderWithID(id, instrumentID int64, side Side, orderType OrderType, price, quantity float64, clientID int64) *Order {
	return &Order{
		ID:               id,
		InstrumentID:     instrumentID,
		Side:             side,
		Type:             orderType,
		Price:            price,
		Quantity:         quantity,
		OriginalQuantity: quantity,
		ClientID:         clientID,
		Status:           New,
		CreatedAt:        time.Now(),
	}
}

// IsFilled returns true if the order has been completely filled
func (o *Order) IsFilled() bool {
	return o.Quantity <= 0 || o.Status == Filled
}

// ReduceQuantity reduces the order quantity by the given amount
// and updates the status accordingly
func (o *Order) ReduceQuantity(amount float64) {
	o.Quantity -= amount
	now := time.Now()
	o.UpdatedAt = &now
	if o.Quantity <= 0 {
		o.Quantity = 0
		o.Status = Filled
	} else {
		o.Status = PartiallyFilled
	}
}

// Cancel marks the order as cancelled
func (o *Order) Cancel() {
	o.Status = Cancelled
	now := time.Now()
	o.UpdatedAt = &now
}

// Reject marks the order as rejected
func (o *Order) Reject() {
	o.Status = Rejected
	now := time.Now()
	o.UpdatedAt = &now
}

// RemainingValue returns the remaining notional value of the order
func (o *Order) RemainingValue() float64 {
	return o.Price * o.Quantity
}

// FilledQuantity returns the quantity that has been filled
func (o *Order) FilledQuantity() float64 {
	return o.OriginalQuantity - o.Quantity
}

// ResetOrderIDCounter resets the order ID counter (for testing)
func ResetOrderIDCounter() {
	atomic.StoreInt64(&nextOrderID, 0)
}
