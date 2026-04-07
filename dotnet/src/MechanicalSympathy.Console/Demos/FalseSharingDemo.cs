using System.Diagnostics;
using MechanicalSympathy.Core.Infrastructure.CachePadding;
using Microsoft.Extensions.Logging;

namespace MechanicalSympathy.Console.Demos;

/// <summary>
/// Demonstrates the impact of false sharing and how cache line padding fixes it.
/// </summary>
public sealed class FalseSharingDemo
{
    private const long NumIterations = 100_000_000;
    private readonly ILogger<FalseSharingDemo> _logger;

    public FalseSharingDemo(ILogger<FalseSharingDemo> logger)
    {
        _logger = logger;
    }

    public async Task RunAsync()
    {
        System.Console.WriteLine("┌──────────────────────────────────────────────────────────────────────┐");
        System.Console.WriteLine("│ Demo 1: False Sharing vs Cache-Line Padding                         │");
        System.Console.WriteLine("├──────────────────────────────────────────────────────────────────────┤");
        System.Console.WriteLine("│ Two threads increment separate counters. Without padding, both      │");
        System.Console.WriteLine("│ counters share the same cache line, causing false sharing.          │");
        System.Console.WriteLine("└──────────────────────────────────────────────────────────────────────┘");
        System.Console.WriteLine();

        System.Console.WriteLine($"Running {NumIterations:N0} iterations per thread...");
        System.Console.WriteLine();

        // Test BadCounter (with false sharing)
        var badCounter = new BadCounter();
        var sw = Stopwatch.StartNew();

        await Task.WhenAll(
            Task.Run(() => IncrementBadCounter(badCounter, useCount1: true)),
            Task.Run(() => IncrementBadCounter(badCounter, useCount1: false))
        );

        var badTime = sw.Elapsed;

        // Test GoodCounter (with cache line padding)
        var goodCounter = new GoodCounter();
        sw.Restart();

        await Task.WhenAll(
            Task.Run(() => IncrementGoodCounter(goodCounter, useCount1: true)),
            Task.Run(() => IncrementGoodCounter(goodCounter, useCount1: false))
        );

        var goodTime = sw.Elapsed;

        // Calculate speedup
        var speedup = badTime.TotalMilliseconds / goodTime.TotalMilliseconds;

        // Print results
        System.Console.WriteLine("Results:");
        System.Console.WriteLine($"  BadCounter  (false sharing):   {badTime.TotalMilliseconds,10:F1} ms");
        System.Console.WriteLine($"  GoodCounter (cache padded):    {goodTime.TotalMilliseconds,10:F1} ms");
        System.Console.WriteLine($"  Speedup:                       {speedup,10:F1}x");
        System.Console.WriteLine();

        System.Console.WriteLine("Verification:");
        System.Console.WriteLine($"  BadCounter.Count1:  {badCounter.Count1:N0}");
        System.Console.WriteLine($"  BadCounter.Count2:  {badCounter.Count2:N0}");
        System.Console.WriteLine($"  GoodCounter.Count1: {goodCounter.Count1:N0}");
        System.Console.WriteLine($"  GoodCounter.Count2: {goodCounter.Count2:N0}");
        System.Console.WriteLine();

        if (speedup >= 1.5)
        {
            System.Console.WriteLine($"[PASS] Cache line padding improved performance by {speedup:F1}x");
        }
        else
        {
            System.Console.WriteLine($"[NOTE] Speedup ({speedup:F1}x) may vary based on CPU architecture");
        }
    }

    private static void IncrementBadCounter(BadCounter counter, bool useCount1)
    {
        for (long i = 0; i < NumIterations; i++)
        {
            if (useCount1)
                counter.IncrementCount1();
            else
                counter.IncrementCount2();
        }
    }

    private static void IncrementGoodCounter(GoodCounter counter, bool useCount1)
    {
        for (long i = 0; i < NumIterations; i++)
        {
            if (useCount1)
                counter.IncrementCount1();
            else
                counter.IncrementCount2();
        }
    }
}
