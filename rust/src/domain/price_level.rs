//! Price level representing all orders at a specific price point.
//!
//! Uses a Vec for sequential iteration during matching (cache-friendly).
//! Orders are stored in FIFO order for price-time priority.

use super::Order;

/// Represents all orders at a specific price point.
#[derive(Debug)]
pub struct PriceLevel {
    price: f64,
    orders: Vec<Order>,
}

impl PriceLevel {
    /// Creates a new price level with the specified price.
    pub fn new(price: f64) -> Self {
        Self {
            price,
            orders: Vec::with_capacity(16),
        }
    }

    /// Creates a new price level with specified initial capacity.
    pub fn with_capacity(price: f64, capacity: usize) -> Self {
        Self {
            price,
            orders: Vec::with_capacity(capacity),
        }
    }

    /// Returns the price for this level.
    #[inline]
    pub fn price(&self) -> f64 {
        self.price
    }

    /// Returns read-only access to orders at this level.
    #[inline]
    pub fn orders(&self) -> &[Order] {
        &self.orders
    }

    /// Returns mutable access to orders at this level.
    #[inline]
    pub fn orders_mut(&mut self) -> &mut Vec<Order> {
        &mut self.orders
    }

    /// Total quantity available at this price level.
    #[inline]
    pub fn total_quantity(&self) -> f64 {
        // Sequential iteration for cache efficiency
        self.orders.iter().map(|o| o.quantity).sum()
    }

    /// Number of orders at this price level.
    #[inline]
    pub fn order_count(&self) -> usize {
        self.orders.len()
    }

    /// Whether this price level is empty.
    #[inline]
    pub fn is_empty(&self) -> bool {
        self.orders.is_empty()
    }

    /// Adds an order to this price level (FIFO order).
    #[inline]
    pub fn add_order(&mut self, order: Order) {
        self.orders.push(order);
    }

    /// Removes an order from this price level by ID.
    pub fn remove_order(&mut self, order_id: u64) -> Option<Order> {
        if let Some(pos) = self.orders.iter().position(|o| o.id == order_id) {
            Some(self.orders.remove(pos))
        } else {
            None
        }
    }

    /// Removes the first order from this price level.
    #[inline]
    pub fn remove_first(&mut self) -> Option<Order> {
        if self.orders.is_empty() {
            None
        } else {
            Some(self.orders.remove(0))
        }
    }

    /// Clears all orders from this price level.
    #[inline]
    pub fn clear(&mut self) {
        self.orders.clear();
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::domain::{OrderType, Side};

    #[test]
    fn test_price_level_operations() {
        let mut level = PriceLevel::new(100.0);
        assert!(level.is_empty());

        let order1 = Order::with_id(1, 1, Side::Buy, OrderType::Limit, 100.0, 100.0, 1);
        let order2 = Order::with_id(2, 1, Side::Buy, OrderType::Limit, 100.0, 200.0, 1);

        level.add_order(order1);
        level.add_order(order2);

        assert_eq!(level.order_count(), 2);
        assert!((level.total_quantity() - 300.0).abs() < f64::EPSILON);

        let removed = level.remove_first().unwrap();
        assert_eq!(removed.id, 1);
        assert_eq!(level.order_count(), 1);
    }
}
