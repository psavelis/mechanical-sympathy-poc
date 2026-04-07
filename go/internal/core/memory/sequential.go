// Package memory implements Sequential Memory Access patterns for cache efficiency.
//
// Sequential memory access leverages CPU prefetching by accessing data in a
// predictable, linear pattern. Modern CPUs detect sequential access patterns
// and prefetch upcoming cache lines, dramatically improving performance.
//
// Key insights:
// - Arrays/slices provide contiguous memory layout
// - Index-based iteration (for i := 0; i < len; i++) is cache-friendly
// - Random access patterns (maps, linked lists) cause cache misses
//
// Reference: https://martinfowler.com/articles/mechanical-sympathy-principles.html
package memory

import (
	"sync"
	"time"

	"github.com/psavelis/mechanical-sympathy-poc/go/internal/domain"
)

// SequentialOrderBuffer provides cache-friendly storage for orders.
// Uses a contiguous slice for sequential iteration patterns.
type SequentialOrderBuffer struct {
	data     []domain.Order
	capacity int
}

// NewSequentialOrderBuffer creates a new buffer with the given capacity
func NewSequentialOrderBuffer(capacity int) *SequentialOrderBuffer {
	return &SequentialOrderBuffer{
		data:     make([]domain.Order, 0, capacity),
		capacity: capacity,
	}
}

// Add adds an order to the buffer
func (b *SequentialOrderBuffer) Add(order domain.Order) {
	b.data = append(b.data, order)
}

// AddPtr adds an order from a pointer
func (b *SequentialOrderBuffer) AddPtr(order *domain.Order) {
	b.data = append(b.data, *order)
}

// Len returns the number of orders in the buffer
func (b *SequentialOrderBuffer) Len() int {
	return len(b.data)
}

// Cap returns the buffer capacity
func (b *SequentialOrderBuffer) Cap() int {
	return b.capacity
}

// Get returns the order at the given index
func (b *SequentialOrderBuffer) Get(index int) *domain.Order {
	if index < 0 || index >= len(b.data) {
		return nil
	}
	return &b.data[index]
}

// SumQuantities calculates the sum of all order quantities.
// Uses sequential iteration for cache efficiency.
func (b *SequentialOrderBuffer) SumQuantities() float64 {
	var sum float64
	// Sequential access pattern - CPU prefetcher can predict next access
	for i := 0; i < len(b.data); i++ {
		sum += b.data[i].Quantity
	}
	return sum
}

// SumPrices calculates the sum of all order prices.
func (b *SequentialOrderBuffer) SumPrices() float64 {
	var sum float64
	for i := 0; i < len(b.data); i++ {
		sum += b.data[i].Price
	}
	return sum
}

// SumValues calculates the sum of all order values (price * quantity).
func (b *SequentialOrderBuffer) SumValues() float64 {
	var sum float64
	for i := 0; i < len(b.data); i++ {
		sum += b.data[i].Price * b.data[i].Quantity
	}
	return sum
}

// ForEach iterates over all orders sequentially
func (b *SequentialOrderBuffer) ForEach(fn func(*domain.Order)) {
	for i := range b.data {
		fn(&b.data[i])
	}
}

// Filter returns a new buffer with orders matching the predicate
func (b *SequentialOrderBuffer) Filter(predicate func(*domain.Order) bool) *SequentialOrderBuffer {
	result := NewSequentialOrderBuffer(b.capacity / 2)
	for i := range b.data {
		if predicate(&b.data[i]) {
			result.Add(b.data[i])
		}
	}
	return result
}

// Clear removes all orders from the buffer
func (b *SequentialOrderBuffer) Clear() {
	b.data = b.data[:0]
}

// Data returns the underlying slice (for iteration)
func (b *SequentialOrderBuffer) Data() []domain.Order {
	return b.data
}

// LinkedNode represents a node in a linked list.
// Used for comparison with sequential access patterns.
// Linked list traversal causes cache misses due to pointer chasing.
type LinkedNode struct {
	Value int64
	Next  *LinkedNode
}

// CreateLinkedList creates a linked list of the given size
func CreateLinkedList(size int) *LinkedNode {
	if size == 0 {
		return nil
	}

	head := &LinkedNode{Value: 0}
	current := head

	for i := 1; i < size; i++ {
		current.Next = &LinkedNode{Value: int64(i)}
		current = current.Next
	}

	return head
}

// SumLinkedList calculates the sum of all values in the linked list.
// This demonstrates cache-unfriendly access patterns due to pointer chasing.
func SumLinkedList(head *LinkedNode) int64 {
	var sum int64
	for current := head; current != nil; current = current.Next {
		sum += current.Value // Each access potentially causes a cache miss
	}
	return sum
}

// LenLinkedList returns the length of the linked list
func LenLinkedList(head *LinkedNode) int {
	count := 0
	for current := head; current != nil; current = current.Next {
		count++
	}
	return count
}

// SequentialAccessDemoResult holds the results of a sequential access demonstration
type SequentialAccessDemoResult struct {
	SequentialDuration time.Duration
	RandomDuration     time.Duration
	Speedup            float64
	Size               int
	Iterations         int
}

// RunSequentialAccessDemo demonstrates the performance difference between
// sequential and random memory access patterns.
func RunSequentialAccessDemo(size int, iterations int) SequentialAccessDemoResult {
	// Create test data
	data := make([]int64, size)
	for i := range data {
		data[i] = int64(i)
	}

	// Create random indices (deterministic for reproducibility)
	randomIndices := make([]int, size)
	for i := range randomIndices {
		randomIndices[i] = (i*31 + 17) % size
	}

	// Test sequential access
	sequentialStart := time.Now()
	for iter := 0; iter < iterations; iter++ {
		var sum int64
		for i := 0; i < len(data); i++ {
			sum += data[i]
		}
		_ = sum
	}
	sequentialDuration := time.Since(sequentialStart)

	// Test random access
	randomStart := time.Now()
	for iter := 0; iter < iterations; iter++ {
		var sum int64
		for _, idx := range randomIndices {
			sum += data[idx]
		}
		_ = sum
	}
	randomDuration := time.Since(randomStart)

	speedup := float64(randomDuration) / float64(sequentialDuration)

	return SequentialAccessDemoResult{
		SequentialDuration: sequentialDuration,
		RandomDuration:     randomDuration,
		Speedup:            speedup,
		Size:               size,
		Iterations:         iterations,
	}
}

// ArrayVsLinkedListResult holds the results of array vs linked list comparison
type ArrayVsLinkedListResult struct {
	ArrayDuration      time.Duration
	LinkedListDuration time.Duration
	Speedup            float64
	Size               int
	Iterations         int
}

// RunArrayVsLinkedListDemo demonstrates the performance difference between
// contiguous arrays and linked lists.
func RunArrayVsLinkedListDemo(size int, iterations int) ArrayVsLinkedListResult {
	// Create array
	array := make([]int64, size)
	for i := range array {
		array[i] = int64(i)
	}

	// Create linked list
	linkedList := CreateLinkedList(size)

	// Test array access
	arrayStart := time.Now()
	for iter := 0; iter < iterations; iter++ {
		var sum int64
		for i := 0; i < len(array); i++ {
			sum += array[i]
		}
		_ = sum
	}
	arrayDuration := time.Since(arrayStart)

	// Test linked list access
	linkedStart := time.Now()
	for iter := 0; iter < iterations; iter++ {
		_ = SumLinkedList(linkedList)
	}
	linkedListDuration := time.Since(linkedStart)

	speedup := float64(linkedListDuration) / float64(arrayDuration)

	return ArrayVsLinkedListResult{
		ArrayDuration:      arrayDuration,
		LinkedListDuration: linkedListDuration,
		Speedup:            speedup,
		Size:               size,
		Iterations:         iterations,
	}
}

// OrderMapAccess demonstrates random access patterns using a map
type OrderMapAccess struct {
	orders map[int64]*domain.Order
}

// NewOrderMapAccess creates a new order map
func NewOrderMapAccess() *OrderMapAccess {
	return &OrderMapAccess{
		orders: make(map[int64]*domain.Order),
	}
}

// Add adds an order to the map
func (m *OrderMapAccess) Add(order *domain.Order) {
	m.orders[order.ID] = order
}

// Get retrieves an order by ID
func (m *OrderMapAccess) Get(id int64) *domain.Order {
	return m.orders[id]
}

// Len returns the number of orders
func (m *OrderMapAccess) Len() int {
	return len(m.orders)
}

// SumQuantities calculates the sum of all quantities.
// Map iteration order is random, causing cache misses.
func (m *OrderMapAccess) SumQuantities() float64 {
	var sum float64
	for _, order := range m.orders {
		sum += order.Quantity
	}
	return sum
}

// MapVsSliceResult holds the results of map vs slice comparison
type MapVsSliceResult struct {
	SliceDuration time.Duration
	MapDuration   time.Duration
	Speedup       float64
	Size          int
	Iterations    int
}

// RunMapVsSliceDemo demonstrates the performance difference between
// sequential slice iteration and map iteration.
func RunMapVsSliceDemo(size int, iterations int) MapVsSliceResult {
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

	// Test slice access
	sliceStart := time.Now()
	for iter := 0; iter < iterations; iter++ {
		var sum float64
		for i := 0; i < len(slice); i++ {
			sum += slice[i].Quantity
		}
		_ = sum
	}
	sliceDuration := time.Since(sliceStart)

	// Test map access
	mapStart := time.Now()
	for iter := 0; iter < iterations; iter++ {
		var sum float64
		for _, order := range orderMap {
			sum += order.Quantity
		}
		_ = sum
	}
	mapDuration := time.Since(mapStart)

	speedup := float64(mapDuration) / float64(sliceDuration)

	return MapVsSliceResult{
		SliceDuration: sliceDuration,
		MapDuration:   mapDuration,
		Speedup:       speedup,
		Size:          size,
		Iterations:    iterations,
	}
}

// PreallocatedBuffer demonstrates the benefit of pre-allocation
type PreallocatedBuffer struct {
	data []int64
	mu   sync.RWMutex
}

// NewPreallocatedBuffer creates a buffer with pre-allocated capacity
func NewPreallocatedBuffer(capacity int) *PreallocatedBuffer {
	return &PreallocatedBuffer{
		data: make([]int64, 0, capacity),
	}
}

// Add appends a value to the buffer
func (b *PreallocatedBuffer) Add(value int64) {
	b.mu.Lock()
	b.data = append(b.data, value)
	b.mu.Unlock()
}

// Sum returns the sum of all values
func (b *PreallocatedBuffer) Sum() int64 {
	b.mu.RLock()
	defer b.mu.RUnlock()

	var sum int64
	for i := 0; i < len(b.data); i++ {
		sum += b.data[i]
	}
	return sum
}

// Len returns the number of items
func (b *PreallocatedBuffer) Len() int {
	b.mu.RLock()
	defer b.mu.RUnlock()
	return len(b.data)
}
