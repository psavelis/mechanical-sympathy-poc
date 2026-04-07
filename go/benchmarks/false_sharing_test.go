package benchmarks

import (
	"fmt"
	"sync"
	"testing"

	"github.com/psavelis/mechanical-sympathy-poc/go/internal/core/cachepadding"
)

// BenchmarkBadCounter benchmarks the counter with false sharing
func BenchmarkBadCounter(b *testing.B) {
	for _, iterations := range []int{10_000_000, 100_000_000} {
		b.Run(fmt.Sprintf("iterations=%d", iterations), func(b *testing.B) {
			for i := 0; i < b.N; i++ {
				counter := cachepadding.NewBadCounter()
				var wg sync.WaitGroup
				wg.Add(2)

				go func() {
					defer wg.Done()
					for j := 0; j < iterations; j++ {
						counter.IncrementCount1()
					}
				}()

				go func() {
					defer wg.Done()
					for j := 0; j < iterations; j++ {
						counter.IncrementCount2()
					}
				}()

				wg.Wait()
			}
		})
	}
}

// BenchmarkGoodCounter benchmarks the counter with cache line padding
func BenchmarkGoodCounter(b *testing.B) {
	for _, iterations := range []int{10_000_000, 100_000_000} {
		b.Run(fmt.Sprintf("iterations=%d", iterations), func(b *testing.B) {
			for i := 0; i < b.N; i++ {
				counter := cachepadding.NewGoodCounter()
				var wg sync.WaitGroup
				wg.Add(2)

				go func() {
					defer wg.Done()
					for j := 0; j < iterations; j++ {
						counter.IncrementCount1()
					}
				}()

				go func() {
					defer wg.Done()
					for j := 0; j < iterations; j++ {
						counter.IncrementCount2()
					}
				}()

				wg.Wait()
			}
		})
	}
}

// BenchmarkPaddedCounterArray benchmarks the padded counter array
func BenchmarkPaddedCounterArray(b *testing.B) {
	numCounters := 8
	iterationsPerCounter := 1_000_000

	b.Run("PaddedCounterArray", func(b *testing.B) {
		for i := 0; i < b.N; i++ {
			counters := cachepadding.NewPaddedCounterArray(numCounters)
			var wg sync.WaitGroup
			wg.Add(numCounters)

			for c := 0; c < numCounters; c++ {
				go func(idx int) {
					defer wg.Done()
					for j := 0; j < iterationsPerCounter; j++ {
						counters.Increment(idx)
					}
				}(c)
			}

			wg.Wait()
		}
	})
}

// BenchmarkFalseSharingComparison directly compares bad vs good counters
func BenchmarkFalseSharingComparison(b *testing.B) {
	iterations := 10_000_000

	b.Run("BadCounter_FalseSharing", func(b *testing.B) {
		for i := 0; i < b.N; i++ {
			counter := cachepadding.NewBadCounter()
			var wg sync.WaitGroup
			wg.Add(2)

			go func() {
				defer wg.Done()
				for j := 0; j < iterations; j++ {
					counter.IncrementCount1()
				}
			}()

			go func() {
				defer wg.Done()
				for j := 0; j < iterations; j++ {
					counter.IncrementCount2()
				}
			}()

			wg.Wait()
		}
	})

	b.Run("GoodCounter_CachePadded", func(b *testing.B) {
		for i := 0; i < b.N; i++ {
			counter := cachepadding.NewGoodCounter()
			var wg sync.WaitGroup
			wg.Add(2)

			go func() {
				defer wg.Done()
				for j := 0; j < iterations; j++ {
					counter.IncrementCount1()
				}
			}()

			go func() {
				defer wg.Done()
				for j := 0; j < iterations; j++ {
					counter.IncrementCount2()
				}
			}()

			wg.Wait()
		}
	})
}
