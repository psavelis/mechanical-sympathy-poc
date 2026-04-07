// CLI demo application for Mechanical Sympathy principles in Go.
//
// This application demonstrates four key mechanical sympathy principles:
// 1. Cache Line Padding (False Sharing Prevention)
// 2. Single Writer Principle
// 3. Natural Batching
// 4. Sequential Memory Access
//
// Reference: https://martinfowler.com/articles/mechanical-sympathy-principles.html
package main

import (
	"flag"
	"fmt"
	"os"
	"runtime"
	"strings"
	"time"

	"github.com/psavelis/mechanical-sympathy-poc/go/internal/core/agents"
	"github.com/psavelis/mechanical-sympathy-poc/go/internal/core/batching"
	"github.com/psavelis/mechanical-sympathy-poc/go/internal/core/cachepadding"
	"github.com/psavelis/mechanical-sympathy-poc/go/internal/core/memory"
)

func main() {
	demo := flag.String("demo", "all", "Demo to run: all, false-sharing, single-writer, batching, sequential")
	iterations := flag.Int64("iterations", 10_000_000, "Number of iterations for false-sharing demo")
	flag.Parse()

	printHeader()

	switch strings.ToLower(*demo) {
	case "all":
		runFalseSharingDemo(*iterations)
		fmt.Println()
		runSingleWriterDemo()
		fmt.Println()
		runNaturalBatchingDemo()
		fmt.Println()
		runSequentialAccessDemo()
	case "false-sharing":
		runFalseSharingDemo(*iterations)
	case "single-writer":
		runSingleWriterDemo()
	case "batching":
		runNaturalBatchingDemo()
	case "sequential":
		runSequentialAccessDemo()
	default:
		fmt.Fprintf(os.Stderr, "Unknown demo: %s\n", *demo)
		fmt.Println("Available demos: all, false-sharing, single-writer, batching, sequential")
		os.Exit(1)
	}

	fmt.Println()
	fmt.Println(strings.Repeat("═", 72))
	fmt.Println("  All demos completed!")
	fmt.Println(strings.Repeat("═", 72))
}

func printHeader() {
	fmt.Println("╔══════════════════════════════════════════════════════════════════════╗")
	fmt.Println("║     Mechanical Sympathy Principles - Go Implementation               ║")
	fmt.Println("║     Based on Martin Fowler's article                                 ║")
	fmt.Println("╠══════════════════════════════════════════════════════════════════════╣")
	fmt.Printf("║  Available parallelism:     %-4d cores                               ║\n", runtime.NumCPU())
	fmt.Printf("║  GOMAXPROCS:                %-4d                                     ║\n", runtime.GOMAXPROCS(0))
	fmt.Println("║  Cache line size:           64 bytes                                 ║")
	fmt.Printf("║  Go version:                %-10s                              ║\n", runtime.Version())
	fmt.Println("╚══════════════════════════════════════════════════════════════════════╝")
	fmt.Println()
}

func runFalseSharingDemo(iterations int64) {
	printDemoHeader("1. False Sharing Prevention (Cache Line Padding)")

	fmt.Println("False sharing occurs when two threads write to different variables")
	fmt.Println("that share the same CPU cache line (64 bytes). This causes the cache")
	fmt.Println("line to bounce between cores, severely degrading performance.")
	fmt.Println()
	fmt.Printf("Running %s iterations per thread...\n", formatNumber(iterations))
	fmt.Println()

	result := cachepadding.RunFalseSharingDemo(iterations)

	fmt.Println("Results:")
	fmt.Println(strings.Repeat("-", 50))
	fmt.Printf("  BadCounter  (false sharing):     %10s\n", formatDuration(result.BadDuration))
	fmt.Printf("  GoodCounter (cache padded):      %10s\n", formatDuration(result.GoodDuration))
	fmt.Println(strings.Repeat("-", 50))
	fmt.Printf("  Speedup:                           %6.2fx\n", result.Speedup)
	fmt.Println()

	if result.Speedup > 1.5 {
		fmt.Println("✓ Cache padding eliminated false sharing contention!")
	} else {
		fmt.Println("Note: On some architectures (e.g., ARM64), the JIT may already optimize this.")
	}
}

func runSingleWriterDemo() {
	printDemoHeader("2. Single Writer Principle")

	fmt.Println("The Single Writer Principle ensures all writes to shared state")
	fmt.Println("occur through a single goroutine (the 'agent'). Other goroutines")
	fmt.Println("communicate via channels, eliminating lock contention.")
	fmt.Println()

	// Test configurations
	configs := []struct {
		messages  int
		producers int
	}{
		{100_000, 1},
		{100_000, 8},
		{1_000_000, 8},
	}

	fmt.Println("Results:")
	fmt.Println(strings.Repeat("-", 70))
	fmt.Printf("  %-12s %-10s %-12s %-12s %-10s\n", "Messages", "Producers", "Mutex", "Channel", "Speedup")
	fmt.Println(strings.Repeat("-", 70))

	for _, cfg := range configs {
		result := agents.RunSingleWriterDemo(cfg.messages, cfg.producers)
		fmt.Printf("  %-12s %-10d %-12s %-12s %6.2fx\n",
			formatNumber(int64(cfg.messages)),
			cfg.producers,
			formatDuration(result.MutexDuration),
			formatDuration(result.ChannelDuration),
			result.Speedup)
	}
	fmt.Println(strings.Repeat("-", 70))
	fmt.Println()
	fmt.Println("✓ Channel-based single writer eliminates lock contention!")
}

func runNaturalBatchingDemo() {
	printDemoHeader("3. Natural Batching")

	fmt.Println("Natural batching collects messages into batches based on arrival rate,")
	fmt.Println("not fixed timeouts. This provides low latency under light load and")
	fmt.Println("high throughput under heavy load.")
	fmt.Println()

	// Demonstrate with different burst patterns
	configs := []struct {
		totalItems int
		burstSize  int
		burstDelay time.Duration
		name       string
	}{
		{1000, 10, 10 * time.Millisecond, "Light load (small bursts)"},
		{10000, 500, 5 * time.Millisecond, "Heavy load (large bursts)"},
		{5000, 100, 0, "Maximum throughput (no delay)"},
	}

	fmt.Println("Results:")
	fmt.Println(strings.Repeat("-", 70))

	for _, cfg := range configs {
		result := batching.RunNaturalBatchingDemo(cfg.totalItems, cfg.burstSize, cfg.burstDelay)

		fmt.Printf("\n  %s:\n", cfg.name)
		fmt.Printf("    Total items:       %s\n", formatNumber(int64(result.TotalItems)))
		fmt.Printf("    Total batches:     %d\n", result.TotalBatches)
		fmt.Printf("    Average batch:     %.1f items\n", result.AverageBatchSize)
		fmt.Printf("    Throughput:        %s items/sec\n", formatNumber(int64(result.Throughput)))
		fmt.Printf("    Duration:          %s\n", formatDuration(result.Duration))

		// Show batch size distribution (first 10)
		if len(result.BatchSizes) > 0 {
			displayCount := 10
			if len(result.BatchSizes) < displayCount {
				displayCount = len(result.BatchSizes)
			}
			fmt.Printf("    First %d batches:  %v\n", displayCount, result.BatchSizes[:displayCount])
		}
	}

	fmt.Println(strings.Repeat("-", 70))
	fmt.Println()
	fmt.Println("✓ Batch sizes naturally adapt to arrival rate!")
}

func runSequentialAccessDemo() {
	printDemoHeader("4. Sequential Memory Access")

	fmt.Println("Sequential memory access leverages CPU prefetching by accessing")
	fmt.Println("data in a predictable, linear pattern. Random access patterns")
	fmt.Println("cause cache misses, severely degrading performance.")
	fmt.Println()

	// Test different sizes
	sizes := []int{1_000, 10_000, 100_000}
	iterations := 100

	fmt.Println("Sequential vs Random Access:")
	fmt.Println(strings.Repeat("-", 70))
	fmt.Printf("  %-10s %-15s %-15s %-10s\n", "Size", "Sequential", "Random", "Speedup")
	fmt.Println(strings.Repeat("-", 70))

	for _, size := range sizes {
		result := memory.RunSequentialAccessDemo(size, iterations)
		fmt.Printf("  %-10s %-15s %-15s %6.2fx\n",
			formatNumber(int64(size)),
			formatDuration(result.SequentialDuration),
			formatDuration(result.RandomDuration),
			result.Speedup)
	}
	fmt.Println(strings.Repeat("-", 70))
	fmt.Println()

	fmt.Println("Array vs Linked List:")
	fmt.Println(strings.Repeat("-", 70))
	fmt.Printf("  %-10s %-15s %-15s %-10s\n", "Size", "Array", "LinkedList", "Speedup")
	fmt.Println(strings.Repeat("-", 70))

	for _, size := range sizes {
		result := memory.RunArrayVsLinkedListDemo(size, iterations)
		fmt.Printf("  %-10s %-15s %-15s %6.2fx\n",
			formatNumber(int64(size)),
			formatDuration(result.ArrayDuration),
			formatDuration(result.LinkedListDuration),
			result.Speedup)
	}
	fmt.Println(strings.Repeat("-", 70))
	fmt.Println()

	fmt.Println("Slice vs Map Iteration:")
	fmt.Println(strings.Repeat("-", 70))
	fmt.Printf("  %-10s %-15s %-15s %-10s\n", "Size", "Slice", "Map", "Speedup")
	fmt.Println(strings.Repeat("-", 70))

	for _, size := range sizes {
		result := memory.RunMapVsSliceDemo(size, iterations)
		fmt.Printf("  %-10s %-15s %-15s %6.2fx\n",
			formatNumber(int64(size)),
			formatDuration(result.SliceDuration),
			formatDuration(result.MapDuration),
			result.Speedup)
	}
	fmt.Println(strings.Repeat("-", 70))
	fmt.Println()
	fmt.Println("✓ Sequential access patterns outperform random access!")
}

func printDemoHeader(title string) {
	fmt.Println(strings.Repeat("═", 72))
	fmt.Printf("  %s\n", title)
	fmt.Println(strings.Repeat("═", 72))
	fmt.Println()
}

func formatNumber(n int64) string {
	if n < 1000 {
		return fmt.Sprintf("%d", n)
	}
	if n < 1_000_000 {
		return fmt.Sprintf("%.1fK", float64(n)/1_000)
	}
	if n < 1_000_000_000 {
		return fmt.Sprintf("%.1fM", float64(n)/1_000_000)
	}
	return fmt.Sprintf("%.1fB", float64(n)/1_000_000_000)
}

func formatDuration(d time.Duration) string {
	if d < time.Microsecond {
		return fmt.Sprintf("%.1f ns", float64(d.Nanoseconds()))
	}
	if d < time.Millisecond {
		return fmt.Sprintf("%.1f µs", float64(d.Nanoseconds())/1000)
	}
	if d < time.Second {
		return fmt.Sprintf("%.1f ms", float64(d.Nanoseconds())/1_000_000)
	}
	return fmt.Sprintf("%.2f s", d.Seconds())
}
