//! Cache padding module demonstrating false sharing prevention.
//!
//! False sharing occurs when two threads on different CPUs access different
//! variables that happen to be on the same cache line. When one thread
//! modifies its variable, it invalidates the entire cache line for the
//! other CPU, causing expensive cache coherency traffic.
//!
//! This module provides:
//! - `BadCounter`: Demonstrates false sharing (both counters share a cache line)
//! - `GoodCounter`: Uses cache padding to prevent false sharing

use cache_padded::CachePadded;
use std::sync::atomic::{AtomicU64, Ordering};

/// Cache line size in bytes (64 bytes on most modern x86/ARM processors).
pub const CACHE_LINE_SIZE: usize = 64;

/// BAD: Counter with false sharing.
///
/// Both `count1` and `count2` are adjacent in memory and likely share
/// the same cache line. When thread 1 increments `count1` and thread 2
/// increments `count2`, they will cause cache line invalidations on
/// each other's CPUs, dramatically slowing down both operations.
#[repr(C)]
pub struct BadCounter {
    pub count1: AtomicU64,
    pub count2: AtomicU64,
}

impl BadCounter {
    /// Creates a new BadCounter with both counters initialized to 0.
    pub fn new() -> Self {
        Self {
            count1: AtomicU64::new(0),
            count2: AtomicU64::new(0),
        }
    }

    /// Increments count1 atomically.
    #[inline]
    pub fn increment_count1(&self) {
        self.count1.fetch_add(1, Ordering::Relaxed);
    }

    /// Increments count2 atomically.
    #[inline]
    pub fn increment_count2(&self) {
        self.count2.fetch_add(1, Ordering::Relaxed);
    }

    /// Returns the current value of count1.
    #[inline]
    pub fn get_count1(&self) -> u64 {
        self.count1.load(Ordering::Relaxed)
    }

    /// Returns the current value of count2.
    #[inline]
    pub fn get_count2(&self) -> u64 {
        self.count2.load(Ordering::Relaxed)
    }
}

impl Default for BadCounter {
    fn default() -> Self {
        Self::new()
    }
}

/// GOOD: Counter with cache line padding to prevent false sharing.
///
/// Each counter is wrapped in `CachePadded<T>` which ensures the value
/// is aligned and padded to occupy its own cache line. This prevents
/// false sharing because modifications to one counter will never
/// invalidate the cache line containing the other counter.
pub struct GoodCounter {
    pub count1: CachePadded<AtomicU64>,
    pub count2: CachePadded<AtomicU64>,
}

impl GoodCounter {
    /// Creates a new GoodCounter with both counters initialized to 0.
    pub fn new() -> Self {
        Self {
            count1: CachePadded::new(AtomicU64::new(0)),
            count2: CachePadded::new(AtomicU64::new(0)),
        }
    }

    /// Increments count1 atomically.
    #[inline]
    pub fn increment_count1(&self) {
        self.count1.fetch_add(1, Ordering::Relaxed);
    }

    /// Increments count2 atomically.
    #[inline]
    pub fn increment_count2(&self) {
        self.count2.fetch_add(1, Ordering::Relaxed);
    }

    /// Returns the current value of count1.
    #[inline]
    pub fn get_count1(&self) -> u64 {
        self.count1.load(Ordering::Relaxed)
    }

    /// Returns the current value of count2.
    #[inline]
    pub fn get_count2(&self) -> u64 {
        self.count2.load(Ordering::Relaxed)
    }
}

impl Default for GoodCounter {
    fn default() -> Self {
        Self::new()
    }
}

/// Demonstrates the impact of false sharing by running concurrent increments.
pub fn run_false_sharing_demo(iterations: u64) -> (std::time::Duration, std::time::Duration) {
    use std::sync::Arc;
    use std::thread;
    use std::time::Instant;

    // Test BadCounter (with false sharing)
    let bad_counter = Arc::new(BadCounter::new());
    let bad_start = Instant::now();

    let bad1 = Arc::clone(&bad_counter);
    let bad2 = Arc::clone(&bad_counter);

    let handle1 = thread::spawn(move || {
        for _ in 0..iterations {
            bad1.increment_count1();
        }
    });

    let handle2 = thread::spawn(move || {
        for _ in 0..iterations {
            bad2.increment_count2();
        }
    });

    handle1.join().unwrap();
    handle2.join().unwrap();
    let bad_duration = bad_start.elapsed();

    // Test GoodCounter (without false sharing)
    let good_counter = Arc::new(GoodCounter::new());
    let good_start = Instant::now();

    let good1 = Arc::clone(&good_counter);
    let good2 = Arc::clone(&good_counter);

    let handle1 = thread::spawn(move || {
        for _ in 0..iterations {
            good1.increment_count1();
        }
    });

    let handle2 = thread::spawn(move || {
        for _ in 0..iterations {
            good2.increment_count2();
        }
    });

    handle1.join().unwrap();
    handle2.join().unwrap();
    let good_duration = good_start.elapsed();

    (bad_duration, good_duration)
}

#[cfg(test)]
mod tests {
    use super::*;

    #[test]
    fn test_bad_counter() {
        let counter = BadCounter::new();
        counter.increment_count1();
        counter.increment_count2();
        counter.increment_count1();

        assert_eq!(counter.get_count1(), 2);
        assert_eq!(counter.get_count2(), 1);
    }

    #[test]
    fn test_good_counter() {
        let counter = GoodCounter::new();
        counter.increment_count1();
        counter.increment_count2();
        counter.increment_count1();

        assert_eq!(counter.get_count1(), 2);
        assert_eq!(counter.get_count2(), 1);
    }

    #[test]
    fn test_cache_padded_size() {
        // CachePadded should be at least 64 bytes
        assert!(std::mem::size_of::<CachePadded<AtomicU64>>() >= CACHE_LINE_SIZE);
    }
}
