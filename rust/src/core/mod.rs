//! Core infrastructure implementations demonstrating mechanical sympathy principles.
//!
//! This module contains:
//! - Cache padding for false sharing prevention
//! - Single writer agents using tokio channels
//! - Natural batching without artificial delays
//! - Sequential memory access buffers

pub mod agents;
pub mod batching;
pub mod cache_padding;
pub mod sequential_buffer;

pub use agents::{Agent, AgentHandle, OrderMatchingAgent, StatsAgent, StatsMessage, StatsSnapshot};
pub use batching::{BatchingOptions, NaturalBatcher};
pub use cache_padding::{BadCounter, GoodCounter};
pub use sequential_buffer::SequentialOrderBuffer;
