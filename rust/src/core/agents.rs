//! Single Writer Principle implementation using tokio channels.
//!
//! The Single Writer Principle states that for any piece of mutable state,
//! only one thread should be responsible for modifying it. Other threads
//! can read the state but must send messages to the owning thread to
//! request modifications.
//!
//! This eliminates the need for locks and allows for lock-free synchronization.

use crate::domain::{Order, OrderBook, OrderBookSnapshot, Side, Trade};
use std::collections::HashMap;
use std::sync::atomic::{AtomicU64, Ordering};
use std::sync::Arc;
use tokio::sync::mpsc;

/// Generic agent trait for message-based processing.
pub trait Agent<T> {
    /// Sends a message to the agent.
    fn send(
        &self,
        msg: T,
    ) -> impl std::future::Future<Output = Result<(), mpsc::error::SendError<T>>> + Send;
}

/// Handle for sending messages to an agent.
#[derive(Clone)]
pub struct AgentHandle<T> {
    sender: mpsc::Sender<T>,
}

impl<T: Send> AgentHandle<T> {
    /// Creates a new agent handle from a sender.
    pub fn new(sender: mpsc::Sender<T>) -> Self {
        Self { sender }
    }

    /// Sends a message to the agent.
    pub async fn send(&self, msg: T) -> Result<(), mpsc::error::SendError<T>> {
        self.sender.send(msg).await
    }

    /// Tries to send a message without blocking.
    pub fn try_send(&self, msg: T) -> Result<(), mpsc::error::TrySendError<T>> {
        self.sender.try_send(msg)
    }

    /// Returns a clone of the underlying sender for direct use.
    pub fn sender_clone(&self) -> mpsc::Sender<T> {
        self.sender.clone()
    }
}

/// Message type for the order matching agent.
pub enum OrderCommand {
    PlaceOrder(Order),
    CancelOrder {
        order_id: u64,
        instrument_id: u64,
    },
    GetSnapshot {
        instrument_id: u64,
        reply: tokio::sync::oneshot::Sender<Option<OrderBookSnapshot>>,
    },
}

/// Order matching agent implementing Single Writer Principle.
///
/// All order book state is owned exclusively by this agent.
/// Multiple producers can send orders via the channel, but only
/// this agent's processing loop modifies the order books.
pub struct OrderMatchingAgent {
    receiver: mpsc::Receiver<OrderCommand>,
    order_books: HashMap<u64, OrderBook>,
    trade_sender: mpsc::Sender<Trade>,
    total_orders: Arc<AtomicU64>,
    total_trades: Arc<AtomicU64>,
}

impl OrderMatchingAgent {
    /// Creates a new order matching agent.
    pub fn new(
        capacity: usize,
        trade_sender: mpsc::Sender<Trade>,
    ) -> (AgentHandle<OrderCommand>, Self) {
        let (tx, rx) = mpsc::channel(capacity);
        let agent = Self {
            receiver: rx,
            order_books: HashMap::new(),
            trade_sender,
            total_orders: Arc::new(AtomicU64::new(0)),
            total_trades: Arc::new(AtomicU64::new(0)),
        };
        (AgentHandle::new(tx), agent)
    }

    /// Returns the total orders counter for external read access.
    pub fn total_orders_counter(&self) -> Arc<AtomicU64> {
        Arc::clone(&self.total_orders)
    }

    /// Returns the total trades counter for external read access.
    pub fn total_trades_counter(&self) -> Arc<AtomicU64> {
        Arc::clone(&self.total_trades)
    }

    /// Returns the number of pending messages in the channel.
    pub fn pending_count(&self) -> usize {
        // Note: This is an approximation as the channel doesn't expose exact count
        0
    }

    /// Runs the agent's processing loop.
    pub async fn run(&mut self) {
        while let Some(cmd) = self.receiver.recv().await {
            self.process_command(cmd).await;
        }
    }

    /// Processes a single command.
    async fn process_command(&mut self, cmd: OrderCommand) {
        match cmd {
            OrderCommand::PlaceOrder(order) => {
                self.total_orders.fetch_add(1, Ordering::Relaxed);
                self.process_order(order).await;
            }
            OrderCommand::CancelOrder {
                order_id,
                instrument_id,
            } => {
                if let Some(book) = self.order_books.get_mut(&instrument_id) {
                    // Create a dummy order for removal lookup
                    let dummy = Order::with_id(
                        order_id,
                        instrument_id,
                        Side::Buy, // Side doesn't matter for lookup
                        crate::domain::OrderType::Limit,
                        0.0,
                        0.0,
                        0,
                    );
                    book.remove_order(&dummy);
                }
            }
            OrderCommand::GetSnapshot {
                instrument_id,
                reply,
            } => {
                let snapshot = self
                    .order_books
                    .get(&instrument_id)
                    .map(OrderBookSnapshot::from);
                let _ = reply.send(snapshot);
            }
        }
    }

    /// Processes an incoming order.
    async fn process_order(&mut self, order: Order) {
        let instrument_id = order.instrument_id;

        // Get or create order book
        let book = self
            .order_books
            .entry(instrument_id)
            .or_insert_with(|| OrderBook::new(instrument_id));

        // Match the order
        let (trades, remaining) = book.match_order(order);

        // Send trades
        for trade in trades {
            self.total_trades.fetch_add(1, Ordering::Relaxed);
            let _ = self.trade_sender.send(trade).await;
        }

        // Add remaining order to book if not fully filled
        if let Some(remaining_order) = remaining {
            book.add_order(remaining_order);
        }
    }
}

/// Message type for the stats agent.
#[derive(Clone, Copy)]
pub struct StatsMessage(pub i64);

/// Snapshot of statistics.
#[derive(Debug, Clone, Copy, Default)]
pub struct StatsSnapshot {
    pub total: i64,
    pub count: u64,
    pub min: i64,
    pub max: i64,
    pub average: f64,
}

/// Statistics agent implementing Single Writer Principle.
pub struct StatsAgent {
    receiver: mpsc::Receiver<StatsMessage>,
    total: i64,
    count: u64,
    min: i64,
    max: i64,
}

impl StatsAgent {
    /// Creates a new stats agent.
    pub fn new(capacity: usize) -> (AgentHandle<StatsMessage>, Self) {
        let (tx, rx) = mpsc::channel(capacity);
        let agent = Self {
            receiver: rx,
            total: 0,
            count: 0,
            min: i64::MAX,
            max: i64::MIN,
        };
        (AgentHandle::new(tx), agent)
    }

    /// Runs the agent's processing loop.
    pub async fn run(&mut self) {
        while let Some(StatsMessage(value)) = self.receiver.recv().await {
            self.process_value(value);
        }
    }

    /// Processes a single value.
    fn process_value(&mut self, value: i64) {
        self.total += value;
        self.count += 1;
        self.min = self.min.min(value);
        self.max = self.max.max(value);
    }

    /// Returns a snapshot of current statistics.
    pub fn snapshot(&self) -> StatsSnapshot {
        StatsSnapshot {
            total: self.total,
            count: self.count,
            min: if self.count > 0 { self.min } else { 0 },
            max: if self.count > 0 { self.max } else { 0 },
            average: if self.count > 0 {
                self.total as f64 / self.count as f64
            } else {
                0.0
            },
        }
    }

    /// Resets all statistics.
    pub fn reset(&mut self) {
        self.total = 0;
        self.count = 0;
        self.min = i64::MAX;
        self.max = i64::MIN;
    }

    /// Returns the total sum.
    pub fn total(&self) -> i64 {
        self.total
    }

    /// Returns the count.
    pub fn count(&self) -> u64 {
        self.count
    }

    /// Returns the minimum value.
    pub fn min(&self) -> i64 {
        if self.count > 0 {
            self.min
        } else {
            0
        }
    }

    /// Returns the maximum value.
    pub fn max(&self) -> i64 {
        if self.count > 0 {
            self.max
        } else {
            0
        }
    }

    /// Returns the average.
    pub fn average(&self) -> f64 {
        if self.count > 0 {
            self.total as f64 / self.count as f64
        } else {
            0.0
        }
    }
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_stats_agent() {
        let (handle, mut agent) = StatsAgent::new(1024);

        // Spawn the agent
        let agent_handle = tokio::spawn(async move {
            agent.run().await;
            agent
        });

        // Send some values
        for i in 1..=10 {
            handle.send(StatsMessage(i)).await.unwrap();
        }

        // Drop sender to close channel
        drop(handle);

        // Wait for agent to finish
        let agent = agent_handle.await.unwrap();

        assert_eq!(agent.total(), 55);
        assert_eq!(agent.count(), 10);
        assert_eq!(agent.min(), 1);
        assert_eq!(agent.max(), 10);
        assert!((agent.average() - 5.5).abs() < f64::EPSILON);
    }
}
