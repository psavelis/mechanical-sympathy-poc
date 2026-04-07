package benchmarks

import (
	"fmt"
	"testing"

	"github.com/psavelis/mechanical-sympathy-poc/go/internal/core/memory"
	"github.com/psavelis/mechanical-sympathy-poc/go/internal/domain"
)

// BenchmarkSequentialAccess benchmarks sequential vs random array access
func BenchmarkSequentialAccess(b *testing.B) {
	for _, size := range []int{1_000, 10_000, 100_000} {
		// Prepare data
		data := make([]int64, size)
		for i := range data {
			data[i] = int64(i)
		}

		// Random indices (deterministic)
		randomIndices := make([]int, size)
		for i := range randomIndices {
			randomIndices[i] = (i*31 + 17) % size
		}

		b.Run(fmt.Sprintf("Sequential/size=%d", size), func(b *testing.B) {
			for i := 0; i < b.N; i++ {
				var sum int64
				for j := 0; j < len(data); j++ {
					sum += data[j]
				}
				_ = sum
			}
		})

		b.Run(fmt.Sprintf("Random/size=%d", size), func(b *testing.B) {
			for i := 0; i < b.N; i++ {
				var sum int64
				for _, idx := range randomIndices {
					sum += data[idx]
				}
				_ = sum
			}
		})
	}
}

// BenchmarkArrayVsLinkedList benchmarks array vs linked list traversal
func BenchmarkArrayVsLinkedList(b *testing.B) {
	for _, size := range []int{1_000, 10_000, 100_000} {
		// Create array
		array := make([]int64, size)
		for i := range array {
			array[i] = int64(i)
		}

		// Create linked list
		list := memory.CreateLinkedList(size)

		b.Run(fmt.Sprintf("Array/size=%d", size), func(b *testing.B) {
			for i := 0; i < b.N; i++ {
				var sum int64
				for j := 0; j < len(array); j++ {
					sum += array[j]
				}
				_ = sum
			}
		})

		b.Run(fmt.Sprintf("LinkedList/size=%d", size), func(b *testing.B) {
			for i := 0; i < b.N; i++ {
				_ = memory.SumLinkedList(list)
			}
		})
	}
}

// BenchmarkSliceVsMap benchmarks slice iteration vs map iteration
func BenchmarkSliceVsMap(b *testing.B) {
	for _, size := range []int{1_000, 10_000, 100_000} {
		// Create slice of orders
		slice := make([]domain.Order, size)
		for i := range slice {
			slice[i] = domain.Order{
				ID:       int64(i),
				Price:    100.0,
				Quantity: float64(i),
			}
		}

		// Create map of orders
		orderMap := make(map[int64]*domain.Order, size)
		for i := range slice {
			orderMap[int64(i)] = &slice[i]
		}

		b.Run(fmt.Sprintf("Slice/size=%d", size), func(b *testing.B) {
			for i := 0; i < b.N; i++ {
				var sum float64
				for j := 0; j < len(slice); j++ {
					sum += slice[j].Quantity
				}
				_ = sum
			}
		})

		b.Run(fmt.Sprintf("Map/size=%d", size), func(b *testing.B) {
			for i := 0; i < b.N; i++ {
				var sum float64
				for _, order := range orderMap {
					sum += order.Quantity
				}
				_ = sum
			}
		})
	}
}

// BenchmarkSequentialOrderBuffer benchmarks the SequentialOrderBuffer
func BenchmarkSequentialOrderBuffer(b *testing.B) {
	for _, size := range []int{1_000, 10_000, 100_000} {
		// Create and populate buffer
		buffer := memory.NewSequentialOrderBuffer(size)
		for i := 0; i < size; i++ {
			buffer.Add(domain.Order{
				ID:       int64(i),
				Price:    100.0 + float64(i),
				Quantity: float64(i),
			})
		}

		b.Run(fmt.Sprintf("SumQuantities/size=%d", size), func(b *testing.B) {
			for i := 0; i < b.N; i++ {
				_ = buffer.SumQuantities()
			}
		})

		b.Run(fmt.Sprintf("SumPrices/size=%d", size), func(b *testing.B) {
			for i := 0; i < b.N; i++ {
				_ = buffer.SumPrices()
			}
		})

		b.Run(fmt.Sprintf("SumValues/size=%d", size), func(b *testing.B) {
			for i := 0; i < b.N; i++ {
				_ = buffer.SumValues()
			}
		})
	}
}

// BenchmarkMemoryAccessComparison comprehensive comparison
func BenchmarkMemoryAccessComparison(b *testing.B) {
	size := 100_000

	// Prepare data structures
	array := make([]int64, size)
	for i := range array {
		array[i] = int64(i)
	}

	randomIndices := make([]int, size)
	for i := range randomIndices {
		randomIndices[i] = (i*31 + 17) % size
	}

	linkedList := memory.CreateLinkedList(size)

	orderSlice := make([]domain.Order, size)
	for i := range orderSlice {
		orderSlice[i] = domain.Order{ID: int64(i), Quantity: float64(i)}
	}

	orderMap := make(map[int64]*domain.Order, size)
	for i := range orderSlice {
		orderMap[int64(i)] = &orderSlice[i]
	}

	b.Run("Array_Sequential", func(b *testing.B) {
		for i := 0; i < b.N; i++ {
			var sum int64
			for j := 0; j < len(array); j++ {
				sum += array[j]
			}
			_ = sum
		}
	})

	b.Run("Array_Random", func(b *testing.B) {
		for i := 0; i < b.N; i++ {
			var sum int64
			for _, idx := range randomIndices {
				sum += array[idx]
			}
			_ = sum
		}
	})

	b.Run("LinkedList_Traversal", func(b *testing.B) {
		for i := 0; i < b.N; i++ {
			_ = memory.SumLinkedList(linkedList)
		}
	})

	b.Run("Slice_Sequential", func(b *testing.B) {
		for i := 0; i < b.N; i++ {
			var sum float64
			for j := 0; j < len(orderSlice); j++ {
				sum += orderSlice[j].Quantity
			}
			_ = sum
		}
	})

	b.Run("Map_Iteration", func(b *testing.B) {
		for i := 0; i < b.N; i++ {
			var sum float64
			for _, order := range orderMap {
				sum += order.Quantity
			}
			_ = sum
		}
	})
}
