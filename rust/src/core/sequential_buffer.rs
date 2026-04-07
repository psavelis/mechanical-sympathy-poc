//! Sequential memory access buffer for cache-friendly iteration.
//!
//! Modern CPUs have multi-level cache hierarchies that work best with
//! sequential memory access patterns. When iterating through contiguous
//! memory, the CPU can prefetch upcoming cache lines, hiding memory latency.
//!
//! Random access patterns defeat this prefetching, causing cache misses
//! and significantly slower performance.

use crate::domain::Order;

/// Cache-aligned buffer for sequential order access.
///
/// Uses a contiguous Vec for cache-friendly iteration during order matching.
#[repr(C, align(64))]
pub struct SequentialOrderBuffer {
    data: Vec<Order>,
    capacity: usize,
}

impl SequentialOrderBuffer {
    /// Creates a new buffer with the specified capacity.
    pub fn with_capacity(capacity: usize) -> Self {
        Self {
            data: Vec::with_capacity(capacity),
            capacity,
        }
    }

    /// Returns the number of orders in the buffer.
    #[inline]
    pub fn len(&self) -> usize {
        self.data.len()
    }

    /// Returns true if the buffer is empty.
    #[inline]
    pub fn is_empty(&self) -> bool {
        self.data.is_empty()
    }

    /// Returns the capacity of the buffer.
    #[inline]
    pub fn capacity(&self) -> usize {
        self.capacity
    }

    /// Adds an order to the buffer.
    #[inline]
    pub fn push(&mut self, order: Order) {
        self.data.push(order);
    }

    /// Removes and returns the last order.
    #[inline]
    pub fn pop(&mut self) -> Option<Order> {
        self.data.pop()
    }

    /// Clears all orders from the buffer.
    #[inline]
    pub fn clear(&mut self) {
        self.data.clear();
    }

    /// Returns an iterator over orders (sequential access).
    #[inline]
    pub fn iter(&self) -> impl Iterator<Item = &Order> {
        self.data.iter()
    }

    /// Returns a mutable iterator over orders (sequential access).
    #[inline]
    pub fn iter_mut(&mut self) -> impl Iterator<Item = &mut Order> {
        self.data.iter_mut()
    }

    /// Returns a slice of orders for direct sequential access.
    #[inline]
    pub fn as_slice(&self) -> &[Order] {
        &self.data
    }

    /// Returns a mutable slice of orders for direct sequential access.
    #[inline]
    pub fn as_mut_slice(&mut self) -> &mut [Order] {
        &mut self.data
    }

    /// Processes all orders with a sequential access pattern.
    ///
    /// This is cache-friendly because we iterate through contiguous memory.
    #[inline]
    pub fn process_sequential<F>(&self, mut f: F)
    where
        F: FnMut(&Order),
    {
        for order in &self.data {
            f(order);
        }
    }

    /// Sums a field from all orders using sequential access.
    pub fn sum_quantities(&self) -> f64 {
        let mut sum = 0.0;
        // Sequential iteration - CPU can prefetch next cache lines
        for order in &self.data {
            sum += order.quantity;
        }
        sum
    }
}

/// Demonstrates sequential vs random access patterns.
pub fn run_sequential_access_demo(
    size: usize,
    iterations: usize,
) -> (std::time::Duration, std::time::Duration) {
    use crate::domain::{OrderType, Side};
    use std::time::Instant;

    // Create test data
    let orders: Vec<Order> = (0..size)
        .map(|i| Order::with_id(i as u64, 1, Side::Buy, OrderType::Limit, 100.0, 100.0, 1))
        .collect();

    // Generate random indices for random access
    let mut random_indices: Vec<usize> = (0..size).collect();
    // Simple shuffle using a deterministic pattern
    for i in 0..size {
        let j = (i * 31 + 17) % size;
        random_indices.swap(i, j);
    }

    // Sequential access benchmark
    let sequential_start = Instant::now();
    let mut sequential_sum = 0.0;
    for _ in 0..iterations {
        for order in &orders {
            sequential_sum += order.quantity;
        }
    }
    let sequential_duration = sequential_start.elapsed();

    // Random access benchmark
    let random_start = Instant::now();
    let mut random_sum = 0.0;
    for _ in 0..iterations {
        for &idx in &random_indices {
            random_sum += orders[idx].quantity;
        }
    }
    let random_duration = random_start.elapsed();

    // Prevent optimization from removing the loops
    assert!((sequential_sum - random_sum).abs() < 1.0);

    (sequential_duration, random_duration)
}

/// Simulates processing with pointer chasing (cache-unfriendly).
pub struct LinkedNode {
    pub value: i64,
    pub next: Option<Box<LinkedNode>>,
}

impl LinkedNode {
    /// Creates a linked list of the specified size.
    pub fn create_list(size: usize) -> Option<Box<LinkedNode>> {
        let mut head: Option<Box<LinkedNode>> = None;
        for i in (0..size).rev() {
            head = Some(Box::new(LinkedNode {
                value: i as i64,
                next: head,
            }));
        }
        head
    }

    /// Sums all values by traversing the linked list.
    pub fn sum_linked(node: &Option<Box<LinkedNode>>) -> i64 {
        let mut sum = 0;
        let mut current = node;
        while let Some(ref n) = current {
            sum += n.value;
            current = &n.next;
        }
        sum
    }
}

#[cfg(test)]
mod tests {
    use super::*;
    use crate::domain::{OrderType, Side};

    #[test]
    fn test_sequential_buffer() {
        let mut buffer = SequentialOrderBuffer::with_capacity(100);

        for i in 0..10 {
            buffer.push(Order::with_id(
                i,
                1,
                Side::Buy,
                OrderType::Limit,
                100.0,
                (i + 1) as f64 * 10.0,
                1,
            ));
        }

        assert_eq!(buffer.len(), 10);
        assert!((buffer.sum_quantities() - 550.0).abs() < f64::EPSILON);
    }

    #[test]
    fn test_linked_list() {
        let list = LinkedNode::create_list(100);
        let sum = LinkedNode::sum_linked(&list);
        assert_eq!(sum, (0..100).sum::<i64>());
    }
}
