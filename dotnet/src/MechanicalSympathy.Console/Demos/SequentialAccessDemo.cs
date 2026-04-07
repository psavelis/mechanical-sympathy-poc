using System.Diagnostics;
using MechanicalSympathy.Core.Infrastructure.Memory;
using MechanicalSympathy.Domain.Entities;
using MechanicalSympathy.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace MechanicalSympathy.Console.Demos;

/// <summary>
/// Demonstrates the performance difference between sequential and random memory access.
/// </summary>
public sealed class SequentialAccessDemo
{
    private readonly ILogger<SequentialAccessDemo> _logger;

    public SequentialAccessDemo(ILogger<SequentialAccessDemo> logger)
    {
        _logger = logger;
    }

    public async Task RunAsync()
    {
        System.Console.WriteLine("┌──────────────────────────────────────────────────────────────────────┐");
        System.Console.WriteLine("│ Demo 4: Sequential vs Random Memory Access                          │");
        System.Console.WriteLine("├──────────────────────────────────────────────────────────────────────┤");
        System.Console.WriteLine("│ Sequential access enables CPU cache prefetching. Random access      │");
        System.Console.WriteLine("│ causes cache misses and memory stalls.                              │");
        System.Console.WriteLine("└──────────────────────────────────────────────────────────────────────┘");
        System.Console.WriteLine();

        const int orderCount = 100_000;
        const int iterations = 100;

        System.Console.WriteLine($"Configuration:");
        System.Console.WriteLine($"  Order count:  {orderCount:N0}");
        System.Console.WriteLine($"  Iterations:   {iterations}");
        System.Console.WriteLine();

        // Create test data
        var orders = CreateOrders(orderCount);
        var buffer = CreateBuffer(orderCount);
        var dictionary = CreateDictionary(orders);
        var randomIndices = CreateRandomIndices(orderCount);

        // Warmup
        _ = SumQuantitiesArray(orders);
        _ = SumQuantitiesBuffer(buffer);
        _ = SumQuantitiesDictionaryRandom(dictionary, randomIndices);

        await Task.Yield();

        // Test Array Sequential
        var arrayTimes = new List<double>();
        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            _ = SumQuantitiesArray(orders);
            sw.Stop();
            arrayTimes.Add(sw.Elapsed.TotalMicroseconds);
        }

        // Test Buffer Sequential (Span)
        var bufferTimes = new List<double>();
        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            _ = SumQuantitiesBuffer(buffer);
            sw.Stop();
            bufferTimes.Add(sw.Elapsed.TotalMicroseconds);
        }

        // Test Dictionary Random
        var dictTimes = new List<double>();
        for (var i = 0; i < iterations; i++)
        {
            var sw = Stopwatch.StartNew();
            _ = SumQuantitiesDictionaryRandom(dictionary, randomIndices);
            sw.Stop();
            dictTimes.Add(sw.Elapsed.TotalMicroseconds);
        }

        // Calculate statistics
        var arrayAvg = arrayTimes.Average();
        var bufferAvg = bufferTimes.Average();
        var dictAvg = dictTimes.Average();

        var arrayMin = arrayTimes.Min();
        var bufferMin = bufferTimes.Min();
        var dictMin = dictTimes.Min();

        System.Console.WriteLine("Results (microseconds per iteration):");
        System.Console.WriteLine($"  {"Access Pattern",-25} {"Avg",12} {"Min",12} {"Relative",12}");
        System.Console.WriteLine($"  {new string('-', 61)}");
        System.Console.WriteLine($"  {"Array Sequential",-25} {arrayAvg,12:F1} {arrayMin,12:F1} {1.0,12:F2}x");
        System.Console.WriteLine($"  {"Buffer Span",-25} {bufferAvg,12:F1} {bufferMin,12:F1} {bufferAvg / arrayAvg,12:F2}x");
        System.Console.WriteLine($"  {"Dictionary Random",-25} {dictAvg,12:F1} {dictMin,12:F1} {dictAvg / arrayAvg,12:F2}x");
        System.Console.WriteLine();

        var speedup = dictAvg / arrayAvg;
        System.Console.WriteLine($"Sequential access is {speedup:F1}x faster than random access");
        System.Console.WriteLine();

        System.Console.WriteLine("[INFO] Why sequential access is faster:");
        System.Console.WriteLine("       1. CPU prefetcher predicts next access");
        System.Console.WriteLine("       2. Cache lines are fully utilized");
        System.Console.WriteLine("       3. No cache misses from jumping around memory");
    }

    private static Order[] CreateOrders(int count)
    {
        var orders = new Order[count];
        for (var i = 0; i < count; i++)
        {
            orders[i] = Order.Create(
                id: i,
                instrumentId: 1,
                side: Side.Buy,
                type: OrderType.Limit,
                price: 100m + (i % 100) * 0.01m,
                quantity: 100,
                clientId: 1
            );
        }
        return orders;
    }

    private static SequentialOrderBuffer CreateBuffer(int count)
    {
        var buffer = new SequentialOrderBuffer(count);
        for (var i = 0; i < count; i++)
        {
            buffer.Add(Order.Create(
                id: i,
                instrumentId: 1,
                side: Side.Buy,
                type: OrderType.Limit,
                price: 100m + (i % 100) * 0.01m,
                quantity: 100,
                clientId: 1
            ));
        }
        return buffer;
    }

    private static Dictionary<long, Order> CreateDictionary(Order[] orders)
    {
        var dict = new Dictionary<long, Order>(orders.Length);
        foreach (var order in orders)
        {
            dict[order.Id] = order;
        }
        return dict;
    }

    private static long[] CreateRandomIndices(int count)
    {
        var indices = new long[count];
        var random = new Random(42); // Fixed seed for reproducibility

        for (var i = 0; i < count; i++)
        {
            indices[i] = random.Next(count);
        }

        return indices;
    }

    private static decimal SumQuantitiesArray(Order[] orders)
    {
        decimal sum = 0;
        for (var i = 0; i < orders.Length; i++)
        {
            sum += orders[i].Quantity;
        }
        return sum;
    }

    private static decimal SumQuantitiesBuffer(SequentialOrderBuffer buffer)
    {
        return buffer.SumQuantities();
    }

    private static decimal SumQuantitiesDictionaryRandom(Dictionary<long, Order> dict, long[] indices)
    {
        decimal sum = 0;
        for (var i = 0; i < indices.Length; i++)
        {
            sum += dict[indices[i]].Quantity;
        }
        return sum;
    }
}
