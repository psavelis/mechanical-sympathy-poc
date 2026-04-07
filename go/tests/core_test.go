package tests

import (
	"context"
	"sync"
	"testing"
	"time"

	"github.com/psavelis/mechanical-sympathy-poc/go/internal/core/agents"
	"github.com/psavelis/mechanical-sympathy-poc/go/internal/core/batching"
	"github.com/psavelis/mechanical-sympathy-poc/go/internal/core/cachepadding"
	"github.com/psavelis/mechanical-sympathy-poc/go/internal/core/memory"
	"github.com/psavelis/mechanical-sympathy-poc/go/internal/domain"
)

func TestCachePaddedCounterSize(t *testing.T) {
	// Verify padding
	if cachepadding.CacheLineSize != 64 {
		t.Errorf("Expected cache line size 64, got %d", cachepadding.CacheLineSize)
	}
}

func TestBadCounter(t *testing.T) {
	counter := cachepadding.NewBadCounter()

	var wg sync.WaitGroup
	wg.Add(2)

	iterations := 10000

	go func() {
		defer wg.Done()
		for i := 0; i < iterations; i++ {
			counter.IncrementCount1()
		}
	}()

	go func() {
		defer wg.Done()
		for i := 0; i < iterations; i++ {
			counter.IncrementCount2()
		}
	}()

	wg.Wait()

	if counter.GetCount1() != int64(iterations) {
		t.Errorf("Expected Count1 = %d, got %d", iterations, counter.GetCount1())
	}
	if counter.GetCount2() != int64(iterations) {
		t.Errorf("Expected Count2 = %d, got %d", iterations, counter.GetCount2())
	}
}

func TestGoodCounter(t *testing.T) {
	counter := cachepadding.NewGoodCounter()

	var wg sync.WaitGroup
	wg.Add(2)

	iterations := 10000

	go func() {
		defer wg.Done()
		for i := 0; i < iterations; i++ {
			counter.IncrementCount1()
		}
	}()

	go func() {
		defer wg.Done()
		for i := 0; i < iterations; i++ {
			counter.IncrementCount2()
		}
	}()

	wg.Wait()

	if counter.GetCount1() != int64(iterations) {
		t.Errorf("Expected Count1 = %d, got %d", iterations, counter.GetCount1())
	}
	if counter.GetCount2() != int64(iterations) {
		t.Errorf("Expected Count2 = %d, got %d", iterations, counter.GetCount2())
	}
}

func TestPaddedCounterArray(t *testing.T) {
	numCounters := 4
	counters := cachepadding.NewPaddedCounterArray(numCounters)

	if counters.Len() != numCounters {
		t.Errorf("Expected length %d, got %d", numCounters, counters.Len())
	}

	// Test increment
	for i := 0; i < numCounters; i++ {
		for j := 0; j <= i; j++ {
			counters.Increment(i)
		}
	}

	// Verify counts
	for i := 0; i < numCounters; i++ {
		expected := int64(i + 1)
		if counters.Load(i) != expected {
			t.Errorf("Counter %d: expected %d, got %d", i, expected, counters.Load(i))
		}
	}

	// Test sum
	expectedSum := int64(1 + 2 + 3 + 4) // 0+1, 1+1, 2+1, 3+1
	if counters.Sum() != expectedSum {
		t.Errorf("Expected sum %d, got %d", expectedSum, counters.Sum())
	}
}

func TestStatsAgent(t *testing.T) {
	agent, handle := agents.NewStatsAgent(100)

	ctx, cancel := context.WithCancel(context.Background())

	// Use WaitGroup to wait for agent to complete
	var wg sync.WaitGroup
	wg.Add(1)

	// Start agent
	go func() {
		defer wg.Done()
		agent.Run(ctx)
	}()

	// Send messages
	values := []int64{10, 20, 30, 40, 50}
	for _, v := range values {
		handle.Send(agents.StatsMessage{Value: v})
	}

	// Wait for processing
	time.Sleep(100 * time.Millisecond)

	// Stop agent
	handle.Close()
	cancel()

	// Wait for agent goroutine to finish
	wg.Wait()

	// Check results - now safe to access
	snapshot := agent.Snapshot()

	if snapshot.Count != int64(len(values)) {
		t.Errorf("Expected count %d, got %d", len(values), snapshot.Count)
	}

	expectedTotal := int64(150)
	if snapshot.Total != expectedTotal {
		t.Errorf("Expected total %d, got %d", expectedTotal, snapshot.Total)
	}

	if snapshot.Min != 10 {
		t.Errorf("Expected min 10, got %d", snapshot.Min)
	}

	if snapshot.Max != 50 {
		t.Errorf("Expected max 50, got %d", snapshot.Max)
	}
}

func TestNaturalBatcher(t *testing.T) {
	inbox := make(chan int, 100)
	opts := batching.DefaultOptions()
	opts.MaxBatchSize = 10
	batcher := batching.NewNaturalBatcher(inbox, opts)

	// Send some items
	for i := 0; i < 5; i++ {
		inbox <- i
	}

	// Get first batch
	batch, ok := batcher.NextBatch()
	if !ok {
		t.Fatal("Expected batch to be available")
	}

	// Should have collected all 5 items
	if len(batch) != 5 {
		t.Errorf("Expected batch size 5, got %d", len(batch))
	}

	// Send max batch size items
	for i := 0; i < 15; i++ {
		inbox <- i
	}

	// Should get max batch size
	batch, ok = batcher.NextBatch()
	if !ok {
		t.Fatal("Expected batch to be available")
	}

	if len(batch) > opts.MaxBatchSize {
		t.Errorf("Batch size %d exceeds max %d", len(batch), opts.MaxBatchSize)
	}

	close(inbox)

	// Drain remaining
	for {
		batch, ok := batcher.NextBatch()
		if !ok {
			break
		}
		_ = batch
	}
}

func TestBatchProcessor(t *testing.T) {
	var processed []int
	var mu sync.Mutex

	processor := batching.NewBatchProcessor(100, batching.DefaultOptions(), func(batch []int) {
		mu.Lock()
		processed = append(processed, batch...)
		mu.Unlock()
	})

	processor.Start()

	// Submit items
	for i := 0; i < 50; i++ {
		processor.Submit(i)
	}

	// Wait for processing
	time.Sleep(100 * time.Millisecond)

	processor.Stop()

	mu.Lock()
	count := len(processed)
	mu.Unlock()

	if count != 50 {
		t.Errorf("Expected 50 processed items, got %d", count)
	}
}

func TestSequentialOrderBuffer(t *testing.T) {
	buffer := memory.NewSequentialOrderBuffer(100)

	if buffer.Len() != 0 {
		t.Errorf("New buffer should be empty, got length %d", buffer.Len())
	}

	// Add orders
	for i := 0; i < 10; i++ {
		buffer.Add(domain.Order{
			ID:       int64(i),
			Price:    100.0 + float64(i),
			Quantity: float64(i + 1),
		})
	}

	if buffer.Len() != 10 {
		t.Errorf("Expected 10 orders, got %d", buffer.Len())
	}

	// Test SumQuantities
	expectedQty := float64(1 + 2 + 3 + 4 + 5 + 6 + 7 + 8 + 9 + 10) // 55
	if buffer.SumQuantities() != expectedQty {
		t.Errorf("Expected sum quantities %f, got %f", expectedQty, buffer.SumQuantities())
	}

	// Test SumPrices
	expectedPrices := float64(100 + 101 + 102 + 103 + 104 + 105 + 106 + 107 + 108 + 109) // 1045
	if buffer.SumPrices() != expectedPrices {
		t.Errorf("Expected sum prices %f, got %f", expectedPrices, buffer.SumPrices())
	}

	// Test Clear
	buffer.Clear()
	if buffer.Len() != 0 {
		t.Errorf("Cleared buffer should be empty, got length %d", buffer.Len())
	}
}

func TestLinkedList(t *testing.T) {
	list := memory.CreateLinkedList(5)

	if list == nil {
		t.Fatal("Expected non-nil linked list")
	}

	// Check length
	length := memory.LenLinkedList(list)
	if length != 5 {
		t.Errorf("Expected length 5, got %d", length)
	}

	// Check sum
	expectedSum := int64(0 + 1 + 2 + 3 + 4) // 10
	sum := memory.SumLinkedList(list)
	if sum != expectedSum {
		t.Errorf("Expected sum %d, got %d", expectedSum, sum)
	}

	// Test empty list
	emptyList := memory.CreateLinkedList(0)
	if emptyList != nil {
		t.Error("Expected nil for empty linked list")
	}

	emptySum := memory.SumLinkedList(nil)
	if emptySum != 0 {
		t.Errorf("Expected sum 0 for nil list, got %d", emptySum)
	}
}

func TestAgentHandle(t *testing.T) {
	agent, handle := agents.NewStatsAgent(10)

	ctx, cancel := context.WithCancel(context.Background())
	defer cancel()

	go agent.Run(ctx)

	// Test Send
	handle.Send(agents.StatsMessage{Value: 1})

	// Test TrySend
	for i := 0; i < 20; i++ {
		handle.TrySend(agents.StatsMessage{Value: int64(i)})
	}

	// Test Pending
	pending := handle.Pending()
	if pending < 0 {
		t.Errorf("Pending should be >= 0, got %d", pending)
	}

	handle.Close()
}

func TestRunFalseSharingDemo(t *testing.T) {
	result := cachepadding.RunFalseSharingDemo(100000)

	if result.Iterations != 100000 {
		t.Errorf("Expected 100000 iterations, got %d", result.Iterations)
	}

	// Both durations should be positive
	if result.BadDuration <= 0 {
		t.Error("BadDuration should be positive")
	}
	if result.GoodDuration <= 0 {
		t.Error("GoodDuration should be positive")
	}
}

func TestRunSingleWriterDemo(t *testing.T) {
	result := agents.RunSingleWriterDemo(10000, 4)

	if result.Messages != 10000 {
		t.Errorf("Expected 10000 messages, got %d", result.Messages)
	}
	if result.Producers != 4 {
		t.Errorf("Expected 4 producers, got %d", result.Producers)
	}

	// Both durations should be positive
	if result.MutexDuration <= 0 {
		t.Error("MutexDuration should be positive")
	}
	if result.ChannelDuration <= 0 {
		t.Error("ChannelDuration should be positive")
	}
}

func TestRunSequentialAccessDemo(t *testing.T) {
	result := memory.RunSequentialAccessDemo(1000, 10)

	if result.Size != 1000 {
		t.Errorf("Expected size 1000, got %d", result.Size)
	}
	if result.Iterations != 10 {
		t.Errorf("Expected 10 iterations, got %d", result.Iterations)
	}

	// Both durations should be positive
	if result.SequentialDuration <= 0 {
		t.Error("SequentialDuration should be positive")
	}
	if result.RandomDuration <= 0 {
		t.Error("RandomDuration should be positive")
	}
}
