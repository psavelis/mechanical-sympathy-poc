//! Natural Batching implementation.
//!
//! Natural batching allows processing to begin immediately when data is available,
//! and the batch grows while more data arrives. Unlike artificial batching with
//! timeouts, this approach:
//!
//! 1. Has no artificial latency - processing starts immediately
//! 2. Automatically adapts to load - batches are larger under high load
//! 3. Maintains throughput without sacrificing latency

use tokio::sync::mpsc;

/// Options for natural batching.
#[derive(Debug, Clone)]
pub struct BatchingOptions {
    /// Maximum batch size before processing.
    pub max_batch_size: usize,
    /// Initial capacity for batch vector.
    pub initial_capacity: usize,
}

impl Default for BatchingOptions {
    fn default() -> Self {
        Self {
            max_batch_size: 100,
            initial_capacity: 16,
        }
    }
}

impl BatchingOptions {
    /// Creates new batching options with the specified max batch size.
    pub fn with_max_size(max_batch_size: usize) -> Self {
        Self {
            max_batch_size,
            initial_capacity: max_batch_size.min(64),
        }
    }
}

/// Natural batcher that collects items into batches without artificial delays.
pub struct NaturalBatcher<T> {
    receiver: mpsc::Receiver<T>,
    options: BatchingOptions,
    current_batch: Vec<T>,
}

impl<T> NaturalBatcher<T> {
    /// Creates a new natural batcher.
    pub fn new(receiver: mpsc::Receiver<T>, options: BatchingOptions) -> Self {
        Self {
            receiver,
            current_batch: Vec::with_capacity(options.initial_capacity),
            options,
        }
    }

    /// Creates a natural batcher with a new channel.
    pub fn with_channel(capacity: usize, options: BatchingOptions) -> (mpsc::Sender<T>, Self) {
        let (tx, rx) = mpsc::channel(capacity);
        (tx, Self::new(rx, options))
    }

    /// Gets the next batch of items.
    ///
    /// This method:
    /// 1. Waits for at least one item (no busy polling)
    /// 2. Drains all immediately available items up to max_batch_size
    /// 3. Returns the batch for processing
    ///
    /// Returns None when the channel is closed.
    pub async fn next_batch(&mut self) -> Option<Vec<T>> {
        // Wait for first item (blocking)
        let first = self.receiver.recv().await?;

        self.current_batch.clear();
        self.current_batch.push(first);

        // Drain all immediately available items (non-blocking)
        while self.current_batch.len() < self.options.max_batch_size {
            match self.receiver.try_recv() {
                Ok(item) => self.current_batch.push(item),
                Err(_) => break,
            }
        }

        // Return ownership of the batch
        let batch = std::mem::replace(
            &mut self.current_batch,
            Vec::with_capacity(self.options.initial_capacity),
        );

        Some(batch)
    }

    /// Runs the batcher with a processing function.
    pub async fn run<F, Fut>(mut self, mut process: F)
    where
        F: FnMut(Vec<T>) -> Fut,
        Fut: std::future::Future<Output = ()>,
    {
        while let Some(batch) = self.next_batch().await {
            process(batch).await;
        }
    }

    /// Returns the current batch size (for monitoring).
    pub fn current_batch_size(&self) -> usize {
        self.current_batch.len()
    }

    /// Returns the max batch size configuration.
    pub fn max_batch_size(&self) -> usize {
        self.options.max_batch_size
    }
}

/// Demonstrates natural batching behavior.
pub async fn run_batching_demo() -> Vec<usize> {
    let (tx, mut batcher) =
        NaturalBatcher::<i32>::with_channel(1024, BatchingOptions::with_max_size(50));

    // Spawn producer that sends items in bursts
    let producer = tokio::spawn(async move {
        // Burst 1: 10 items
        for i in 0..10 {
            tx.send(i).await.unwrap();
        }
        tokio::time::sleep(tokio::time::Duration::from_millis(10)).await;

        // Burst 2: 100 items (will be split into batches of 50)
        for i in 10..110 {
            tx.send(i).await.unwrap();
        }
        tokio::time::sleep(tokio::time::Duration::from_millis(10)).await;

        // Burst 3: 5 items
        for i in 110..115 {
            tx.send(i).await.unwrap();
        }
    });

    // Consumer collecting batch sizes
    let consumer = tokio::spawn(async move {
        let mut sizes = Vec::new();
        while let Some(batch) = batcher.next_batch().await {
            sizes.push(batch.len());
        }
        sizes
    });

    producer.await.unwrap();
    consumer.await.unwrap()
}

#[cfg(test)]
mod tests {
    use super::*;

    #[tokio::test]
    async fn test_natural_batching() {
        let (tx, mut batcher) =
            NaturalBatcher::<i32>::with_channel(100, BatchingOptions::with_max_size(10));

        // Send 5 items quickly
        for i in 0..5 {
            tx.send(i).await.unwrap();
        }

        // Drop sender to close channel
        drop(tx);

        // Should get all items in one batch (since they arrived together)
        let batch = batcher.next_batch().await.unwrap();
        assert_eq!(batch.len(), 5);

        // Channel closed
        assert!(batcher.next_batch().await.is_none());
    }

    #[tokio::test]
    async fn test_max_batch_size() {
        let (tx, mut batcher) =
            NaturalBatcher::<i32>::with_channel(100, BatchingOptions::with_max_size(5));

        // Send 12 items quickly
        for i in 0..12 {
            tx.send(i).await.unwrap();
        }

        // First batch should be max size
        let batch1 = batcher.next_batch().await.unwrap();
        assert_eq!(batch1.len(), 5);

        // Second batch should be max size
        let batch2 = batcher.next_batch().await.unwrap();
        assert_eq!(batch2.len(), 5);

        // Drop sender
        drop(tx);

        // Third batch should have remaining items
        let batch3 = batcher.next_batch().await.unwrap();
        assert_eq!(batch3.len(), 2);
    }
}
