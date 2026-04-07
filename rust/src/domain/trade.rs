//! Trade entity representing an executed match between two orders.

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::sync::atomic::{AtomicU64, Ordering};

/// Global trade ID generator
static NEXT_TRADE_ID: AtomicU64 = AtomicU64::new(1);

/// Trade entity representing an executed match.
#[derive(Debug, Clone, Serialize, Deserialize)]
pub struct Trade {
    pub id: u64,
    pub instrument_id: u64,
    pub buy_order_id: u64,
    pub sell_order_id: u64,
    pub price: f64,
    pub quantity: f64,
    pub executed_at: DateTime<Utc>,
}

impl Trade {
    /// Creates a new trade from matched orders.
    pub fn new(
        instrument_id: u64,
        buy_order_id: u64,
        sell_order_id: u64,
        price: f64,
        quantity: f64,
    ) -> Self {
        Self {
            id: NEXT_TRADE_ID.fetch_add(1, Ordering::Relaxed),
            instrument_id,
            buy_order_id,
            sell_order_id,
            price,
            quantity,
            executed_at: Utc::now(),
        }
    }

    /// Creates a trade with a specific ID (for testing).
    pub fn with_id(
        id: u64,
        instrument_id: u64,
        buy_order_id: u64,
        sell_order_id: u64,
        price: f64,
        quantity: f64,
    ) -> Self {
        Self {
            id,
            instrument_id,
            buy_order_id,
            sell_order_id,
            price,
            quantity,
            executed_at: Utc::now(),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_trade_creation() {
        let trade = Trade::new(100, 1, 2, 150.50, 500.0);

        assert_eq!(trade.instrument_id, 100);
        assert_eq!(trade.buy_order_id, 1);
        assert_eq!(trade.sell_order_id, 2);
        assert!((trade.price - 150.50).abs() < f64::EPSILON);
        assert!((trade.quantity - 500.0).abs() < f64::EPSILON);
    }
}
