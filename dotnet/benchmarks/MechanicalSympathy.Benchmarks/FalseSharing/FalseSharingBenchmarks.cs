using BenchmarkDotNet.Attributes;
using MechanicalSympathy.Benchmarks.Configs;
using MechanicalSympathy.Core.Infrastructure.CachePadding;

namespace MechanicalSympathy.Benchmarks.FalseSharing;

/// <summary>
/// Benchmarks demonstrating the impact of false sharing and cache line padding.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class FalseSharingBenchmarks
{
    private BadCounter _badCounter = null!;
    private GoodCounter _goodCounter = null!;

    [Params(10_000_000, 100_000_000)]
    public int Iterations { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _badCounter = new BadCounter();
        _goodCounter = new GoodCounter();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _badCounter.Reset();
        _goodCounter.Reset();
    }

    /// <summary>
    /// Benchmark with false sharing - both counters share the same cache line.
    /// Expected: Much slower due to cache line contention.
    /// </summary>
    [Benchmark(Baseline = true, Description = "BadCounter (False Sharing)")]
    public async Task BadCounterWithFalseSharing()
    {
        _badCounter.Reset();

        await Task.WhenAll(
            Task.Run(() => IncrementBad(_badCounter, true)),
            Task.Run(() => IncrementBad(_badCounter, false))
        );
    }

    /// <summary>
    /// Benchmark with cache line padding - each counter has its own cache line.
    /// Expected: Much faster due to no cache line contention.
    /// </summary>
    [Benchmark(Description = "GoodCounter (Cache-Line Padded)")]
    public async Task GoodCounterWithPadding()
    {
        _goodCounter.Reset();

        await Task.WhenAll(
            Task.Run(() => IncrementGood(_goodCounter, true)),
            Task.Run(() => IncrementGood(_goodCounter, false))
        );
    }

    private void IncrementBad(BadCounter counter, bool isFirst)
    {
        for (var i = 0; i < Iterations; i++)
        {
            if (isFirst)
                counter.IncrementCount1();
            else
                counter.IncrementCount2();
        }
    }

    private void IncrementGood(GoodCounter counter, bool isFirst)
    {
        for (var i = 0; i < Iterations; i++)
        {
            if (isFirst)
                counter.IncrementCount1();
            else
                counter.IncrementCount2();
        }
    }
}
