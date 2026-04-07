package domain

// Side represents the order side (Buy or Sell)
type Side uint8

const (
	Buy Side = iota
	Sell
)

func (s Side) String() string {
	switch s {
	case Buy:
		return "Buy"
	case Sell:
		return "Sell"
	default:
		return "Unknown"
	}
}

// OrderType represents the type of order (Limit or Market)
type OrderType uint8

const (
	Limit OrderType = iota
	Market
)

func (t OrderType) String() string {
	switch t {
	case Limit:
		return "Limit"
	case Market:
		return "Market"
	default:
		return "Unknown"
	}
}

// OrderStatus represents the current status of an order
type OrderStatus uint8

const (
	New OrderStatus = iota
	PartiallyFilled
	Filled
	Cancelled
	Rejected
)

func (s OrderStatus) String() string {
	switch s {
	case New:
		return "New"
	case PartiallyFilled:
		return "PartiallyFilled"
	case Filled:
		return "Filled"
	case Cancelled:
		return "Cancelled"
	case Rejected:
		return "Rejected"
	default:
		return "Unknown"
	}
}
