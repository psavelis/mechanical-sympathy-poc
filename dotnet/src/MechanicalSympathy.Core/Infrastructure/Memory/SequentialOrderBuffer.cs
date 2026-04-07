using System.Runtime.CompilerServices;
using MechanicalSympathy.Domain.Entities;

namespace MechanicalSympathy.Core.Infrastructure.Memory;

/// <summary>
/// Sequential order buffer optimized for cache-friendly iteration.
/// </summary>
/// <remarks>
/// <para>
/// This buffer implements the Predictable Memory Access principle from Martin Fowler's
/// mechanical sympathy article:
/// "Favor algorithms enabling sequential, linear data access over random access patterns."
/// </para>
/// <para>
/// Key properties:
/// </para>
/// <list type="bullet">
/// <item>Contiguous array storage for sequential memory access</item>
/// <item>Pre-allocated capacity to avoid allocations on hot paths</item>
/// <item>Linear iteration pattern maximizes CPU cache utilization</item>
/// <item>CPU prefetcher can predict access patterns</item>
/// </list>
/// <para>
/// Memory accessed recently will probably be accessed again soon (temporal locality).
/// Memory near recently accessed memory will probably be accessed soon (spatial locality).
/// This structure maximizes both forms of locality.
/// </para>
/// </remarks>
public sealed class SequentialOrderBuffer
{
    private readonly Order[] _orders;
    private int _count;

    /// <summary>Current number of orders in the buffer.</summary>
    public int Count => _count;

    /// <summary>Maximum capacity of the buffer.</summary>
    public int Capacity => _orders.Length;

    /// <summary>Whether the buffer is empty.</summary>
    public bool IsEmpty => _count == 0;

    /// <summary>Whether the buffer is full.</summary>
    public bool IsFull => _count >= _orders.Length;

    /// <summary>
    /// Creates a new sequential order buffer with the specified capacity.
    /// </summary>
    /// <param name="capacity">Maximum number of orders to store.</param>
    public SequentialOrderBuffer(int capacity)
    {
        ArgumentOutOfRangeException.ThrowIfLessThan(capacity, 1);
        _orders = new Order[capacity];
    }

    /// <summary>
    /// Adds an order to the buffer.
    /// </summary>
    /// <exception cref="InvalidOperationException">Thrown when buffer is full.</exception>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Add(Order order)
    {
        if (_count >= _orders.Length)
            throw new InvalidOperationException("Buffer is full");

        _orders[_count++] = order;
    }

    /// <summary>
    /// Tries to add an order to the buffer.
    /// </summary>
    /// <returns>True if added, false if buffer is full.</returns>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public bool TryAdd(Order order)
    {
        if (_count >= _orders.Length)
            return false;

        _orders[_count++] = order;
        return true;
    }

    /// <summary>
    /// Gets a span over the valid orders in the buffer.
    /// This enables cache-friendly sequential iteration.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public ReadOnlySpan<Order> AsSpan() => _orders.AsSpan(0, _count);

    /// <summary>
    /// Gets a mutable span for advanced scenarios.
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public Span<Order> AsWritableSpan() => _orders.AsSpan(0, _count);

    /// <summary>
    /// Process all orders with a delegate - cache-friendly sequential access.
    /// </summary>
    public void ForEach(Action<Order> action)
    {
        var span = _orders.AsSpan(0, _count);
        for (var i = 0; i < span.Length; i++)
        {
            action(span[i]);
        }
    }

    /// <summary>
    /// Process all orders with an indexed delegate.
    /// </summary>
    public void ForEach(Action<Order, int> action)
    {
        var span = _orders.AsSpan(0, _count);
        for (var i = 0; i < span.Length; i++)
        {
            action(span[i], i);
        }
    }

    /// <summary>
    /// Sum quantities using sequential access pattern.
    /// Demonstrates predictable memory access for aggregation.
    /// </summary>
    public decimal SumQuantities()
    {
        decimal sum = 0;
        var span = _orders.AsSpan(0, _count);

        // Sequential access - CPU prefetcher can predict next access
        for (var i = 0; i < span.Length; i++)
        {
            sum += span[i].Quantity;
        }

        return sum;
    }

    /// <summary>
    /// Sum prices using sequential access pattern.
    /// </summary>
    public decimal SumPrices()
    {
        decimal sum = 0;
        var span = _orders.AsSpan(0, _count);

        for (var i = 0; i < span.Length; i++)
        {
            sum += span[i].Price;
        }

        return sum;
    }

    /// <summary>
    /// Calculate total value (price * quantity) using sequential access.
    /// </summary>
    public decimal SumValues()
    {
        decimal sum = 0;
        var span = _orders.AsSpan(0, _count);

        for (var i = 0; i < span.Length; i++)
        {
            sum += span[i].Price * span[i].Quantity;
        }

        return sum;
    }

    /// <summary>
    /// Clears the buffer, resetting count to zero.
    /// Does not clear array elements (for performance).
    /// </summary>
    [MethodImpl(MethodImplOptions.AggressiveInlining)]
    public void Clear() => _count = 0;

    /// <summary>
    /// Gets an order by index.
    /// </summary>
    public Order this[int index]
    {
        [MethodImpl(MethodImplOptions.AggressiveInlining)]
        get
        {
            ArgumentOutOfRangeException.ThrowIfNegative(index);
            ArgumentOutOfRangeException.ThrowIfGreaterThanOrEqual(index, _count);
            return _orders[index];
        }
    }

    /// <summary>
    /// Creates an enumerator for foreach loops.
    /// Note: Span-based iteration is more efficient when possible.
    /// </summary>
    public IEnumerator<Order> GetEnumerator()
    {
        for (var i = 0; i < _count; i++)
        {
            yield return _orders[i];
        }
    }
}
