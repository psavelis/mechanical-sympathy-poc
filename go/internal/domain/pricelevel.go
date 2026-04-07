package domain

// PriceLevel represents a collection of orders at a specific price point.
// Orders are stored in a slice for sequential iteration (cache-friendly).
type PriceLevel struct {
	Price  float64
	Orders []*Order // FIFO order - sequential iteration for matching
}

// NewPriceLevel creates a new price level with pre-allocated capacity
func NewPriceLevel(price float64) *PriceLevel {
	return &PriceLevel{
		Price:  price,
		Orders: make([]*Order, 0, 16), // Pre-allocate for performance
	}
}

// NewPriceLevelWithCapacity creates a price level with custom capacity
func NewPriceLevelWithCapacity(price float64, capacity int) *PriceLevel {
	return &PriceLevel{
		Price:  price,
		Orders: make([]*Order, 0, capacity),
	}
}

// AddOrder adds an order to the price level (maintains FIFO order)
func (p *PriceLevel) AddOrder(order *Order) {
	p.Orders = append(p.Orders, order)
}

// RemoveOrder removes an order from the price level by ID
// Returns the removed order or nil if not found
func (p *PriceLevel) RemoveOrder(orderID int64) *Order {
	for i, order := range p.Orders {
		if order.ID == orderID {
			// Remove by swapping with last element and truncating
			removed := p.Orders[i]
			p.Orders = append(p.Orders[:i], p.Orders[i+1:]...)
			return removed
		}
	}
	return nil
}

// RemoveFirst removes and returns the first order (for FIFO matching)
func (p *PriceLevel) RemoveFirst() *Order {
	if len(p.Orders) == 0 {
		return nil
	}
	first := p.Orders[0]
	p.Orders = p.Orders[1:]
	return first
}

// TotalQuantity returns the sum of all order quantities at this level
// Uses sequential iteration for cache efficiency
func (p *PriceLevel) TotalQuantity() float64 {
	var sum float64
	for i := 0; i < len(p.Orders); i++ {
		sum += p.Orders[i].Quantity
	}
	return sum
}

// OrderCount returns the number of orders at this price level
func (p *PriceLevel) OrderCount() int {
	return len(p.Orders)
}

// IsEmpty returns true if there are no orders at this price level
func (p *PriceLevel) IsEmpty() bool {
	return len(p.Orders) == 0
}

// Clear removes all orders from the price level
func (p *PriceLevel) Clear() {
	p.Orders = p.Orders[:0]
}

// FirstOrder returns the first order without removing it
func (p *PriceLevel) FirstOrder() *Order {
	if len(p.Orders) == 0 {
		return nil
	}
	return p.Orders[0]
}
