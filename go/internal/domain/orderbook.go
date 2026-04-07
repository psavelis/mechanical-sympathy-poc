package domain

import "sort"

// OrderBook represents a limit order book for a single instrument.
// Uses maps for O(1) price level lookup with sorted iteration when needed.
type OrderBook struct {
	InstrumentID int64
	Bids         map[float64]*PriceLevel // Buy orders (best = highest price)
	Asks         map[float64]*PriceLevel // Sell orders (best = lowest price)
}

// NewOrderBook creates a new order book for the given instrument
func NewOrderBook(instrumentID int64) *OrderBook {
	return &OrderBook{
		InstrumentID: instrumentID,
		Bids:         make(map[float64]*PriceLevel),
		Asks:         make(map[float64]*PriceLevel),
	}
}

// AddOrder adds an order to the appropriate side of the book
func (ob *OrderBook) AddOrder(order *Order) {
	book := ob.Bids
	if order.Side == Sell {
		book = ob.Asks
	}

	level, exists := book[order.Price]
	if !exists {
		level = NewPriceLevel(order.Price)
		book[order.Price] = level
	}
	level.AddOrder(order)
}

// RemoveOrder removes an order from the book
func (ob *OrderBook) RemoveOrder(order *Order) bool {
	book := ob.Bids
	if order.Side == Sell {
		book = ob.Asks
	}

	level, exists := book[order.Price]
	if !exists {
		return false
	}

	removed := level.RemoveOrder(order.ID)
	if removed == nil {
		return false
	}

	// Clean up empty price levels
	if level.IsEmpty() {
		delete(book, order.Price)
	}

	return true
}

// BestBid returns the highest bid price
func (ob *OrderBook) BestBid() (float64, bool) {
	var best float64
	found := false
	for price := range ob.Bids {
		if !found || price > best {
			best = price
			found = true
		}
	}
	return best, found
}

// BestAsk returns the lowest ask price
func (ob *OrderBook) BestAsk() (float64, bool) {
	var best float64
	found := false
	for price := range ob.Asks {
		if !found || price < best {
			best = price
			found = true
		}
	}
	return best, found
}

// Spread returns the bid-ask spread
func (ob *OrderBook) Spread() (float64, bool) {
	bestBid, hasBid := ob.BestBid()
	bestAsk, hasAsk := ob.BestAsk()
	if !hasBid || !hasAsk {
		return 0, false
	}
	return bestAsk - bestBid, true
}

// MidPrice returns the mid-price
func (ob *OrderBook) MidPrice() (float64, bool) {
	bestBid, hasBid := ob.BestBid()
	bestAsk, hasAsk := ob.BestAsk()
	if !hasBid || !hasAsk {
		return 0, false
	}
	return (bestBid + bestAsk) / 2, true
}

// GetOppositeSide returns the side opposite to the given order side
func (ob *OrderBook) GetOppositeSide(side Side) map[float64]*PriceLevel {
	if side == Buy {
		return ob.Asks
	}
	return ob.Bids
}

// GetSide returns the order book side for the given side
func (ob *OrderBook) GetSide(side Side) map[float64]*PriceLevel {
	if side == Buy {
		return ob.Bids
	}
	return ob.Asks
}

// TotalBidOrders returns the total number of bid orders
func (ob *OrderBook) TotalBidOrders() int {
	total := 0
	for _, level := range ob.Bids {
		total += level.OrderCount()
	}
	return total
}

// TotalAskOrders returns the total number of ask orders
func (ob *OrderBook) TotalAskOrders() int {
	total := 0
	for _, level := range ob.Asks {
		total += level.OrderCount()
	}
	return total
}

// TotalBidQuantity returns the total bid quantity
func (ob *OrderBook) TotalBidQuantity() float64 {
	var total float64
	for _, level := range ob.Bids {
		total += level.TotalQuantity()
	}
	return total
}

// TotalAskQuantity returns the total ask quantity
func (ob *OrderBook) TotalAskQuantity() float64 {
	var total float64
	for _, level := range ob.Asks {
		total += level.TotalQuantity()
	}
	return total
}

// SortedBidPrices returns bid prices sorted descending (best first)
func (ob *OrderBook) SortedBidPrices() []float64 {
	prices := make([]float64, 0, len(ob.Bids))
	for price := range ob.Bids {
		prices = append(prices, price)
	}
	sort.Sort(sort.Reverse(sort.Float64Slice(prices)))
	return prices
}

// SortedAskPrices returns ask prices sorted ascending (best first)
func (ob *OrderBook) SortedAskPrices() []float64 {
	prices := make([]float64, 0, len(ob.Asks))
	for price := range ob.Asks {
		prices = append(prices, price)
	}
	sort.Float64s(prices)
	return prices
}

// MatchOrder attempts to match an incoming order against the book
// Returns executed trades and the remaining order (nil if fully filled)
func (ob *OrderBook) MatchOrder(incoming *Order) ([]*Trade, *Order) {
	trades := make([]*Trade, 0)
	oppositeBook := ob.GetOppositeSide(incoming.Side)

	// Get matching prices based on order side
	var matchingPrices []float64
	if incoming.Side == Buy {
		// Buy order matches against asks <= order price
		matchingPrices = ob.SortedAskPrices()
	} else {
		// Sell order matches against bids >= order price
		matchingPrices = ob.SortedBidPrices()
	}

	for _, price := range matchingPrices {
		if incoming.IsFilled() {
			break
		}

		// Check if price is acceptable
		if incoming.Side == Buy && price > incoming.Price {
			break
		}
		if incoming.Side == Sell && price < incoming.Price {
			break
		}

		level, exists := oppositeBook[price]
		if !exists || level.IsEmpty() {
			continue
		}

		// Match against orders at this price level
		ordersToRemove := make([]int64, 0)

		for _, resting := range level.Orders {
			if incoming.IsFilled() {
				break
			}

			matchQty := min(incoming.Quantity, resting.Quantity)
			matchPrice := resting.Price // Price-time priority: use resting order price

			// Create trade
			var buyOrderID, sellOrderID int64
			if incoming.Side == Buy {
				buyOrderID = incoming.ID
				sellOrderID = resting.ID
			} else {
				buyOrderID = resting.ID
				sellOrderID = incoming.ID
			}

			trade := NewTrade(ob.InstrumentID, buyOrderID, sellOrderID, matchPrice, matchQty)
			trades = append(trades, trade)

			// Update quantities
			incoming.ReduceQuantity(matchQty)
			resting.ReduceQuantity(matchQty)

			if resting.IsFilled() {
				ordersToRemove = append(ordersToRemove, resting.ID)
			}
		}

		// Remove filled orders
		for _, orderID := range ordersToRemove {
			level.RemoveOrder(orderID)
		}

		// Clean up empty price level
		if level.IsEmpty() {
			delete(oppositeBook, price)
		}
	}

	// Return remaining order if not fully filled
	if !incoming.IsFilled() {
		return trades, incoming
	}
	return trades, nil
}

// Clear removes all orders from the book
func (ob *OrderBook) Clear() {
	ob.Bids = make(map[float64]*PriceLevel)
	ob.Asks = make(map[float64]*PriceLevel)
}

// OrderBookSnapshot represents a point-in-time snapshot of the order book
type OrderBookSnapshot struct {
	InstrumentID   int64
	BestBid        *float64
	BestAsk        *float64
	Spread         *float64
	TotalBidOrders int
	TotalAskOrders int
	TotalBidQty    float64
	TotalAskQty    float64
}

// Snapshot creates a snapshot of the current order book state
func (ob *OrderBook) Snapshot() *OrderBookSnapshot {
	snapshot := &OrderBookSnapshot{
		InstrumentID:   ob.InstrumentID,
		TotalBidOrders: ob.TotalBidOrders(),
		TotalAskOrders: ob.TotalAskOrders(),
		TotalBidQty:    ob.TotalBidQuantity(),
		TotalAskQty:    ob.TotalAskQuantity(),
	}

	if bestBid, ok := ob.BestBid(); ok {
		snapshot.BestBid = &bestBid
	}
	if bestAsk, ok := ob.BestAsk(); ok {
		snapshot.BestAsk = &bestAsk
	}
	if spread, ok := ob.Spread(); ok {
		snapshot.Spread = &spread
	}

	return snapshot
}
