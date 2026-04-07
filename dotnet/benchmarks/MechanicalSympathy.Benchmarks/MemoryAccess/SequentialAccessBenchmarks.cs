using BenchmarkDotNet.Attributes;
using MechanicalSympathy.Benchmarks.Configs;
using MechanicalSympathy.Core.Infrastructure.Memory;
using MechanicalSympathy.Domain.Entities;
using MechanicalSympathy.Domain.ValueObjects;

namespace MechanicalSympathy.Benchmarks.MemoryAccess;

/// <summary>
/// Benchmarks demonstrating the performance difference between
/// sequential and random memory access patterns.
/// </summary>
[Config(typeof(BenchmarkConfig))]
[MemoryDiagnoser]
public class SequentialAccessBenchmarks
{
    private Order[] _ordersArray = null!;
    private SequentialOrderBuffer _ordersBuffer = null!;
    private Dictionary<long, Order> _ordersDict = null!;
    private long[] _randomIndices = null!;

    [Params(1000, 10000, 100000)]
    public int OrderCount { get; set; }

    [GlobalSetup]
    public void Setup()
    {
        _ordersArray = new Order[OrderCount];
        _ordersBuffer = new SequentialOrderBuffer(OrderCount);
        _ordersDict = new Dictionary<long, Order>(OrderCount);

        var random = new Random(42); // Fixed seed for reproducibility
        _randomIndices = new long[OrderCount];

        for (var i = 0; i < OrderCount; i++)
        {
            var order = Order.Create(
                id: i,
                instrumentId: 1,
                side: Side.Buy,
                type: OrderType.Limit,
                price: 100m + (i % 100) * 0.01m,
                quantity: 100,
                clientId: 1
            );

            _ordersArray[i] = order;
            _ordersBuffer.Add(order);
            _ordersDict[i] = order;
            _randomIndices[i] = random.Next(OrderCount);
        }
    }

    /// <summary>
    /// Sequential access through array - baseline for cache-efficient access.
    /// </summary>
    [Benchmark(Baseline = true, Description = "Array Sequential")]
    public decimal ArraySequentialAccess()
    {
        decimal sum = 0;
        for (var i = 0; i < _ordersArray.Length; i++)
        {
            sum += _ordersArray[i].Quantity;
        }
        return sum;
    }

    /// <summary>
    /// Sequential access using Span - same cache efficiency, bounds-check elimination.
    /// </summary>
    [Benchmark(Description = "Array Span")]
    public decimal ArraySpanAccess()
    {
        decimal sum = 0;
        var span = _ordersArray.AsSpan();
        for (var i = 0; i < span.Length; i++)
        {
            sum += span[i].Quantity;
        }
        return sum;
    }

    /// <summary>
    /// Sequential access through SequentialOrderBuffer.
    /// </summary>
    [Benchmark(Description = "Buffer Sequential")]
    public decimal BufferSequentialAccess()
    {
        return _ordersBuffer.SumQuantities();
    }

    /// <summary>
    /// Random access through Dictionary - poor cache utilization.
    /// Expected: Much slower due to cache misses.
    /// </summary>
    [Benchmark(Description = "Dictionary Random")]
    public decimal DictionaryRandomAccess()
    {
        decimal sum = 0;

        // Random access pattern - poor cache utilization
        for (var i = 0; i < OrderCount; i++)
        {
            var key = _randomIndices[i];
            sum += _ordersDict[key].Quantity;
        }

        return sum;
    }

    /// <summary>
    /// Sequential access through List - similar to array but with overhead.
    /// </summary>
    [Benchmark(Description = "List Sequential")]
    public decimal ListSequentialAccess()
    {
        decimal sum = 0;
        var list = _ordersDict.Values.ToList();

        for (var i = 0; i < list.Count; i++)
        {
            sum += list[i].Quantity;
        }

        return sum;
    }

    /// <summary>
    /// Foreach enumeration - additional allocation from enumerator.
    /// </summary>
    [Benchmark(Description = "Foreach Enumeration")]
    public decimal ForeachAccess()
    {
        decimal sum = 0;
        foreach (var order in _ordersArray)
        {
            sum += order.Quantity;
        }
        return sum;
    }
}
