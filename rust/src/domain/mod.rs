//! Domain entities for the trading system.
//!
//! These entities mirror the .NET implementation to ensure fair benchmarking
//! comparison between the two platforms.

pub mod order;
pub mod order_book;
pub mod price_level;
pub mod trade;

pub use order::{Order, OrderStatus, OrderType, Side};
pub use order_book::{OrderBook, OrderBookSnapshot};
pub use price_level::PriceLevel;
pub use trade::Trade;
