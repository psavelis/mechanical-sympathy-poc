using System.Diagnostics;
using MechanicalSympathy.Core.Infrastructure.Agents;
using Microsoft.Extensions.Logging;

namespace MechanicalSympathy.Console.Demos;

/// <summary>
/// Demonstrates the Single Writer Principle using System.Threading.Channels.
/// </summary>
public sealed class SingleWriterDemo
{
    private readonly ILogger<SingleWriterDemo> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public SingleWriterDemo(ILogger<SingleWriterDemo> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task RunAsync()
    {
        System.Console.WriteLine("┌──────────────────────────────────────────────────────────────────────┐");
        System.Console.WriteLine("│ Demo 2: Single Writer Principle (StatsAgent)                        │");
        System.Console.WriteLine("├──────────────────────────────────────────────────────────────────────┤");
        System.Console.WriteLine("│ Multiple producer threads send messages to a single agent thread.   │");
        System.Console.WriteLine("│ The agent owns all state - no locks needed, zero contention.        │");
        System.Console.WriteLine("└──────────────────────────────────────────────────────────────────────┘");
        System.Console.WriteLine();

        const int producerCount = 20;
        const long updatesPerProducer = 5_000_000;
        var totalUpdates = producerCount * updatesPerProducer;

        System.Console.WriteLine($"Configuration:");
        System.Console.WriteLine($"  Producers:           {producerCount}");
        System.Console.WriteLine($"  Updates per producer: {updatesPerProducer:N0}");
        System.Console.WriteLine($"  Total updates:        {totalUpdates:N0}");
        System.Console.WriteLine();

        // Compare with Interlocked approach
        System.Console.WriteLine("Running Interlocked approach (baseline)...");
        var interlockedResult = await RunInterlockedApproachAsync(producerCount, updatesPerProducer);

        System.Console.WriteLine("Running Single Writer approach...");
        var singleWriterResult = await RunSingleWriterApproachAsync(producerCount, updatesPerProducer);

        // Print results
        System.Console.WriteLine();
        System.Console.WriteLine("Results:");
        System.Console.WriteLine($"  Interlocked (mutex-based):    {interlockedResult.ElapsedMs,10:F1} ms");
        System.Console.WriteLine($"  Single Writer (lock-free):    {singleWriterResult.ElapsedMs,10:F1} ms");

        var throughputInterlocked = totalUpdates / (interlockedResult.ElapsedMs / 1000.0);
        var throughputSingleWriter = totalUpdates / (singleWriterResult.ElapsedMs / 1000.0);

        System.Console.WriteLine();
        System.Console.WriteLine("Throughput:");
        System.Console.WriteLine($"  Interlocked:    {throughputInterlocked / 1_000_000:F2} M ops/sec");
        System.Console.WriteLine($"  Single Writer:  {throughputSingleWriter / 1_000_000:F2} M ops/sec");
        System.Console.WriteLine();

        System.Console.WriteLine("Verification:");
        System.Console.WriteLine($"  Interlocked total:    {interlockedResult.Total:N0}");
        System.Console.WriteLine($"  Single Writer total:  {singleWriterResult.Total:N0}");
        System.Console.WriteLine($"  Expected:             {totalUpdates:N0}");
        System.Console.WriteLine();

        if (interlockedResult.Total == totalUpdates && singleWriterResult.Total == totalUpdates)
        {
            System.Console.WriteLine("[PASS] Both approaches computed correct totals");
        }
        else
        {
            System.Console.WriteLine("[FAIL] Totals don't match expected value");
        }
    }

    private async Task<(double ElapsedMs, long Total)> RunInterlockedApproachAsync(
        int producerCount, long updatesPerProducer)
    {
        long counter = 0;
        var sw = Stopwatch.StartNew();

        var tasks = new Task[producerCount];
        for (var p = 0; p < producerCount; p++)
        {
            tasks[p] = Task.Run(() =>
            {
                for (long i = 0; i < updatesPerProducer; i++)
                {
                    Interlocked.Increment(ref counter);
                }
            });
        }

        await Task.WhenAll(tasks);
        sw.Stop();

        return (sw.Elapsed.TotalMilliseconds, counter);
    }

    private async Task<(double ElapsedMs, long Total)> RunSingleWriterApproachAsync(
        int producerCount, long updatesPerProducer)
    {
        var agentLogger = _loggerFactory.CreateLogger<StatsAgent>();
        await using var agent = new StatsAgent(agentLogger, capacity: 4096);

        var cts = new CancellationTokenSource();
        var agentTask = agent.StartAsync(cts.Token);

        var sw = Stopwatch.StartNew();

        // Launch producers
        var tasks = new Task[producerCount];
        for (var p = 0; p < producerCount; p++)
        {
            tasks[p] = Task.Run(async () =>
            {
                for (long i = 0; i < updatesPerProducer; i++)
                {
                    await agent.SendAsync(new StatsMessage(1));
                }
            });
        }

        await Task.WhenAll(tasks);

        // Stop agent and wait for it to finish processing
        await agent.StopAsync();
        await agentTask;

        sw.Stop();

        return (sw.Elapsed.TotalMilliseconds, agent.Total);
    }
}
