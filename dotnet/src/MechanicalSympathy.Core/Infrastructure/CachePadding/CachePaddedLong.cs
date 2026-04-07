using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;

namespace MechanicalSympathy.Core.Infrastructure.CachePadding;

/// <summary>
/// A long value padded to occupy an entire cache line, preventing false sharing.
/// </summary>
/// <remarks>
/// <para>
/// This structure places the value in the middle of a 128-byte region,
/// ensuring it cannot share a cache line with adjacent memory, regardless
/// of how this struct is aligned in memory.
/// </para>
/// <para>
/// The total size is 2 cache lines (128 bytes) to protect against both
/// forward and backward false sharing.
/// </para>
/// </remarks>
[StructLayout(LayoutKind.Explicit, Size = CacheLineConstants.CacheLineSize * 2)]
public struct CachePaddedLong
{
    /// <summary>
    /// The actual value, placed at offset 64 (start of second cache line).
    /// This ensures the value has its own cache line with padding on both sides.
    /// </summary>
    [FieldOffset(CacheLineConstants.CacheLineSize)]
    public long Value;

    /// <summary>
    /// Creates a new cache-padded long with the specified initial value.
    /// </summary>
    public CachePaddedLong(long value) => Value = value;

    /// <summary>
    /// Atomically increments the value and returns the new value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Increment() => Interlocked.Increment(ref Value);

    /// <summary>
    /// Atomically decrements the value and returns the new value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Decrement() => Interlocked.Decrement(ref Value);

    /// <summary>
    /// Atomically adds a value and returns the new value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Add(long value) => Interlocked.Add(ref Value, value);

    /// <summary>
    /// Atomically exchanges the value and returns the original value.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public long Exchange(long newValue) => Interlocked.Exchange(ref Value, newValue);

    /// <summary>
    /// Implicit conversion to long for convenient reading.
    /// </summary>
    public static implicit operator long(CachePaddedLong padded) => Volatile.Read(ref padded.Value);

    /// <inheritdoc />
    public override string ToString() => Value.ToString();
}
