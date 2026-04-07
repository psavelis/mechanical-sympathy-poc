namespace MechanicalSympathy.Core.Infrastructure.Batching;

/// <summary>
/// Configuration options for natural batching.
/// </summary>
public sealed record BatchingOptions
{
    /// <summary>
    /// Maximum number of items to include in a single batch.
    /// Once this size is reached, the batch is processed immediately.
    /// Default: 100
    /// </summary>
    public int MaxBatchSize { get; init; } = 100;

    /// <summary>
    /// Capacity of the input channel.
    /// This controls backpressure - producers will wait when the channel is full.
    /// Default: 4096
    /// </summary>
    public int ChannelCapacity { get; init; } = 4096;

    /// <summary>
    /// Whether to allow single-item batches.
    /// When true, a single item can be processed immediately if no more items are available.
    /// When false, waits for at least MinBatchSize items (slower but more efficient for batching).
    /// Default: true (for low latency)
    /// </summary>
    public bool AllowSingleItemBatch { get; init; } = true;

    /// <summary>
    /// Minimum number of items before processing a batch.
    /// Only used when AllowSingleItemBatch is false.
    /// Default: 1
    /// </summary>
    public int MinBatchSize { get; init; } = 1;

    /// <summary>
    /// Creates default options optimized for low latency.
    /// </summary>
    public static BatchingOptions LowLatency => new()
    {
        MaxBatchSize = 50,
        ChannelCapacity = 2048,
        AllowSingleItemBatch = true
    };

    /// <summary>
    /// Creates options optimized for high throughput.
    /// </summary>
    public static BatchingOptions HighThroughput => new()
    {
        MaxBatchSize = 500,
        ChannelCapacity = 8192,
        AllowSingleItemBatch = false,
        MinBatchSize = 10
    };
}
