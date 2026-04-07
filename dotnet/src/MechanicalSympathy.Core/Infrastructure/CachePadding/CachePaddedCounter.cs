using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MechanicalSympathy.Core.Infrastructure.CachePadding;

/// <summary>
/// BAD: Counter class with false sharing - both counters share the same cache line.
/// </summary>
/// <remarks>
/// <para>
/// When two threads increment Count1 and Count2 respectively, they will
/// contend for the same cache line, causing the cache coherency protocol
/// to constantly invalidate and transfer the cache line between CPU cores.
/// </para>
/// <para>
/// This results in severe performance degradation (3-10x slower) on multi-core
/// systems compared to the properly padded version.
/// </para>
/// </remarks>
public class BadCounter
{
    /// <summary>First counter value.</summary>
    public long Count1;

    /// <summary>Second counter value - shares cache line with Count1!</summary>
    public long Count2;

    /// <summary>Atomically increments Count1.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementCount1() => Interlocked.Increment(ref Count1);

    /// <summary>Atomically increments Count2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementCount2() => Interlocked.Increment(ref Count2);

    /// <summary>Resets both counters to zero.</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref Count1, 0);
        Interlocked.Exchange(ref Count2, 0);
    }
}

/// <summary>
/// GOOD: Counter class with cache line padding - each counter has its own cache line.
/// </summary>
/// <remarks>
/// <para>
/// By using StructLayout.Explicit and placing each counter at a different
/// cache line offset, we ensure that threads incrementing different counters
/// never contend for the same cache line.
/// </para>
/// <para>
/// The total size is 3 cache lines (192 bytes):
/// - First cache line: padding
/// - Second cache line: Count1
/// - Third cache line: Count2
/// </para>
/// <para>
/// This demonstrates the cache line padding principle from Martin Fowler's
/// mechanical sympathy article: "Pad cache lines with empty data so each
/// contains only one variable."
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Explicit, Size = CacheLineConstants.CacheLineSize * 3)]
public class GoodCounter
{
    /// <summary>
    /// First counter value - occupies its own cache line at offset 64.
    /// </summary>
    [FieldOffset(CacheLineConstants.CacheLineSize)]
    public long Count1;

    /// <summary>
    /// Second counter value - occupies its own cache line at offset 128.
    /// </summary>
    [FieldOffset(CacheLineConstants.CacheLineSize * 2)]
    public long Count2;

    /// <summary>Atomically increments Count1.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementCount1() => Interlocked.Increment(ref Count1);

    /// <summary>Atomically increments Count2.</summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void IncrementCount2() => Interlocked.Increment(ref Count2);

    /// <summary>Resets both counters to zero.</summary>
    public void Reset()
    {
        Interlocked.Exchange(ref Count1, 0);
        Interlocked.Exchange(ref Count2, 0);
    }
}

/// <summary>
/// Multi-counter array with cache line padding for each counter.
/// Useful when you need more than two independent counters.
/// </summary>
public sealed class PaddedCounterArray
{
    private readonly CachePaddedLong[] _counters;

    /// <summary>Number of counters in this array.</summary>
    public int Length => _counters.Length;

    /// <summary>
    /// Creates a new padded counter array with the specified number of counters.
    /// </summary>
    public PaddedCounterArray(int count)
    {
        _counters = new CachePaddedLong[count];
    }

    /// <summary>
    /// Gets or sets a counter value by index.
    /// </summary>
    public long this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get => _counters[index].Value;
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        set => _counters[index].Value = value;
    }

    /// <summary>
    /// Atomically increments the counter at the specified index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Increment(int index) => _counters[index].Increment();

    /// <summary>
    /// Atomically adds a value to the counter at the specified index.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Add(int index, long value) => _counters[index].Add(value);

    /// <summary>
    /// Gets the sum of all counters.
    /// </summary>
    public long Sum()
    {
        long sum = 0;
        for (var i = 0; i < _counters.Length; i++)
        {
            sum += _counters[i].Value;
        }
        return sum;
    }

    /// <summary>
    /// Resets all counters to zero.
    /// </summary>
    public void Reset()
    {
        for (var i = 0; i < _counters.Length; i++)
        {
            _counters[i].Exchange(0);
        }
    }
}
