//! Order book managing bid and ask price levels.
//!
//! Uses sorted Vecs for sequential cache-friendly iteration during matching.

use super::{Order, PriceLevel, Side, Trade};
use std::collections::BTreeMap;

/// Order book managing bid and ask sides.
#[derive(Debug)]
pub struct OrderBook {
    instrument_id: u64,
    /// Bids sorted by price descending (best bid first)
    bids: BTreeMap<OrderedFloat, PriceLevel>,
    /// Asks sorted by price ascending (best ask first)
    asks: BTreeMap<OrderedFloat, PriceLevel>,
}

/// Wrapper for f64 to enable ordering in BTreeMap
#[derive(Debug, Clone, Copy, PartialEq)]
struct OrderedFloat(f64);

impl Eq for OrderedFloat {}

impl PartialOrd for OrderedFloat {
    fn partial_cmp(&self, other: &Self) -> Option<std::cmp::Ordering> {
        Some(self.cmp(other))
    }
}

impl Ord for OrderedFloat {
    fn cmp(&self, other: &Self) -> std::cmp::Ordering {
        self.0
            .partial_cmp(&other.0)
            .unwrap_or(std::cmp::Ordering::Equal)
    }
}

impl OrderBook {
    /// Creates a new order book for the specified instrument.
    pub fn new(instrument_id: u64) -> Self {
        Self {
            instrument_id,
            bids: BTreeMap::new(),
            asks: BTreeMap::new(),
        }
    }

    /// Returns the instrument ID.
    #[inline]
    pub fn instrument_id(&self) -> u64 {
        self.instrument_id
    }

    /// Returns the best bid price, if any.
    pub fn best_bid(&self) -> Option<f64> {
        self.bids.iter().next_back().map(|(k, _)| k.0)
    }

    /// Returns the best ask price, if any.
    pub fn best_ask(&self) -> Option<f64> {
        self.asks.iter().next().map(|(k, _)| k.0)
    }

    /// Returns the spread (best ask - best bid), if both sides have orders.
    pub fn spread(&self) -> Option<f64> {
        match (self.best_bid(), self.best_ask()) {
            (Some(bid), Some(ask)) => Some(ask - bid),
            _ => None,
        }
    }

    /// Returns total number of bid orders.
    pub fn total_bid_orders(&self) -> usize {
        self.bids.values().map(|l| l.order_count()).sum()
    }

    /// Returns total number of ask orders.
    pub fn total_ask_orders(&self) -> usize {
        self.asks.values().map(|l| l.order_count()).sum()
    }

    /// Adds an order to the book.
    pub fn add_order(&mut self, order: Order) {
        let price = OrderedFloat(order.price);
        let book = match order.side {
            Side::Buy => &mut self.bids,
            Side::Sell => &mut self.asks,
        };

        book.entry(price)
            .or_insert_with(|| PriceLevel::new(order.price))
            .add_order(order);
    }

    /// Removes an order from the book.
    pub fn remove_order(&mut self, order: &Order) -> bool {
        let price = OrderedFloat(order.price);
        let book = match order.side {
            Side::Buy => &mut self.bids,
            Side::Sell => &mut self.asks,
        };

        if let Some(level) = book.get_mut(&price) {
            if level.remove_order(order.id).is_some() {
                if level.is_empty() {
                    book.remove(&price);
                }
                return true;
            }
        }
        false
    }

    /// Gets the opposite side price levels for matching.
    pub fn get_opposite_side(&self, side: Side) -> impl Iterator<Item = &PriceLevel> {
        match side {
            Side::Buy => {
                // Buyer matches against asks (lowest first)
                Box::new(self.asks.values()) as Box<dyn Iterator<Item = &PriceLevel>>
            }
            Side::Sell => {
                // Seller matches against bids (highest first)
                Box::new(self.bids.values().rev()) as Box<dyn Iterator<Item = &PriceLevel>>
            }
        }
    }

    /// Matches an incoming order against the book and returns trades.
    pub fn match_order(&mut self, mut incoming: Order) -> (Vec<Trade>, Option<Order>) {
        let mut trades = Vec::new();

        let opposite_book = match incoming.side {
            Side::Buy => &mut self.asks,
            Side::Sell => &mut self.bids,
        };

        let mut levels_to_remove = Vec::new();

        // Collect matching prices
        let matching_prices: Vec<OrderedFloat> = match incoming.side {
            Side::Buy => opposite_book
                .keys()
                .filter(|p| p.0 <= incoming.price)
                .copied()
                .collect(),
            Side::Sell => opposite_book
                .keys()
                .rev()
                .filter(|p| p.0 >= incoming.price)
                .copied()
                .collect(),
        };

        for price_key in matching_prices {
            if incoming.is_filled() {
                break;
            }

            if let Some(level) = opposite_book.get_mut(&price_key) {
                let orders = level.orders_mut();
                let mut orders_to_remove = Vec::new();

                for (idx, resting) in orders.iter_mut().enumerate() {
                    if incoming.is_filled() {
                        break;
                    }

                    let match_qty = incoming.quantity.min(resting.quantity);
                    let match_price = resting.price;

                    // Create trade
                    let (buy_id, sell_id) = match incoming.side {
                        Side::Buy => (incoming.id, resting.id),
                        Side::Sell => (resting.id, incoming.id),
                    };

                    trades.push(Trade::new(
                        self.instrument_id,
                        buy_id,
                        sell_id,
                        match_price,
                        match_qty,
                    ));

                    incoming.reduce_quantity(match_qty);
                    resting.reduce_quantity(match_qty);

                    if resting.is_filled() {
                        orders_to_remove.push(idx);
                    }
                }

                // Remove filled orders (in reverse to maintain indices)
                for idx in orders_to_remove.into_iter().rev() {
                    orders.remove(idx);
                }

                if level.is_empty() {
                    levels_to_remove.push(price_key);
                }
            }
        }

        // Remove empty levels
        for price in levels_to_remove {
            opposite_book.remove(&price);
        }

        // Return remaining order if not fully filled
        let remaining = if !incoming.is_filled() {
            Some(incoming)
        } else {
            None
        };

        (trades, remaining)
    }

    /// Clears all orders from the book.
    pub fn clear(&mut self) {
        self.bids.clear();
        self.asks.clear();
    }
}

/// Snapshot of order book state for external consumption.
#[derive(Debug, Clone)]
pub struct OrderBookSnapshot {
    pub instrument_id: u64,
    pub best_bid: Option<f64>,
    pub best_ask: Option<f64>,
    pub spread: Option<f64>,
    pub total_bid_orders: usize,
    pub total_ask_orders: usize,
}

impl From<&OrderBook> for OrderBookSnapshot {
    fn from(book: &OrderBook) -> Self {
        Self {
            instrument_id: book.instrument_id(),
            best_bid: book.best_bid(),
            best_ask: book.best_ask(),
            spread: book.spread(),
            total_bid_orders: book.total_bid_orders(),
            total_ask_orders: book.total_ask_orders(),
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::domain::OrderType;

    #[test]
    fn test_order_book_basic() {
        let mut book = OrderBook::new(100);
        assert_eq!(book.instrument_id(), 100);
        assert!(book.best_bid().is_none());
        assert!(book.best_ask().is_none());

        // Add a buy order
        let buy = Order::with_id(1, 100, Side::Buy, OrderType::Limit, 99.0, 100.0, 1);
        book.add_order(buy);
        assert!((book.best_bid().unwrap() - 99.0).abs() < f64::EPSILON);

        // Add a sell order
        let sell = Order::with_id(2, 100, Side::Sell, OrderType::Limit, 101.0, 100.0, 1);
        book.add_order(sell);
        assert!((book.best_ask().unwrap() - 101.0).abs() < f64::EPSILON);
        assert!((book.spread().unwrap() - 2.0).abs() < f64::EPSILON);
    }

    #[test]
    fn test_order_matching() {
        let mut book = OrderBook::new(1);

        // Add resting sell order
        let sell = Order::with_id(1, 1, Side::Sell, OrderType::Limit, 100.0, 100.0, 1);
        book.add_order(sell);

        // Add crossing buy order
        let buy = Order::with_id(2, 1, Side::Buy, OrderType::Limit, 100.0, 100.0, 2);
        let (trades, remaining) = book.match_order(buy);

        assert_eq!(trades.len(), 1);
        assert!((trades[0].quantity - 100.0).abs() < f64::EPSILON);
        assert!((trades[0].price - 100.0).abs() < f64::EPSILON);
        assert!(remaining.is_none());
    }

    #[test]
    fn test_partial_fill() {
        let mut book = OrderBook::new(1);

        // Add large resting sell order
        let sell = Order::with_id(1, 1, Side::Sell, OrderType::Limit, 100.0, 1000.0, 1);
        book.add_order(sell);

        // Add smaller crossing buy order
        let buy = Order::with_id(2, 1, Side::Buy, OrderType::Limit, 100.0, 300.0, 2);
        let (trades, remaining) = book.match_order(buy);

        assert_eq!(trades.len(), 1);
        assert!((trades[0].quantity - 300.0).abs() < f64::EPSILON);
        assert!(remaining.is_none());
    }
}
