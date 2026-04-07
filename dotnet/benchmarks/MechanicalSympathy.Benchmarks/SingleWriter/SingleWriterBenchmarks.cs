using BenchmarkDotNet.Attributes;
using MechanicalSympathy.Benchmarks.Configs;
using MechanicalSympathy.Core.Infrastructure.Agents;
using Microsoft.Extensions.Logging.Abstractions;

namespace MechanicalSympathy.Benchmarks.SingleWriter;

/// <summary>
/// Benchmarks comparing Interlocked (lock-based) vs Single Writer (lock-free) patterns.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class SingleWriterBenchmarks
{
    [Params(1_000_000)]
    public int MessageCount { get; set; }

    [Params(1, 4, 8)]
    public int ProducerCount { get; set; }

    /// <summary>
    /// Baseline: Using Interlocked for thread-safe counter increment.
    /// Has lock contention that increases with producer count.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Interlocked (Lock-Based)")]
    public async Task InterlockedCounter()
    {
        long counter = 0;
        var tasks = new Task[ProducerCount];
        var perProducer = MessageCount / ProducerCount;

        for (var p = 0; p < ProducerCount; p++)
        {
            tasks[p] = Task.Run(() =>
            {
                for (var i = 0; i < perProducer; i++)
                {
                    Interlocked.Increment(ref counter);
                }
            });
        }

        await Task.WhenAll(tasks);
    }

    /// <summary>
    /// Single Writer pattern using System.Threading.Channels.
    /// No lock contention - scales linearly with producers.
    /// </summary>
    [Benchmark(Description = "Channel (Single Writer)")]
    public async Task ChannelSingleWriter()
    {
        var agent = new StatsAgent(NullLogger<StatsAgent>.Instance, capacity: 4096);
        using var cts = new CancellationTokenSource();

        var consumerTask = agent.StartAsync(cts.Token);
        var perProducer = MessageCount / ProducerCount;

        // Producers
        var producers = new Task[ProducerCount];
        for (var p = 0; p < ProducerCount; p++)
        {
            producers[p] = Task.Run(async () =>
            {
                for (var i = 0; i < perProducer; i++)
                {
                    await agent.SendAsync(new StatsMessage(1));
                }
            });
        }

        await Task.WhenAll(producers);
        await agent.StopAsync();
        await consumerTask;
    }
}
