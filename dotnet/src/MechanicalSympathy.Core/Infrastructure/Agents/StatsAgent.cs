using Microsoft.Extensions.Logging;

namespace MechanicalSympathy.Core.Infrastructure.Agents;

/// <summary>
/// A simple statistics aggregation agent demonstrating the Single Writer Principle.
/// </summary>
/// <remarks>
/// <para>
/// This agent aggregates statistics from multiple producer threads without any locks.
/// The Single Writer Principle ensures that only this agent's thread modifies the
/// internal state, eliminating all race conditions.
/// </para>
/// <para>
/// This is equivalent to the StatsActor pattern from Martin Fowler's article:
/// "Replace mutexes with asynchronous messaging to an 'actor' thread owning the resource."
/// </para>
/// </remarks>
public sealed class StatsAgent : AgentBase<StatsMessage>
{
    // All state is only modified by the single reader thread - no locks needed!
    private long _total;
    private long _count;
    private long _min = long.MaxValue;
    private long _max = long.MinValue;
    private double _sum;
    private double _sumOfSquares;

    /// <summary>Running total of all values.</summary>
    public long Total => Volatile.Read(ref _total);

    /// <summary>Number of values received.</summary>
    public long Count => Volatile.Read(ref _count);

    /// <summary>Minimum value seen.</summary>
    public long Min => Volatile.Read(ref _min);

    /// <summary>Maximum value seen.</summary>
    public long Max => Volatile.Read(ref _max);

    /// <summary>Average of all values.</summary>
    public double Average => _count > 0 ? _sum / _count : 0;

    /// <summary>Standard deviation of all values.</summary>
    public double StandardDeviation
    {
        get
        {
            if (_count < 2) return 0;
            var variance = (_sumOfSquares - (_sum * _sum / _count)) / (_count - 1);
            return Math.Sqrt(Math.Max(0, variance));
        }
    }

    /// <summary>
    /// Creates a new statistics agent.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    /// <param name="capacity">Channel capacity for backpressure.</param>
    public StatsAgent(ILogger<StatsAgent> logger, int capacity = 4096)
        : base(capacity, logger, nameof(StatsAgent))
    {
    }

    /// <summary>
    /// Processes a single statistics message.
    /// This runs on the agent's dedicated thread - no locks needed!
    /// </summary>
    protected override ValueTask ProcessMessageAsync(StatsMessage message, CancellationToken cancellationToken)
    {
        // All these mutations happen on a single thread - zero contention
        _total += message.Value;
        _count++;
        _sum += message.Value;
        _sumOfSquares += (double)message.Value * message.Value;

        if (message.Value < _min) _min = message.Value;
        if (message.Value > _max) _max = message.Value;

        return ValueTask.CompletedTask;
    }

    /// <summary>
    /// Gets a snapshot of current statistics.
    /// </summary>
    public StatsSnapshot GetSnapshot()
    {
        return new StatsSnapshot(
            Total: Total,
            Count: Count,
            Min: Count > 0 ? Min : 0,
            Max: Count > 0 ? Max : 0,
            Average: Average,
            StandardDeviation: StandardDeviation
        );
    }

    /// <summary>
    /// Resets all statistics.
    /// NOTE: Call this only when no producers are active, or accept potential inconsistency.
    /// </summary>
    public void Reset()
    {
        _total = 0;
        _count = 0;
        _min = long.MaxValue;
        _max = long.MinValue;
        _sum = 0;
        _sumOfSquares = 0;
    }
}

/// <summary>
/// Message type for statistics updates.
/// </summary>
/// <param name="Value">The value to aggregate.</param>
public readonly record struct StatsMessage(long Value);

/// <summary>
/// Immutable snapshot of statistics at a point in time.
/// </summary>
/// <param name="Total">Sum of all values.</param>
/// <param name="Count">Number of values.</param>
/// <param name="Min">Minimum value.</param>
/// <param name="Max">Maximum value.</param>
/// <param name="Average">Mean value.</param>
/// <param name="StandardDeviation">Standard deviation.</param>
public readonly record struct StatsSnapshot(
    long Total,
    long Count,
    long Min,
    long Max,
    double Average,
    double StandardDeviation
);
