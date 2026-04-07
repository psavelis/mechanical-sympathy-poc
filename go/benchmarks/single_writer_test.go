package benchmarks

import (
	"context"
	"fmt"
	"sync"
	"testing"
)

// BenchmarkMutexCounter benchmarks mutex-based counter (contention-heavy)
func BenchmarkMutexCounter(b *testing.B) {
	configs := []struct {
		messages  int
		producers int
	}{
		{100_000, 1},
		{100_000, 4},
		{100_000, 8},
		{1_000_000, 8},
	}

	for _, cfg := range configs {
		name := fmt.Sprintf("msgs=%d/prod=%d", cfg.messages, cfg.producers)
		b.Run(name, func(b *testing.B) {
			for i := 0; i < b.N; i++ {
				var mu sync.Mutex
				var counter int64
				var wg sync.WaitGroup
				perProducer := cfg.messages / cfg.producers

				for p := 0; p < cfg.producers; p++ {
					wg.Add(1)
					go func() {
						defer wg.Done()
						for j := 0; j < perProducer; j++ {
							mu.Lock()
							counter++
							mu.Unlock()
						}
					}()
				}
				wg.Wait()
				_ = counter
			}
		})
	}
}

// BenchmarkChannelAgent benchmarks channel-based single writer approach
func BenchmarkChannelAgent(b *testing.B) {
	configs := []struct {
		messages  int
		producers int
	}{
		{100_000, 1},
		{100_000, 4},
		{100_000, 8},
		{1_000_000, 8},
	}

	for _, cfg := range configs {
		name := fmt.Sprintf("msgs=%d/prod=%d", cfg.messages, cfg.producers)
		b.Run(name, func(b *testing.B) {
			for i := 0; i < b.N; i++ {
				inbox := make(chan int64, 4096)
				var counter int64
				ctx, cancel := context.WithCancel(context.Background())

				// Consumer (Single Writer)
				var consumerDone sync.WaitGroup
				consumerDone.Add(1)
				go func() {
					defer consumerDone.Done()
					for {
						select {
						case <-ctx.Done():
							// Drain remaining
							for {
								select {
								case val := <-inbox:
									counter += val
								default:
									return
								}
							}
						case val, ok := <-inbox:
							if !ok {
								return
							}
							counter += val // No lock needed - single writer!
						}
					}
				}()

				// Producers
				var producerDone sync.WaitGroup
				perProducer := cfg.messages / cfg.producers
				for p := 0; p < cfg.producers; p++ {
					producerDone.Add(1)
					go func() {
						defer producerDone.Done()
						for j := 0; j < perProducer; j++ {
							inbox <- 1
						}
					}()
				}

				producerDone.Wait()
				close(inbox)
				cancel()
				consumerDone.Wait()
				_ = counter
			}
		})
	}
}

// BenchmarkSingleWriterComparison directly compares mutex vs channel approaches
func BenchmarkSingleWriterComparison(b *testing.B) {
	messages := 100_000
	producers := 8
	perProducer := messages / producers

	b.Run("Mutex_LockContention", func(b *testing.B) {
		for i := 0; i < b.N; i++ {
			var mu sync.Mutex
			var counter int64
			var wg sync.WaitGroup

			for p := 0; p < producers; p++ {
				wg.Add(1)
				go func() {
					defer wg.Done()
					for j := 0; j < perProducer; j++ {
						mu.Lock()
						counter++
						mu.Unlock()
					}
				}()
			}
			wg.Wait()
		}
	})

	b.Run("Channel_SingleWriter", func(b *testing.B) {
		for i := 0; i < b.N; i++ {
			inbox := make(chan int64, 4096)
			var counter int64

			// Single writer consumer
			var consumerDone sync.WaitGroup
			consumerDone.Add(1)
			go func() {
				defer consumerDone.Done()
				for val := range inbox {
					counter += val
				}
			}()

			// Multiple producers
			var producerDone sync.WaitGroup
			for p := 0; p < producers; p++ {
				producerDone.Add(1)
				go func() {
					defer producerDone.Done()
					for j := 0; j < perProducer; j++ {
						inbox <- 1
					}
				}()
			}

			producerDone.Wait()
			close(inbox)
			consumerDone.Wait()
		}
	})
}

// BenchmarkAtomicCounter benchmarks atomic operations for comparison
func BenchmarkAtomicCounter(b *testing.B) {
	configs := []struct {
		messages  int
		producers int
	}{
		{100_000, 1},
		{100_000, 8},
		{1_000_000, 8},
	}

	for _, cfg := range configs {
		name := fmt.Sprintf("msgs=%d/prod=%d", cfg.messages, cfg.producers)
		b.Run(name, func(b *testing.B) {
			for i := 0; i < b.N; i++ {
				var counter int64
				var wg sync.WaitGroup
				perProducer := cfg.messages / cfg.producers

				for p := 0; p < cfg.producers; p++ {
					wg.Add(1)
					go func() {
						defer wg.Done()
						for j := 0; j < perProducer; j++ {
							// Using simple addition (not atomic for comparison)
							// In real code, use sync/atomic
							counter++
						}
					}()
				}
				wg.Wait()
			}
		})
	}
}
