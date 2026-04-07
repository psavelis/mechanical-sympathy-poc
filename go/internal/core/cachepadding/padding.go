// Package cachepadding implements false sharing prevention through cache line padding.
//
// False sharing occurs when two or more threads write to different variables
// that happen to reside in the same CPU cache line (typically 64 bytes).
// This causes the cache line to bounce between CPU cores, severely degrading
// performance as each core invalidates the others' cached copies.
//
// The solution is to pad variables so each occupies its own cache line,
// eliminating the false sharing problem.
//
// Reference: https://martinfowler.com/articles/mechanical-sympathy-principles.html
package cachepadding

import (
	"sync"
	"sync/atomic"
	"time"
)

// CacheLineSize is the standard cache line size on modern x86/x64 and ARM64 processors
const CacheLineSize = 64

// BadCounter demonstrates FALSE SHARING.
// Both Count1 and Count2 are in the same cache line (within 64 bytes).
// When two threads increment these counters concurrently, they'll
// continuously invalidate each other's cache lines, causing severe
// performance degradation.
type BadCounter struct {
	Count1 int64 // offset 0-7
	Count2 int64 // offset 8-15 - SAME CACHE LINE!
}

// NewBadCounter creates a new BadCounter
func NewBadCounter() *BadCounter {
	return &BadCounter{}
}

// IncrementCount1 atomically increments Count1
func (c *BadCounter) IncrementCount1() {
	atomic.AddInt64(&c.Count1, 1)
}

// IncrementCount2 atomically increments Count2
func (c *BadCounter) IncrementCount2() {
	atomic.AddInt64(&c.Count2, 1)
}

// GetCount1 returns the current value of Count1
func (c *BadCounter) GetCount1() int64 {
	return atomic.LoadInt64(&c.Count1)
}

// GetCount2 returns the current value of Count2
func (c *BadCounter) GetCount2() int64 {
	return atomic.LoadInt64(&c.Count2)
}

// Reset resets both counters to zero
func (c *BadCounter) Reset() {
	atomic.StoreInt64(&c.Count1, 0)
	atomic.StoreInt64(&c.Count2, 0)
}

// GoodCounter prevents false sharing by padding each counter to its own cache line.
// The padding ensures Count1 and Count2 are on separate cache lines,
// eliminating cache line contention between threads.
type GoodCounter struct {
	Count1 int64
	_pad1  [CacheLineSize - 8]byte // Pad to fill the 64-byte cache line
	Count2 int64
	_pad2  [CacheLineSize - 8]byte // Pad to fill the 64-byte cache line
}

// NewGoodCounter creates a new GoodCounter
func NewGoodCounter() *GoodCounter {
	return &GoodCounter{}
}

// IncrementCount1 atomically increments Count1
func (c *GoodCounter) IncrementCount1() {
	atomic.AddInt64(&c.Count1, 1)
}

// IncrementCount2 atomically increments Count2
func (c *GoodCounter) IncrementCount2() {
	atomic.AddInt64(&c.Count2, 1)
}

// GetCount1 returns the current value of Count1
func (c *GoodCounter) GetCount1() int64 {
	return atomic.LoadInt64(&c.Count1)
}

// GetCount2 returns the current value of Count2
func (c *GoodCounter) GetCount2() int64 {
	return atomic.LoadInt64(&c.Count2)
}

// Reset resets both counters to zero
func (c *GoodCounter) Reset() {
	atomic.StoreInt64(&c.Count1, 0)
	atomic.StoreInt64(&c.Count2, 0)
}

// PaddedCounter is a single counter with cache line padding.
// Useful when you need an array of independent counters.
type PaddedCounter struct {
	Value int64
	_pad  [CacheLineSize - 8]byte // Pad to 64 bytes
}

// Increment atomically increments the counter
func (c *PaddedCounter) Increment() int64 {
	return atomic.AddInt64(&c.Value, 1)
}

// Add atomically adds a value to the counter
func (c *PaddedCounter) Add(delta int64) int64 {
	return atomic.AddInt64(&c.Value, delta)
}

// Load atomically loads the counter value
func (c *PaddedCounter) Load() int64 {
	return atomic.LoadInt64(&c.Value)
}

// Store atomically stores a value
func (c *PaddedCounter) Store(val int64) {
	atomic.StoreInt64(&c.Value, val)
}

// PaddedCounterArray is an array of cache-line-padded counters.
// Each counter is on its own cache line, eliminating false sharing
// when different goroutines access different counters.
type PaddedCounterArray struct {
	counters []PaddedCounter
}

// NewPaddedCounterArray creates a new array of padded counters
func NewPaddedCounterArray(count int) *PaddedCounterArray {
	return &PaddedCounterArray{
		counters: make([]PaddedCounter, count),
	}
}

// Increment atomically increments the counter at the given index
func (a *PaddedCounterArray) Increment(index int) int64 {
	return a.counters[index].Increment()
}

// Add atomically adds a value to the counter at the given index
func (a *PaddedCounterArray) Add(index int, delta int64) int64 {
	return a.counters[index].Add(delta)
}

// Load atomically loads the counter value at the given index
func (a *PaddedCounterArray) Load(index int) int64 {
	return a.counters[index].Load()
}

// Store atomically stores a value at the given index
func (a *PaddedCounterArray) Store(index int, val int64) {
	a.counters[index].Store(val)
}

// Len returns the number of counters in the array
func (a *PaddedCounterArray) Len() int {
	return len(a.counters)
}

// Sum returns the sum of all counters
func (a *PaddedCounterArray) Sum() int64 {
	var sum int64
	for i := range a.counters {
		sum += a.counters[i].Load()
	}
	return sum
}

// Reset resets all counters to zero
func (a *PaddedCounterArray) Reset() {
	for i := range a.counters {
		a.counters[i].Store(0)
	}
}

// FalseSharingDemoResult holds the results of a false sharing demonstration
type FalseSharingDemoResult struct {
	BadDuration  time.Duration
	GoodDuration time.Duration
	Speedup      float64
	Iterations   int64
}

// RunFalseSharingDemo demonstrates the performance difference between
// counters with and without cache line padding.
func RunFalseSharingDemo(iterations int64) FalseSharingDemoResult {
	// Test BadCounter (with false sharing)
	badCounter := NewBadCounter()
	badStart := time.Now()

	var wg sync.WaitGroup
	wg.Add(2)

	go func() {
		defer wg.Done()
		for i := int64(0); i < iterations; i++ {
			badCounter.IncrementCount1()
		}
	}()

	go func() {
		defer wg.Done()
		for i := int64(0); i < iterations; i++ {
			badCounter.IncrementCount2()
		}
	}()

	wg.Wait()
	badDuration := time.Since(badStart)

	// Test GoodCounter (without false sharing)
	goodCounter := NewGoodCounter()
	goodStart := time.Now()

	wg.Add(2)

	go func() {
		defer wg.Done()
		for i := int64(0); i < iterations; i++ {
			goodCounter.IncrementCount1()
		}
	}()

	go func() {
		defer wg.Done()
		for i := int64(0); i < iterations; i++ {
			goodCounter.IncrementCount2()
		}
	}()

	wg.Wait()
	goodDuration := time.Since(goodStart)

	speedup := float64(badDuration) / float64(goodDuration)

	return FalseSharingDemoResult{
		BadDuration:  badDuration,
		GoodDuration: goodDuration,
		Speedup:      speedup,
		Iterations:   iterations,
	}
}
