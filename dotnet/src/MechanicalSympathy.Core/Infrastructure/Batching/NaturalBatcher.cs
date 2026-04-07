using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Logging;

namespace MechanicalSympathy.Core.Infrastructure.Batching;

/// <summary>
/// Natural Batching implementation following Martin Fowler's mechanical sympathy principles.
/// </summary>
/// <remarks>
/// <para>
/// Natural Batching achieves optimal latency AND throughput by:
/// </para>
/// <list type="bullet">
/// <item>Beginning immediately when data is available (no artificial delays)</item>
/// <item>Growing the batch while more data arrives during processing setup</item>
/// <item>Completing the batch when either max size is reached OR the queue empties</item>
/// </list>
/// <para>
/// From Martin Fowler's article:
/// "When a single writer processes batches, begin immediately upon data availability.
/// Complete batches when either maximum size is reached or the queue empties."
/// </para>
/// <para>
/// This achieves 2x better performance than timeout-based batching strategies:
/// - Best-case latency: ~100us (single item processed immediately)
/// - Worst-case latency: ~200us (full batch processed)
/// - vs timeout-based: 200us/400us
/// </para>
/// </remarks>
/// <typeparam name="T">The type of items to batch.</typeparam>
public sealed class NaturalBatcher<T> : IAsyncDisposable
{
    private readonly Channel<T> _inputChannel;
    private readonly Func<IReadOnlyList<T>, CancellationToken, ValueTask> _processBatch;
    private readonly BatchingOptions _options;
    private readonly ILogger _logger;
    private readonly List<T> _currentBatch;
    private readonly Stopwatch _batchTimer = new();

    private long _totalItemsProcessed;
    private long _totalBatchesProcessed;

    /// <summary>Total items processed by this batcher.</summary>
    public long TotalItemsProcessed => Volatile.Read(ref _totalItemsProcessed);

    /// <summary>Total batches processed by this batcher.</summary>
    public long TotalBatchesProcessed => Volatile.Read(ref _totalBatchesProcessed);

    /// <summary>Average batch size (items per batch).</summary>
    public double AverageBatchSize => _totalBatchesProcessed > 0
        ? (double)_totalItemsProcessed / _totalBatchesProcessed
        : 0;

    /// <summary>
    /// Creates a new natural batcher.
    /// </summary>
    /// <param name="options">Batching configuration options.</param>
    /// <param name="processBatch">Delegate to process each batch of items.</param>
    /// <param name="logger">Logger instance.</param>
    public NaturalBatcher(
        BatchingOptions options,
        Func<IReadOnlyList<T>, CancellationToken, ValueTask> processBatch,
        ILogger<NaturalBatcher<T>> logger)
    {
        _options = options;
        _processBatch = processBatch;
        _logger = logger;
        _currentBatch = new List<T>(options.MaxBatchSize);

        _inputChannel = Channel.CreateBounded<T>(new BoundedChannelOptions(options.ChannelCapacity)
        {
            SingleWriter = false,      // Multiple producers
            SingleReader = true,       // Single Writer Principle for batch state
            FullMode = BoundedChannelFullMode.Wait
        });
    }

    /// <summary>
    /// Enqueues an item for batched processing.
    /// </summary>
    public ValueTask EnqueueAsync(T item, CancellationToken cancellationToken = default)
    {
        return _inputChannel.Writer.WriteAsync(item, cancellationToken);
    }

    /// <summary>
    /// Tries to enqueue an item without waiting.
    /// </summary>
    public bool TryEnqueue(T item)
    {
        return _inputChannel.Writer.TryWrite(item);
    }

    /// <summary>
    /// Runs the natural batching loop until cancelled.
    /// </summary>
    public async Task RunAsync(CancellationToken cancellationToken)
    {
        var reader = _inputChannel.Reader;

        _logger.LogInformation("NaturalBatcher starting with MaxBatchSize={MaxBatchSize}",
            _options.MaxBatchSize);

        while (!cancellationToken.IsCancellationRequested)
        {
            // NATURAL BATCHING STEP 1: Wait for at least one item
            // This is where we block when there's no work
            if (!await reader.WaitToReadAsync(cancellationToken))
                break; // Channel completed

            _currentBatch.Clear();
            _batchTimer.Restart();

            // NATURAL BATCHING STEP 2: Read all immediately available items
            // This is the key insight - we grab everything that's ready NOW
            // without any artificial delays
            while (_currentBatch.Count < _options.MaxBatchSize &&
                   reader.TryRead(out var item))
            {
                _currentBatch.Add(item);
            }

            // NATURAL BATCHING STEP 3: Process the batch
            // Batch size naturally adapts to load:
            // - Low load: small batches, low latency
            // - High load: large batches, high throughput
            if (_currentBatch.Count > 0)
            {
                if (_options.AllowSingleItemBatch || _currentBatch.Count >= _options.MinBatchSize)
                {
                    _logger.LogDebug("Processing batch of {Count} items", _currentBatch.Count);

                    await _processBatch(_currentBatch, cancellationToken);

                    Interlocked.Add(ref _totalItemsProcessed, _currentBatch.Count);
                    Interlocked.Increment(ref _totalBatchesProcessed);

                    _batchTimer.Stop();
                    _logger.LogDebug("Batch processed in {ElapsedUs}us",
                        _batchTimer.Elapsed.TotalMicroseconds);
                }
            }
        }

        // Drain remaining items on shutdown
        await DrainAsync(cancellationToken);

        _logger.LogInformation(
            "NaturalBatcher stopped. Processed {Items} items in {Batches} batches (avg {Avg:F1} items/batch)",
            TotalItemsProcessed, TotalBatchesProcessed, AverageBatchSize);
    }

    /// <summary>
    /// Drains any remaining items after shutdown signal.
    /// </summary>
    private async ValueTask DrainAsync(CancellationToken cancellationToken)
    {
        _currentBatch.Clear();

        while (_inputChannel.Reader.TryRead(out var item))
        {
            _currentBatch.Add(item);

            if (_currentBatch.Count >= _options.MaxBatchSize)
            {
                await _processBatch(_currentBatch, cancellationToken);
                Interlocked.Add(ref _totalItemsProcessed, _currentBatch.Count);
                Interlocked.Increment(ref _totalBatchesProcessed);
                _currentBatch.Clear();
            }
        }

        // Process any remaining items
        if (_currentBatch.Count > 0)
        {
            await _processBatch(_currentBatch, cancellationToken);
            Interlocked.Add(ref _totalItemsProcessed, _currentBatch.Count);
            Interlocked.Increment(ref _totalBatchesProcessed);
        }
    }

    /// <summary>
    /// Signals that no more items will be added and disposes the batcher.
    /// </summary>
    public async ValueTask DisposeAsync()
    {
        _inputChannel.Writer.Complete();
        GC.SuppressFinalize(this);
    }
}
