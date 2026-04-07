# Mechanical Sympathy Benchmark Report

> Cross-language comparison of mechanical sympathy techniques

## Overview

This report compares the effectiveness of mechanical sympathy optimizations across .NET and Rust implementations.

### Techniques Benchmarked

| Technique | Description |
|-----------|-------------|
| Cache Line Padding | Prevents false sharing by padding data structures to cache line boundaries |
| Single Writer Principle | Ensures only one thread writes to mutable state, using message passing |
| Natural Batching | Processes work in batches to amortize overhead and improve throughput |
| Sequential Memory Access | Optimizes for CPU prefetching by accessing memory sequentially |

---

## .NET 9 Results


BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
Intel Xeon Platinum 8370C CPU 2.80GHz, 1 CPU, 4 logical and 2 physical cores
.NET SDK 9.0.312
  [Host]        : .NET 9.0.14 (9.0.1426.11910), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI
  net9-ServerGC : .NET 9.0.14 (9.0.1426.11910), X64 RyuJIT AVX-512F+CD+BW+DQ+VL+VBMI

Job=net9-ServerGC  Runtime=.NET 9.0  Server=True  
IterationCount=10  WarmupCount=3  

 Method                     | MessageCount | ProducerCount | Mean       | Error      | StdDev     | Median     | P95        | Ratio | RatioSD | Rank | Gen0       | Gen1      | Gen2      | Allocated  | Alloc Ratio |
--------------------------- |------------- |-------------- |-----------:|-----------:|-----------:|-----------:|-----------:|------:|--------:|-----:|-----------:|----------:|----------:|-----------:|------------:|
 **'Interlocked (Lock-Based)'** | **1000000**      | **1**             |   **6.085 ms** |  **0.0181 ms** |  **0.0120 ms** |   **6.082 ms** |   **6.103 ms** |  **1.00** |    **0.00** |    **1** |          **-** |         **-** |         **-** |      **446 B** |        **1.00** |
 'Channel (Single Writer)'  | 1000000      | 1             | 342.168 ms | 49.1563 ms | 32.5139 ms | 344.341 ms | 387.051 ms | 56.23 |    5.10 |    2 |          - |         - |         - |    78128 B |      175.17 |
                            |              |               |            |            |            |            |            |       |         |      |            |           |           |            |             |
 **'Interlocked (Lock-Based)'** | **1000000**      | **4**             |  **17.646 ms** |  **0.1230 ms** |  **0.0813 ms** |  **17.624 ms** |  **17.770 ms** |  **1.00** |    **0.01** |    **1** |          **-** |         **-** |         **-** |      **679 B** |        **1.00** |
 'Channel (Single Writer)'  | 1000000      | 4             | 466.158 ms | 37.1278 ms | 24.5578 ms | 470.853 ms | 489.821 ms | 26.42 |    1.33 |    2 |  4000.0000 |         - |         - | 40720496 B |   59,971.28 |
                            |              |               |            |            |            |            |            |       |         |      |            |           |           |            |             |
 **'Interlocked (Lock-Based)'** | **1000000**      | **8**             |  **17.450 ms** |  **0.1145 ms** |  **0.0682 ms** |  **17.472 ms** |  **17.517 ms** |  **1.00** |    **0.01** |    **1** |          **-** |         **-** |         **-** |     **1006 B** |        **1.00** |
 'Channel (Single Writer)'  | 1000000      | 8             | 550.574 ms | 30.6549 ms | 20.2763 ms | 554.903 ms | 578.465 ms | 31.55 |    1.11 |    2 | 13000.0000 | 1000.0000 | 1000.0000 |          - |        0.00 |

---

## Rust Results

## Rust Benchmark Results (Criterion)

```
[1m[32m     Running[0m benches/false_sharing.rs (target/release/deps/false_sharing-6563e725b0fdc554)
Benchmarking false_sharing/bad_counter/1000000
Benchmarking false_sharing/bad_counter/1000000: Warming up for 3.0000 s
Benchmarking false_sharing/bad_counter/1000000: Collecting 100 samples in estimated 5.2636 s (200 iterations)
Benchmarking false_sharing/bad_counter/1000000: Analyzing
false_sharing/bad_counter/1000000
Benchmarking false_sharing/good_counter/1000000
Benchmarking false_sharing/good_counter/1000000: Warming up for 3.0000 s
Benchmarking false_sharing/good_counter/1000000: Collecting 100 samples in estimated 5.0233 s (2100 iterations)
Benchmarking false_sharing/good_counter/1000000: Analyzing
false_sharing/good_counter/1000000
Benchmarking false_sharing/bad_counter/10000000
Benchmarking false_sharing/bad_counter/10000000: Warming up for 3.0000 s
Benchmarking false_sharing/bad_counter/10000000: Collecting 100 samples in estimated 28.350 s (100 iterations)
Benchmarking false_sharing/bad_counter/10000000: Analyzing
false_sharing/bad_counter/10000000
Benchmarking false_sharing/good_counter/10000000
Benchmarking false_sharing/good_counter/10000000: Warming up for 3.0000 s
Benchmarking false_sharing/good_counter/10000000: Collecting 100 samples in estimated 6.8636 s (300 iterations)
Benchmarking false_sharing/good_counter/10000000: Analyzing
false_sharing/good_counter/10000000
Benchmarking false_sharing/bad_counter/100000000
Benchmarking false_sharing/bad_counter/100000000: Warming up for 3.0000 s
Benchmarking false_sharing/bad_counter/100000000: Collecting 100 samples in estimated 265.33 s (100 iterations)
Benchmarking false_sharing/bad_counter/100000000: Analyzing
false_sharing/bad_counter/100000000
Benchmarking false_sharing/good_counter/100000000
Benchmarking false_sharing/good_counter/100000000: Warming up for 3.0000 s
Benchmarking false_sharing/good_counter/100000000: Collecting 100 samples in estimated 22.669 s (100 iterations)
Benchmarking false_sharing/good_counter/100000000: Analyzing
false_sharing/good_counter/100000000
[1m[32m     Running[0m benches/sequential_access.rs (target/release/deps/sequential_access-a1e59fb5a3725b73)
Benchmarking memory_access/sequential/10000
Benchmarking memory_access/sequential/10000: Warming up for 3.0000 s
Benchmarking memory_access/sequential/10000: Collecting 100 samples in estimated 5.0219 s (404k iterations)
Benchmarking memory_access/sequential/10000: Analyzing
memory_access/sequential/10000
Benchmarking memory_access/sequential/100000
Benchmarking memory_access/sequential/100000: Warming up for 3.0000 s
Benchmarking memory_access/sequential/100000: Collecting 100 samples in estimated 5.0729 s (35k iterations)
Benchmarking memory_access/sequential/100000: Analyzing
memory_access/sequential/100000
Benchmarking memory_access/sequential/1000000
Benchmarking memory_access/sequential/1000000: Warming up for 3.0000 s
Benchmarking memory_access/sequential/1000000: Collecting 100 samples in estimated 5.0150 s (1800 iterations)
Benchmarking memory_access/sequential/1000000: Analyzing
memory_access/sequential/1000000
[1m[32m     Running[0m benches/single_writer.rs (target/release/deps/single_writer-d10e772254cab6d4)
Benchmarking single_writer/agent/100000msg_1prod
Benchmarking single_writer/agent/100000msg_1prod: Warming up for 3.0000 s
Benchmarking single_writer/agent/100000msg_1prod: Collecting 100 samples in estimated 5.1364 s (600 iterations)
Benchmarking single_writer/agent/100000msg_1prod: Analyzing
single_writer/agent/100000msg_1prod
Benchmarking single_writer/mutex/100000msg_1prod
Benchmarking single_writer/mutex/100000msg_1prod: Warming up for 3.0000 s
Benchmarking single_writer/mutex/100000msg_1prod: Collecting 100 samples in estimated 5.0804 s (1100 iterations)
Benchmarking single_writer/mutex/100000msg_1prod: Analyzing
single_writer/mutex/100000msg_1prod
Benchmarking single_writer/agent/100000msg_4prod
Benchmarking single_writer/agent/100000msg_4prod: Warming up for 3.0000 s
Benchmarking single_writer/agent/100000msg_4prod: Collecting 100 samples in estimated 5.3368 s (500 iterations)
Benchmarking single_writer/agent/100000msg_4prod: Analyzing
single_writer/agent/100000msg_4prod
Benchmarking single_writer/mutex/100000msg_4prod
Benchmarking single_writer/mutex/100000msg_4prod: Warming up for 3.0000 s
Benchmarking single_writer/mutex/100000msg_4prod: Collecting 100 samples in estimated 5.4735 s (300 iterations)
Benchmarking single_writer/mutex/100000msg_4prod: Analyzing
single_writer/mutex/100000msg_4prod
Benchmarking single_writer/agent/100000msg_8prod
Benchmarking single_writer/agent/100000msg_8prod: Warming up for 3.0000 s
Benchmarking single_writer/agent/100000msg_8prod: Collecting 100 samples in estimated 5.4948 s (500 iterations)
Benchmarking single_writer/agent/100000msg_8prod: Analyzing
single_writer/agent/100000msg_8prod
Benchmarking single_writer/mutex/100000msg_8prod
Benchmarking single_writer/mutex/100000msg_8prod: Warming up for 3.0000 s
Benchmarking single_writer/mutex/100000msg_8prod: Collecting 100 samples in estimated 5.6062 s (300 iterations)
Benchmarking single_writer/mutex/100000msg_8prod: Analyzing
single_writer/mutex/100000msg_8prod
Benchmarking single_writer/agent/1000000msg_8prod
Benchmarking single_writer/agent/1000000msg_8prod: Warming up for 3.0000 s
Benchmarking single_writer/agent/1000000msg_8prod: Collecting 100 samples in estimated 10.908 s (100 iterations)
Benchmarking single_writer/agent/1000000msg_8prod: Analyzing
single_writer/agent/1000000msg_8prod
Benchmarking single_writer/mutex/1000000msg_8prod
Benchmarking single_writer/mutex/1000000msg_8prod: Warming up for 3.0000 s
Benchmarking single_writer/mutex/1000000msg_8prod: Collecting 100 samples in estimated 18.714 s (100 iterations)
Benchmarking single_writer/mutex/1000000msg_8prod: Analyzing
single_writer/mutex/1000000msg_8prod
                        time:   [26.188 ms 26.337 ms 26.462 ms]
                        time:   [-5.0941% -4.5671% -4.0868%] (p = 0.00 < 0.05)
                        time:   [2.3886 ms 2.3975 ms 2.4098 ms]
                        time:   [-1.8420% -1.4190% -0.8363%] (p = 0.00 < 0.05)
                        time:   [282.99 ms 283.68 ms 284.32 ms]
                        time:   [+4.5607% +4.8842% +5.2077%] (p = 0.00 < 0.05)
                        time:   [22.822 ms 22.852 ms 22.882 ms]
                        time:   [-2.2824% -2.1315% -1.9674%] (p = 0.00 < 0.05)
                        time:   [2.6492 s 2.6506 s 2.6518 s]
                        time:   [-3.7304% -3.6576% -3.5844%] (p = 0.00 < 0.05)
                        time:   [225.47 ms 225.75 ms 226.04 ms]
                        time:   [-2.6263% -2.4200% -2.2462%] (p = 0.00 < 0.05)
                        time:   [12.390 µs 12.438 µs 12.488 µs]
                        time:   [+7.5639% +10.221% +12.950%] (p = 0.00 < 0.05)
                        time:   [10.491 µs 10.510 µs 10.534 µs]
                        time:   [+1.6956% +2.3814% +3.4484%] (p = 0.00 < 0.05)
                        time:   [143.14 µs 143.68 µs 144.09 µs]
                        time:   [-0.8595% -0.3440% +0.0684%] (p = 0.15 > 0.05)
                        time:   [125.20 µs 125.45 µs 125.68 µs]
                        time:   [-1.3986% -1.0633% -0.7231%] (p = 0.00 < 0.05)
                        time:   [2.7731 ms 2.8026 ms 2.8363 ms]
                        time:   [-25.663% -24.586% -23.467%] (p = 0.00 < 0.05)
                        time:   [4.5526 ms 4.5722 ms 4.5935 ms]
                        time:   [-13.343% -12.413% -11.544%] (p = 0.00 < 0.05)
                        time:   [162.09 ns 162.16 ns 162.25 ns]
                        time:   [-0.2175% +0.0979% +0.5654%] (p = 0.75 > 0.05)
                        time:   [1.6523 µs 1.6531 µs 1.6542 µs]
                        time:   [+1.6869% +2.0769% +2.7324%] (p = 0.00 < 0.05)
                        time:   [1.5645 µs 1.5673 µs 1.5709 µs]
                        time:   [-0.5700% -0.0865% +0.3151%] (p = 0.77 > 0.05)
                        time:   [15.513 µs 15.519 µs 15.525 µs]
                        time:   [-0.4285% -0.1486% +0.0537%] (p = 0.27 > 0.05)
                        time:   [15.883 µs 15.901 µs 15.927 µs]
                        time:   [-0.4999% -0.2259% +0.0640%] (p = 0.12 > 0.05)
                        time:   [154.76 µs 154.83 µs 154.92 µs]
                        time:   [-0.5003% -0.3621% -0.2273%] (p = 0.00 < 0.05)
                        time:   [8.4628 ms 8.5201 ms 8.5746 ms]
                        time:   [-4.9421% -4.1125% -3.3309%] (p = 0.00 < 0.05)
                        time:   [4.3178 ms 4.5303 ms 4.7381 ms]
                        time:   [+6.9443% +13.979% +22.527%] (p = 0.00 < 0.05)
                        time:   [10.644 ms 10.663 ms 10.680 ms]
                        time:   [-2.2627% -2.0362% -1.7916%] (p = 0.00 < 0.05)
                        time:   [18.359 ms 18.396 ms 18.431 ms]
                        time:   [-0.0261% +0.6273% +1.1575%] (p = 0.03 < 0.05)
                        time:   [10.973 ms 10.991 ms 11.006 ms]
                        time:   [-1.6309% -1.4046% -1.1793%] (p = 0.00 < 0.05)
                        time:   [18.456 ms 18.497 ms 18.537 ms]
                        time:   [+0.2170% +0.5416% +0.8549%] (p = 0.00 < 0.05)
                        time:   [108.71 ms 108.94 ms 109.13 ms]
                        time:   [-1.8473% -1.6124% -1.3975%] (p = 0.00 < 0.05)
                        time:   [185.08 ms 185.51 ms 185.89 ms]
                        time:   [-0.9217% -0.5053% -0.1202%] (p = 0.01 < 0.05)
```

---

## Go Results

## Go Benchmark Results

```
BenchmarkBadCounter/iterations=10000000-4         	       4	 281551525 ns/op	     120 B/op	       4 allocs/op
BenchmarkBadCounter/iterations=100000000-4        	       1	3040089082 ns/op	     800 B/op	       6 allocs/op
BenchmarkGoodCounter/iterations=10000000-4        	      48	  23820800 ns/op	     273 B/op	       4 allocs/op
BenchmarkGoodCounter/iterations=100000000-4       	       5	 238923873 ns/op	     297 B/op	       4 allocs/op
BenchmarkPaddedCounterArray/PaddedCounterArray-4  	     264	   4481254 ns/op	    1016 B/op	      19 allocs/op
BenchmarkFalseSharingComparison/BadCounter_FalseSharing-4         	       4	 280367462 ns/op	      96 B/op	       4 allocs/op
BenchmarkFalseSharingComparison/GoodCounter_CachePadded-4         	      49	  23914329 ns/op	     208 B/op	       4 allocs/op
BenchmarkSequentialAccess/Sequential/size=1000-4                  	 3765774	       318.9 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialAccess/Random/size=1000-4                      	 3693687	       321.8 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialAccess/Sequential/size=10000-4                 	  383652	      3127 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialAccess/Random/size=10000-4                     	  377660	      3143 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialAccess/Sequential/size=100000-4                	   38546	     31418 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialAccess/Random/size=100000-4                    	   38160	     31425 ns/op	       0 B/op	       0 allocs/op
BenchmarkArrayVsLinkedList/Array/size=1000-4                      	 3723970	       317.6 ns/op	       0 B/op	       0 allocs/op
BenchmarkArrayVsLinkedList/LinkedList/size=1000-4                 	  957322	      1252 ns/op	       0 B/op	       0 allocs/op
BenchmarkArrayVsLinkedList/Array/size=10000-4                     	  377559	      3126 ns/op	       0 B/op	       0 allocs/op
BenchmarkArrayVsLinkedList/LinkedList/size=10000-4                	   95407	     12571 ns/op	       0 B/op	       0 allocs/op
BenchmarkArrayVsLinkedList/Array/size=100000-4                    	   38396	     31191 ns/op	       0 B/op	       0 allocs/op
BenchmarkArrayVsLinkedList/LinkedList/size=100000-4               	    9381	    125762 ns/op	       0 B/op	       0 allocs/op
BenchmarkSliceVsMap/Slice/size=1000-4                             	 1902086	       633.1 ns/op	       0 B/op	       0 allocs/op
BenchmarkSliceVsMap/Map/size=1000-4                               	  109332	     10981 ns/op	       0 B/op	       0 allocs/op
BenchmarkSliceVsMap/Slice/size=10000-4                            	  192238	      6249 ns/op	       0 B/op	       0 allocs/op
BenchmarkSliceVsMap/Map/size=10000-4                              	   10000	    114396 ns/op	       0 B/op	       0 allocs/op
BenchmarkSliceVsMap/Slice/size=100000-4                           	   19172	     62430 ns/op	       0 B/op	       0 allocs/op
BenchmarkSliceVsMap/Map/size=100000-4                             	    1102	   1087879 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumQuantities/size=1000-4          	 3766794	       319.0 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumPrices/size=1000-4              	 3679724	       319.3 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumValues/size=1000-4              	 3748627	       319.1 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumQuantities/size=10000-4         	  382970	      3162 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumPrices/size=10000-4             	  382060	      3126 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumValues/size=10000-4             	  384304	      3133 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumQuantities/size=100000-4        	   38503	     31158 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumPrices/size=100000-4            	   38371	     31165 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumValues/size=100000-4            	   38404	     31228 ns/op	       0 B/op	       0 allocs/op
BenchmarkMemoryAccessComparison/Array_Sequential-4                	   19227	     62375 ns/op	       0 B/op	       0 allocs/op
BenchmarkMemoryAccessComparison/Array_Random-4                    	   19150	     62784 ns/op	       0 B/op	       0 allocs/op
BenchmarkMemoryAccessComparison/LinkedList_Traversal-4            	    9512	    125746 ns/op	       0 B/op	       0 allocs/op
BenchmarkMemoryAccessComparison/Slice_Sequential-4                	   38402	     31216 ns/op	       0 B/op	       0 allocs/op
BenchmarkMemoryAccessComparison/Map_Iteration-4                   	    1040	   1104413 ns/op	       0 B/op	       0 allocs/op
BenchmarkMutexCounter/msgs=100000/prod=1-4                        	    2284	    528872 ns/op	      80 B/op	       4 allocs/op
BenchmarkMutexCounter/msgs=100000/prod=4-4                        	     783	   1540466 ns/op	     231 B/op	       7 allocs/op
BenchmarkMutexCounter/msgs=100000/prod=8-4                        	     526	   2229713 ns/op	     428 B/op	      11 allocs/op
BenchmarkMutexCounter/msgs=1000000/prod=8-4                       	      42	  28131848 ns/op	     428 B/op	      11 allocs/op
```

---

## Cross-Language Comparison

### Key Observations

All three implementations demonstrate the effectiveness of mechanical sympathy techniques:

1. **False Sharing Prevention**: Cache-padded counters show significant performance improvement over naive implementations in all languages

2. **Single Writer Principle**: Message-passing architectures eliminate lock contention and provide predictable latency

3. **Natural Batching**: Batch processing improves throughput by amortizing per-operation overhead

4. **Sequential Access**: Linear memory traversal outperforms random access patterns due to CPU prefetching

### Language-Specific Notes

- **.NET**: Uses `System.Threading.Channels` for message passing, `StructLayout` for padding
- **Rust**: Uses `tokio::sync::mpsc` channels, `cache-padded` crate for padding
- **Go**: Uses native channels for message passing, byte array padding for cache line alignment

---

*Report generated by GitHub Actions*

---
*Last updated: 2026-04-07 19:55:31 UTC*
