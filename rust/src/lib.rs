//! Mechanical Sympathy Principles PoC in Rust
//!
//! This library demonstrates four key mechanical sympathy principles:
//!
//! 1. **Cache Lines & False Sharing Prevention** - Using cache padding to prevent
//!    false sharing between threads accessing adjacent memory locations.
//!
//! 2. **Single Writer Principle** - Using message-passing (channels) to ensure
//!    mutable state is only modified by a single thread, eliminating locks.
//!
//! 3. **Natural Batching** - Processing data in batches that form naturally
//!    based on arrival rate, without artificial delays.
//!
//! 4. **Sequential Memory Access** - Organizing data structures for sequential
//!    iteration to maximize CPU cache efficiency.
//!
//! # Example
//!
//! ```rust
//! use mechanical_sympathy::core::cache_padding::{BadCounter, GoodCounter};
//!
//! // BadCounter has false sharing issues
//! let bad = BadCounter::new();
//! bad.increment_count1();
//!
//! // GoodCounter uses cache padding to prevent false sharing
//! let good = GoodCounter::new();
//! good.increment_count1();
//! ```

pub mod core;
pub mod domain;

// Re-exports for convenience
pub use core::{
    Agent, AgentHandle, BadCounter, BatchingOptions, GoodCounter, NaturalBatcher,
    OrderMatchingAgent, SequentialOrderBuffer, StatsAgent, StatsMessage, StatsSnapshot,
};
pub use domain::{
    Order, OrderBook, OrderBookSnapshot, OrderStatus, OrderType, PriceLevel, Side, Trade,
};
