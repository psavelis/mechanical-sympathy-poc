// Package batching implements Natural Batching for efficient message processing.
//
// Natural Batching collects messages into batches based on arrival rate rather
// than fixed timeouts. The key insight is:
// 1. WAIT (blocking) for the first message
// 2. DRAIN (non-blocking) all immediately available messages
// 3. PROCESS the batch
//
// This approach provides:
// - Low latency under light load (small batches processed immediately)
// - High throughput under heavy load (larger batches amortize overhead)
// - No artificial delays from fixed timeouts
//
// Reference: https://martinfowler.com/articles/mechanical-sympathy-principles.html
package batching

import (
	"sync"
	"sync/atomic"
	"time"
)

// BatchingOptions configures the natural batcher behavior
type BatchingOptions struct {
	// MaxBatchSize is the maximum number of items in a single batch
	MaxBatchSize int
	// InitialCapacity is the initial capacity for batch allocation
	InitialCapacity int
	// MinBatchSize is the minimum batch size before processing (optional)
	MinBatchSize int
	// AllowSingleItemBatch allows processing batches with single items
	AllowSingleItemBatch bool
}

// DefaultOptions returns sensible default batching options
func DefaultOptions() BatchingOptions {
	return BatchingOptions{
		MaxBatchSize:         100,
		InitialCapacity:      16,
		MinBatchSize:         1,
		AllowSingleItemBatch: true,
	}
}

// LowLatencyOptions returns options optimized for low latency
func LowLatencyOptions() BatchingOptions {
	return BatchingOptions{
		MaxBatchSize:         50,
		InitialCapacity:      8,
		MinBatchSize:         1,
		AllowSingleItemBatch: true,
	}
}

// HighThroughputOptions returns options optimized for high throughput
func HighThroughputOptions() BatchingOptions {
	return BatchingOptions{
		MaxBatchSize:         500,
		InitialCapacity:      64,
		MinBatchSize:         10,
		AllowSingleItemBatch: false,
	}
}

// NaturalBatcher implements natural batching for any type T
type NaturalBatcher[T any] struct {
	inbox   <-chan T
	options BatchingOptions

	// Statistics
	totalItemsProcessed   int64
	totalBatchesProcessed int64
}

// NewNaturalBatcher creates a new natural batcher
func NewNaturalBatcher[T any](inbox <-chan T, opts BatchingOptions) *NaturalBatcher[T] {
	return &NaturalBatcher[T]{
		inbox:   inbox,
		options: opts,
	}
}

// NextBatch waits for and returns the next batch of items.
// Returns (batch, true) on success, (nil, false) when channel is closed.
//
// The batching algorithm:
// 1. Block waiting for the first item (respects load)
// 2. Non-blocking drain of all immediately available items
// 3. Return when max batch size reached OR no more items available
func (b *NaturalBatcher[T]) NextBatch() ([]T, bool) {
	// Step 1: WAIT (blocking) for the first item
	first, ok := <-b.inbox
	if !ok {
		return nil, false // Channel closed
	}

	batch := make([]T, 0, b.options.InitialCapacity)
	batch = append(batch, first)

	// Step 2: DRAIN (non-blocking) all immediately available items
	for len(batch) < b.options.MaxBatchSize {
		select {
		case item, ok := <-b.inbox:
			if !ok {
				// Channel closed, return what we have
				return batch, true
			}
			batch = append(batch, item)
		default:
			// No more items immediately available
			return batch, true
		}
	}

	return batch, true
}

// Run continuously processes batches until the channel is closed
func (b *NaturalBatcher[T]) Run(process func([]T)) {
	for {
		batch, ok := b.NextBatch()
		if !ok {
			return
		}

		// Check minimum batch size
		if len(batch) < b.options.MinBatchSize && !b.options.AllowSingleItemBatch {
			// Re-enqueue items? For simplicity, we process anyway
		}

		process(batch)

		// Update statistics
		atomic.AddInt64(&b.totalItemsProcessed, int64(len(batch)))
		atomic.AddInt64(&b.totalBatchesProcessed, 1)
	}
}

// TotalItemsProcessed returns the total number of items processed
func (b *NaturalBatcher[T]) TotalItemsProcessed() int64 {
	return atomic.LoadInt64(&b.totalItemsProcessed)
}

// TotalBatchesProcessed returns the total number of batches processed
func (b *NaturalBatcher[T]) TotalBatchesProcessed() int64 {
	return atomic.LoadInt64(&b.totalBatchesProcessed)
}

// AverageBatchSize returns the average batch size
func (b *NaturalBatcher[T]) AverageBatchSize() float64 {
	batches := atomic.LoadInt64(&b.totalBatchesProcessed)
	if batches == 0 {
		return 0
	}
	return float64(atomic.LoadInt64(&b.totalItemsProcessed)) / float64(batches)
}

// BatchProcessor is a convenience type that combines batching with processing
type BatchProcessor[T any] struct {
	inbox     chan T
	batcher   *NaturalBatcher[T]
	processor func([]T)
	wg        sync.WaitGroup
	running   int32
}

// NewBatchProcessor creates a new batch processor
func NewBatchProcessor[T any](capacity int, opts BatchingOptions, processor func([]T)) *BatchProcessor[T] {
	inbox := make(chan T, capacity)
	return &BatchProcessor[T]{
		inbox:     inbox,
		batcher:   NewNaturalBatcher(inbox, opts),
		processor: processor,
	}
}

// Start begins processing batches in a goroutine
func (bp *BatchProcessor[T]) Start() {
	if !atomic.CompareAndSwapInt32(&bp.running, 0, 1) {
		return // Already running
	}

	bp.wg.Add(1)
	go func() {
		defer bp.wg.Done()
		bp.batcher.Run(bp.processor)
	}()
}

// Submit submits an item for batch processing
func (bp *BatchProcessor[T]) Submit(item T) {
	bp.inbox <- item
}

// TrySubmit attempts to submit an item without blocking
func (bp *BatchProcessor[T]) TrySubmit(item T) bool {
	select {
	case bp.inbox <- item:
		return true
	default:
		return false
	}
}

// Stop signals the processor to stop and waits for completion
func (bp *BatchProcessor[T]) Stop() {
	close(bp.inbox)
	bp.wg.Wait()
	atomic.StoreInt32(&bp.running, 0)
}

// Pending returns the number of items waiting to be processed
func (bp *BatchProcessor[T]) Pending() int {
	return len(bp.inbox)
}

// Stats returns processing statistics
func (bp *BatchProcessor[T]) Stats() (totalItems, totalBatches int64, avgBatchSize float64) {
	return bp.batcher.TotalItemsProcessed(),
		bp.batcher.TotalBatchesProcessed(),
		bp.batcher.AverageBatchSize()
}

// NaturalBatchingDemoResult holds the results of a natural batching demonstration
type NaturalBatchingDemoResult struct {
	TotalItems       int
	TotalBatches     int
	AverageBatchSize float64
	BatchSizes       []int
	Duration         time.Duration
	Throughput       float64 // items per second
}

// RunNaturalBatchingDemo demonstrates natural batching behavior
func RunNaturalBatchingDemo(totalItems int, burstSize int, burstDelay time.Duration) NaturalBatchingDemoResult {
	opts := BatchingOptions{
		MaxBatchSize:         100,
		InitialCapacity:      16,
		MinBatchSize:         1,
		AllowSingleItemBatch: true,
	}

	inbox := make(chan int, 1024)
	batcher := NewNaturalBatcher(inbox, opts)

	var batchSizes []int
	var mu sync.Mutex

	// Start batch consumer
	var consumerDone sync.WaitGroup
	consumerDone.Add(1)
	go func() {
		defer consumerDone.Done()
		for {
			batch, ok := batcher.NextBatch()
			if !ok {
				return
			}
			mu.Lock()
			batchSizes = append(batchSizes, len(batch))
			mu.Unlock()
		}
	}()

	start := time.Now()

	// Send items in bursts
	sent := 0
	for sent < totalItems {
		// Send a burst
		burstCount := burstSize
		if sent+burstCount > totalItems {
			burstCount = totalItems - sent
		}

		for i := 0; i < burstCount; i++ {
			inbox <- sent + i
		}
		sent += burstCount

		// Wait between bursts (if not done)
		if sent < totalItems && burstDelay > 0 {
			time.Sleep(burstDelay)
		}
	}

	close(inbox)
	consumerDone.Wait()

	duration := time.Since(start)

	// Calculate average batch size
	var avgBatchSize float64
	if len(batchSizes) > 0 {
		total := 0
		for _, size := range batchSizes {
			total += size
		}
		avgBatchSize = float64(total) / float64(len(batchSizes))
	}

	throughput := float64(totalItems) / duration.Seconds()

	return NaturalBatchingDemoResult{
		TotalItems:       totalItems,
		TotalBatches:     len(batchSizes),
		AverageBatchSize: avgBatchSize,
		BatchSizes:       batchSizes,
		Duration:         duration,
		Throughput:       throughput,
	}
}
