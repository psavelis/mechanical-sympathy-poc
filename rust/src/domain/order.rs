//! Order entity with cache-friendly field layout.
//!
//! Fields are ordered to maximize cache line utilization during hot path operations.

use chrono::{DateTime, Utc};
use serde::{Deserialize, Serialize};
use std::sync::atomic::{AtomicU64, Ordering};

/// Order side (Buy or Sell)
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[repr(u8)]
pub enum Side {
    Buy = 0,
    Sell = 1,
}

/// Order type
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[repr(u8)]
pub enum OrderType {
    Limit = 0,
    Market = 1,
}

/// Order status
#[derive(Debug, Clone, Copy, PartialEq, Eq, Hash, Serialize, Deserialize)]
#[repr(u8)]
pub enum OrderStatus {
    New = 0,
    PartiallyFilled = 1,
    Filled = 2,
    Cancelled = 3,
    Rejected = 4,
}

/// Global order ID generator
static NEXT_ORDER_ID: AtomicU64 = AtomicU64::new(1);

/// Order entity with cache-friendly layout.
///
/// Hot path fields are grouped together in the first cache line (64 bytes):
/// - id: 8 bytes
/// - instrument_id: 8 bytes
/// - price: 8 bytes (f64)
/// - quantity: 8 bytes (f64)
/// - original_quantity: 8 bytes (f64)
/// - side: 1 byte
/// - order_type: 1 byte
/// - status: 1 byte
///
/// Total: ~51 bytes in hot path
#[derive(Debug, Clone, Serialize, Deserialize)]
#[repr(C)]
pub struct Order {
    // Hot path fields - first cache line
    pub id: u64,
    pub instrument_id: u64,
    pub price: f64,
    pub quantity: f64,
    pub original_quantity: f64,
    pub side: Side,
    pub order_type: OrderType,
    pub status: OrderStatus,

    // Cold path fields - second cache line
    pub client_id: u64,
    pub client_order_id: Option<String>,
    pub created_at: DateTime<Utc>,
}

impl Order {
    /// Creates a new order with the specified parameters.
    pub fn new(
        instrument_id: u64,
        side: Side,
        order_type: OrderType,
        price: f64,
        quantity: f64,
        client_id: u64,
    ) -> Self {
        Self {
            id: NEXT_ORDER_ID.fetch_add(1, Ordering::Relaxed),
            instrument_id,
            price,
            quantity,
            original_quantity: quantity,
            side,
            order_type,
            status: OrderStatus::New,
            client_id,
            client_order_id: None,
            created_at: Utc::now(),
        }
    }

    /// Creates a new order with a specific ID (for testing/benchmarking).
    pub fn with_id(
        id: u64,
        instrument_id: u64,
        side: Side,
        order_type: OrderType,
        price: f64,
        quantity: f64,
        client_id: u64,
    ) -> Self {
        Self {
            id,
            instrument_id,
            price,
            quantity,
            original_quantity: quantity,
            side,
            order_type,
            status: OrderStatus::New,
            client_id,
            client_order_id: None,
            created_at: Utc::now(),
        }
    }

    /// Creates a new order with client order ID.
    pub fn with_client_order_id(
        instrument_id: u64,
        side: Side,
        order_type: OrderType,
        price: f64,
        quantity: f64,
        client_id: u64,
        client_order_id: String,
    ) -> Self {
        Self {
            id: NEXT_ORDER_ID.fetch_add(1, Ordering::Relaxed),
            instrument_id,
            price,
            quantity,
            original_quantity: quantity,
            side,
            order_type,
            status: OrderStatus::New,
            client_id,
            client_order_id: Some(client_order_id),
            created_at: Utc::now(),
        }
    }

    /// Returns true if the order is fully filled.
    #[inline]
    pub fn is_filled(&self) -> bool {
        self.quantity <= 0.0 || self.status == OrderStatus::Filled
    }

    /// Returns the filled quantity.
    #[inline]
    pub fn filled_quantity(&self) -> f64 {
        self.original_quantity - self.quantity
    }

    /// Reduces the order quantity by the specified amount.
    #[inline]
    pub fn reduce_quantity(&mut self, amount: f64) {
        self.quantity -= amount;
        if self.quantity <= 0.0 {
            self.status = OrderStatus::Filled;
        } else {
            self.status = OrderStatus::PartiallyFilled;
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_order_creation() {
        let order = Order::new(100, Side::Buy, OrderType::Limit, 150.50, 1000.0, 42);

        assert_eq!(order.instrument_id, 100);
        assert_eq!(order.side, Side::Buy);
        assert_eq!(order.order_type, OrderType::Limit);
        assert!((order.price - 150.50).abs() < f64::EPSILON);
        assert!((order.quantity - 1000.0).abs() < f64::EPSILON);
        assert!((order.original_quantity - 1000.0).abs() < f64::EPSILON);
        assert_eq!(order.client_id, 42);
        assert_eq!(order.status, OrderStatus::New);
    }

    #[test]
    fn test_reduce_quantity() {
        let mut order = Order::new(1, Side::Buy, OrderType::Limit, 100.0, 1000.0, 1);

        order.reduce_quantity(500.0);
        assert!((order.quantity - 500.0).abs() < f64::EPSILON);
        assert_eq!(order.status, OrderStatus::PartiallyFilled);

        order.reduce_quantity(500.0);
        assert!(order.quantity <= 0.0);
        assert_eq!(order.status, OrderStatus::Filled);
    }
}
