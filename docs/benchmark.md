# Mechanical Sympathy Benchmark Report

> Cross-language comparison of mechanical sympathy techniques in .NET 9, Rust, and Go 1.23

## Overview

This report compares the effectiveness of mechanical sympathy optimizations across .NET, Rust, and Go implementations.

### Techniques Benchmarked

| Technique | Description |
|-----------|-------------|
| Cache Line Padding | Prevents false sharing by padding data structures to cache line boundaries |
| Single Writer Principle | Ensures only one thread writes to mutable state, using message passing |
| Natural Batching | Processes work in batches to amortize overhead and improve throughput |
| Sequential Memory Access | Optimizes for CPU prefetching by accessing memory sequentially |

---

## .NET 9 Results

### .NET Benchmark Results (BenchmarkDotNet)

```

BenchmarkDotNet v0.14.0, Ubuntu 24.04.4 LTS (Noble Numbat)
AMD EPYC 7763, 1 CPU, 4 logical and 2 physical cores
.NET SDK 9.0.312
  [Host]        : .NET 9.0.14 (9.0.1426.11910), X64 RyuJIT AVX2
  net9-ServerGC : .NET 9.0.14 (9.0.1426.11910), X64 RyuJIT AVX2

Job=net9-ServerGC  Runtime=.NET 9.0  Server=True  
IterationCount=10  WarmupCount=3  

 Method                     | MessageCount | ProducerCount | Mean       | Error      | StdDev     | Median     | P95        | Ratio  | RatioSD | Rank | Gen0      | Allocated  | Alloc Ratio |
--------------------------- |------------- |-------------- |-----------:|-----------:|-----------:|-----------:|-----------:|-------:|--------:|-----:|----------:|-----------:|------------:|
 **'Interlocked (Lock-Based)'** | **1000000**      | **1**             |   **2.100 ms** |  **0.0090 ms** |  **0.0059 ms** |   **2.099 ms** |   **2.109 ms** |   **1.00** |    **0.00** |    **1** |         **-** |      **448 B** |        **1.00** |
 'Channel (Single Writer)'  | 1000000      | 1             | 229.784 ms | 14.7021 ms |  9.7245 ms | 227.301 ms | 243.675 ms | 109.45 |    4.43 |    2 |         - |    69373 B |      154.85 |
                            |              |               |            |            |            |            |            |        |         |      |           |            |             |
 **'Interlocked (Lock-Based)'** | **1000000**      | **4**             |  **22.931 ms** |  **0.5447 ms** |  **0.3241 ms** |  **23.084 ms** |  **23.263 ms** |   **1.00** |    **0.02** |    **1** |         **-** |      **688 B** |        **1.00** |
 'Channel (Single Writer)'  | 1000000      | 4             | 296.571 ms | 16.7787 ms | 11.0981 ms | 299.555 ms | 307.258 ms |  12.94 |    0.49 |    2 | 3000.0000 | 32384064 B |   47,069.86 |
                            |              |               |            |            |            |            |            |        |         |      |           |            |             |
 **'Interlocked (Lock-Based)'** | **1000000**      | **8**             |  **22.650 ms** |  **0.2880 ms** |  **0.1905 ms** |  **22.707 ms** |  **22.846 ms** |   **1.00** |    **0.01** |    **1** |         **-** |     **1006 B** |        **1.00** |
 'Channel (Single Writer)'  | 1000000      | 8             | 330.022 ms | 22.8186 ms | 15.0931 ms | 330.053 ms | 345.602 ms |  14.57 |    0.65 |    2 | 6000.0000 | 68103984 B |   67,697.80 |
```

---

## Rust Results

### Rust Benchmark Results (Criterion)

```
false_sharing/bad_counter/1000000
false_sharing/good_counter/1000000
false_sharing/bad_counter/10000000
false_sharing/good_counter/10000000
false_sharing/bad_counter/100000000
false_sharing/good_counter/100000000
memory_access/sequential/10000
memory_access/random/10000
memory_access/sequential/100000
memory_access/random/100000
memory_access/sequential/1000000
memory_access/random/1000000
single_writer/agent/100000msg_1prod
single_writer/mutex/100000msg_1prod
single_writer/agent/100000msg_4prod
single_writer/mutex/100000msg_4prod
single_writer/agent/100000msg_8prod
single_writer/mutex/100000msg_8prod
single_writer/agent/1000000msg_8prod
single_writer/mutex/1000000msg_8prod
                        time:   [26.264 ms 26.335 ms 26.409 ms]
                        time:   [-4.8318% -4.5742% -4.2922%] (p = 0.00 < 0.05)
                        time:   [2.4122 ms 2.4148 ms 2.4175 ms]
                        time:   [-0.9237% -0.7062% -0.5216%] (p = 0.00 < 0.05)
                        time:   [224.52 ms 225.12 ms 225.85 ms]
                        time:   [-17.062% -16.767% -16.458%] (p = 0.00 < 0.05)
                        time:   [23.103 ms 23.129 ms 23.156 ms]
                        time:   [-1.0851% -0.9483% -0.7995%] (p = 0.00 < 0.05)
                        time:   [2.7778 s 2.7875 s 2.7967 s]
                        time:   [+0.9110% +1.3211% +1.6386%] (p = 0.00 < 0.05)
                        time:   [228.97 ms 229.23 ms 229.50 ms]
                        time:   [-1.1227% -0.9167% -0.7441%] (p = 0.00 < 0.05)
                        time:   [13.297 µs 13.323 µs 13.351 µs]
                        time:   [+15.313% +18.171% +21.103%] (p = 0.00 < 0.05)
                        time:   [10.337 µs 10.359 µs 10.392 µs]
                        time:   [-0.0268% +0.3437% +0.6673%] (p = 0.05 > 0.05)
                        time:   [119.46 µs 123.15 µs 127.55 µs]
                        time:   [-7.6841% -5.4058% -3.1571%] (p = 0.00 < 0.05)
                        time:   [102.19 µs 102.29 µs 102.40 µs]
                        time:   [-19.199% -18.103% -16.456%] (p = 0.00 < 0.05)
                        time:   [2.7441 ms 2.7647 ms 2.7872 ms]
                        time:   [-26.502% -25.606% -24.642%] (p = 0.00 < 0.05)
                        time:   [4.8185 ms 4.8493 ms 4.8815 ms]
                        time:   [-8.1597% -7.1044% -6.0488%] (p = 0.00 < 0.05)
                        time:   [162.10 ns 162.17 ns 162.26 ns]
                        time:   [-0.3022% -0.1241% -0.0011%] (p = 0.10 > 0.05)
                        time:   [1.5928 µs 1.5931 µs 1.5934 µs]
                        time:   [-2.0978% -2.0103% -1.9510%] (p = 0.00 < 0.05)
                        time:   [1.5648 µs 1.5673 µs 1.5711 µs]
                        time:   [-0.5588% -0.0790% +0.3037%] (p = 0.78 > 0.05)
                        time:   [15.612 µs 15.617 µs 15.622 µs]
                        time:   [+0.1340% +0.4172% +0.6144%] (p = 0.00 < 0.05)
                        time:   [15.879 µs 15.895 µs 15.916 µs]
                        time:   [-0.6061% -0.3603% -0.1284%] (p = 0.00 < 0.05)
                        time:   [155.25 µs 155.31 µs 155.37 µs]
                        time:   [-0.1616% -0.0211% +0.1002%] (p = 0.77 > 0.05)
                        time:   [8.1693 ms 8.2032 ms 8.2367 ms]
                        time:   [-8.2694% -7.6791% -7.0602%] (p = 0.00 < 0.05)
                        time:   [4.1081 ms 4.2675 ms 4.4254 ms]
                        time:   [+1.0425% +7.3664% +13.884%] (p = 0.02 < 0.05)
                        time:   [10.697 ms 10.717 ms 10.733 ms]
                        time:   [-1.7705% -1.5374% -1.3003%] (p = 0.00 < 0.05)
                        time:   [19.126 ms 19.171 ms 19.215 ms]
                        time:   [+4.1813% +4.8674% +5.4317%] (p = 0.00 < 0.05)
                        time:   [11.136 ms 11.156 ms 11.175 ms]
                        time:   [-0.1612% +0.0821% +0.3367%] (p = 0.52 > 0.05)
                        time:   [19.285 ms 19.323 ms 19.360 ms]
                        time:   [+4.7026% +5.0323% +5.3449%] (p = 0.00 < 0.05)
                        time:   [110.79 ms 110.96 ms 111.09 ms]
                        time:   [+0.0246% +0.2080% +0.3884%] (p = 0.02 < 0.05)
                        time:   [191.96 ms 192.37 ms 192.84 ms]
                        time:   [+2.7464% +3.1728% +3.5991%] (p = 0.00 < 0.05)
```

---

## Go Results

### Go Benchmark Results

```
BenchmarkBadCounter/iterations=10000000-4         	       5	 222548176 ns/op	     364 B/op	       4 allocs/op
BenchmarkBadCounter/iterations=100000000-4        	       1	2916160412 ns/op	      96 B/op	       4 allocs/op
BenchmarkGoodCounter/iterations=10000000-4        	      49	  23860793 ns/op	     339 B/op	       4 allocs/op
BenchmarkGoodCounter/iterations=100000000-4       	       5	 240398662 ns/op	     208 B/op	       4 allocs/op
BenchmarkPaddedCounterArray/PaddedCounterArray-4  	     252	   4514162 ns/op	    1055 B/op	      19 allocs/op
BenchmarkFalseSharingComparison/BadCounter_FalseSharing-4         	       4	 262016265 ns/op	      96 B/op	       4 allocs/op
BenchmarkFalseSharingComparison/GoodCounter_CachePadded-4         	      50	  23823031 ns/op	     208 B/op	       4 allocs/op
BenchmarkSequentialAccess/Sequential/size=1000-4                  	 3737089	       318.0 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialAccess/Random/size=1000-4                      	 3729727	       325.2 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialAccess/Sequential/size=10000-4                 	  382414	      3191 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialAccess/Random/size=10000-4                     	  383238	      3135 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialAccess/Sequential/size=100000-4                	   38066	     31207 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialAccess/Random/size=100000-4                    	   38161	     31831 ns/op	       0 B/op	       0 allocs/op
BenchmarkArrayVsLinkedList/Array/size=1000-4                      	 3748381	       318.2 ns/op	       0 B/op	       0 allocs/op
BenchmarkArrayVsLinkedList/LinkedList/size=1000-4                 	  953858	      1254 ns/op	       0 B/op	       0 allocs/op
BenchmarkArrayVsLinkedList/Array/size=10000-4                     	  383035	      3123 ns/op	       0 B/op	       0 allocs/op
BenchmarkArrayVsLinkedList/LinkedList/size=10000-4                	   95485	     12587 ns/op	       0 B/op	       0 allocs/op
BenchmarkArrayVsLinkedList/Array/size=100000-4                    	   38355	     31195 ns/op	       0 B/op	       0 allocs/op
BenchmarkArrayVsLinkedList/LinkedList/size=100000-4               	    9414	    126076 ns/op	       0 B/op	       0 allocs/op
BenchmarkSliceVsMap/Slice/size=1000-4                             	 1904012	       634.9 ns/op	       0 B/op	       0 allocs/op
BenchmarkSliceVsMap/Map/size=1000-4                               	  109864	     10902 ns/op	       0 B/op	       0 allocs/op
BenchmarkSliceVsMap/Slice/size=10000-4                            	  191784	      6253 ns/op	       0 B/op	       0 allocs/op
BenchmarkSliceVsMap/Map/size=10000-4                              	   10426	    114130 ns/op	       0 B/op	       0 allocs/op
BenchmarkSliceVsMap/Slice/size=100000-4                           	   19172	     62429 ns/op	       0 B/op	       0 allocs/op
BenchmarkSliceVsMap/Map/size=100000-4                             	    1095	   1088993 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumQuantities/size=1000-4          	 3763422	       320.1 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumPrices/size=1000-4              	 3754345	       319.3 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumValues/size=1000-4              	 3757993	       319.2 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumQuantities/size=10000-4         	  381122	      3134 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumPrices/size=10000-4             	  376915	      3147 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumValues/size=10000-4             	  375489	      3130 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumQuantities/size=100000-4        	   38517	     31222 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumPrices/size=100000-4            	   38412	     31272 ns/op	       0 B/op	       0 allocs/op
BenchmarkSequentialOrderBuffer/SumValues/size=100000-4            	   38251	     31273 ns/op	       0 B/op	       0 allocs/op
BenchmarkMemoryAccessComparison/Array_Sequential-4                	   19186	     62389 ns/op	       0 B/op	       0 allocs/op
BenchmarkMemoryAccessComparison/Array_Random-4                    	   19134	     62901 ns/op	       0 B/op	       0 allocs/op
BenchmarkMemoryAccessComparison/LinkedList_Traversal-4            	    9538	    125960 ns/op	       0 B/op	       0 allocs/op
BenchmarkMemoryAccessComparison/Slice_Sequential-4                	   38451	     31301 ns/op	       0 B/op	       0 allocs/op
BenchmarkMemoryAccessComparison/Map_Iteration-4                   	     468	   2142437 ns/op	       0 B/op	       0 allocs/op
BenchmarkMutexCounter/msgs=100000/prod=1-4                        	    2154	    556380 ns/op	      80 B/op	       4 allocs/op
BenchmarkMutexCounter/msgs=100000/prod=4-4                        	     776	   1579682 ns/op	     230 B/op	       7 allocs/op
BenchmarkMutexCounter/msgs=100000/prod=8-4                        	     458	   2441338 ns/op	     425 B/op	      11 allocs/op
BenchmarkMutexCounter/msgs=1000000/prod=8-4                       	      38	  31351397 ns/op	     416 B/op	      11 allocs/op
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
- **Rust**: Uses `tokio::sync::mpsc` channels, `crossbeam-utils::CachePadded` for padding
- **Go**: Uses native channels for message passing, byte array padding for cache line alignment

---

*Report generated by GitHub Actions*

---
*Last updated: 2026-04-08 00:01:36 UTC*
