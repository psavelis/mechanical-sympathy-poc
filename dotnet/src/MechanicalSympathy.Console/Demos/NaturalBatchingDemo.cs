using System.Diagnostics;
using MechanicalSympathy.Core.Infrastructure.Batching;
using Microsoft.Extensions.Logging;

namespace MechanicalSympathy.Console.Demos;

/// <summary>
/// Demonstrates the Natural Batching pattern for optimal latency and throughput.
/// </summary>
public sealed class NaturalBatchingDemo
{
    private readonly ILogger<NaturalBatchingDemo> _logger;
    private readonly ILoggerFactory _loggerFactory;

    public NaturalBatchingDemo(ILogger<NaturalBatchingDemo> logger, ILoggerFactory loggerFactory)
    {
        _logger = logger;
        _loggerFactory = loggerFactory;
    }

    public async Task RunAsync()
    {
        System.Console.WriteLine("┌──────────────────────────────────────────────────────────────────────┐");
        System.Console.WriteLine("│ Demo 3: Natural Batching Pattern                                    │");
        System.Console.WriteLine("├──────────────────────────────────────────────────────────────────────┤");
        System.Console.WriteLine("│ Begin immediately on data availability. Complete when max size      │");
        System.Console.WriteLine("│ reached OR queue empties. No artificial delays.                     │");
        System.Console.WriteLine("└──────────────────────────────────────────────────────────────────────┘");
        System.Console.WriteLine();

        const int totalItems = 1_000_000;
        const int producerCount = 4;
        const int maxBatchSize = 100;

        System.Console.WriteLine($"Configuration:");
        System.Console.WriteLine($"  Total items:    {totalItems:N0}");
        System.Console.WriteLine($"  Producers:      {producerCount}");
        System.Console.WriteLine($"  Max batch size: {maxBatchSize}");
        System.Console.WriteLine();

        var processedItems = 0L;
        var batchCount = 0L;
        var batchSizes = new List<int>();

        var batcherLogger = _loggerFactory.CreateLogger<NaturalBatcher<int>>();
        var options = new BatchingOptions
        {
            MaxBatchSize = maxBatchSize,
            ChannelCapacity = 4096,
            AllowSingleItemBatch = true
        };

        await using var batcher = new NaturalBatcher<int>(
            options,
            async (batch, ct) =>
            {
                // Simulate some processing work
                Interlocked.Add(ref processedItems, batch.Count);
                Interlocked.Increment(ref batchCount);

                lock (batchSizes)
                {
                    batchSizes.Add(batch.Count);
                }

                await Task.Yield();
            },
            batcherLogger);

        var cts = new CancellationTokenSource();
        var batcherTask = batcher.RunAsync(cts.Token);

        var sw = Stopwatch.StartNew();

        // Launch producers
        var itemsPerProducer = totalItems / producerCount;
        var producerTasks = new Task[producerCount];

        for (var p = 0; p < producerCount; p++)
        {
            producerTasks[p] = Task.Run(async () =>
            {
                for (var i = 0; i < itemsPerProducer; i++)
                {
                    await batcher.EnqueueAsync(i);
                }
            });
        }

        await Task.WhenAll(producerTasks);

        // Wait for all items to be processed
        while (Interlocked.Read(ref processedItems) < totalItems)
        {
            await Task.Delay(10);
        }

        await batcher.DisposeAsync();
        await cts.CancelAsync();

        try
        {
            await batcherTask;
        }
        catch (OperationCanceledException)
        {
            // Expected
        }

        sw.Stop();

        // Calculate statistics
        var avgBatchSize = batchSizes.Count > 0 ? batchSizes.Average() : 0;
        var minBatchSize = batchSizes.Count > 0 ? batchSizes.Min() : 0;
        var maxBatchSizeActual = batchSizes.Count > 0 ? batchSizes.Max() : 0;
        var throughput = totalItems / (sw.Elapsed.TotalMilliseconds / 1000.0);

        System.Console.WriteLine("Results:");
        System.Console.WriteLine($"  Time elapsed:      {sw.Elapsed.TotalMilliseconds:F1} ms");
        System.Console.WriteLine($"  Items processed:   {processedItems:N0}");
        System.Console.WriteLine($"  Batches processed: {batchCount:N0}");
        System.Console.WriteLine($"  Throughput:        {throughput / 1_000_000:F2} M items/sec");
        System.Console.WriteLine();
        System.Console.WriteLine("Batch Size Distribution:");
        System.Console.WriteLine($"  Average: {avgBatchSize:F1}");
        System.Console.WriteLine($"  Min:     {minBatchSize}");
        System.Console.WriteLine($"  Max:     {maxBatchSizeActual}");
        System.Console.WriteLine();

        // Analyze batch distribution
        var smallBatches = batchSizes.Count(s => s < 10);
        var mediumBatches = batchSizes.Count(s => s >= 10 && s < maxBatchSize);
        var fullBatches = batchSizes.Count(s => s >= maxBatchSize);

        System.Console.WriteLine("Batch Distribution:");
        System.Console.WriteLine($"  Small (<10):       {smallBatches,6} ({100.0 * smallBatches / batchSizes.Count:F1}%)");
        System.Console.WriteLine($"  Medium (10-{maxBatchSize - 1}):    {mediumBatches,6} ({100.0 * mediumBatches / batchSizes.Count:F1}%)");
        System.Console.WriteLine($"  Full ({maxBatchSize}):        {fullBatches,6} ({100.0 * fullBatches / batchSizes.Count:F1}%)");
        System.Console.WriteLine();

        System.Console.WriteLine("[INFO] Natural batching adapts to load:");
        System.Console.WriteLine("       - Low load: small batches, low latency");
        System.Console.WriteLine("       - High load: full batches, high throughput");
    }
}
