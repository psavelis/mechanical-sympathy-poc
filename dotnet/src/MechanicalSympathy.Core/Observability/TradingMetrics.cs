using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace MechanicalSympathy.Core.Observability;

/// <summary>
/// Trading-specific metrics for observability.
/// </summary>
/// <remarks>
/// <para>
/// From Martin Fowler's article:
/// "Prioritize observability before optimization - establish SLIs, SLOs, and SLAs
/// before implementing these principles to identify optimization targets and know when to stop."
/// </para>
/// <para>
/// These metrics help:
/// </para>
/// <list type="bullet">
/// <item>Identify bottlenecks before optimizing</item>
/// <item>Measure the impact of mechanical sympathy optimizations</item>
/// <item>Set and monitor performance SLOs</item>
/// <item>Know when optimization is "enough"</item>
/// </list>
/// </remarks>
public sealed class TradingMetrics
{
    private readonly Counter<long> _ordersReceived;
    private readonly Counter<long> _ordersMatched;
    private readonly Counter<long> _tradesExecuted;
    private readonly Histogram<double> _matchingLatencyUs;
    private readonly Histogram<int> _batchSize;
    private readonly Histogram<double> _orderBookDepth;

    /// <summary>
    /// Creates a new TradingMetrics instance with the specified meter.
    /// </summary>
    /// <param name="meter">The meter to use for creating instruments.</param>
    public TradingMetrics(Meter meter)
    {
        _ordersReceived = meter.CreateCounter<long>(
            "trading.orders.received",
            unit: "{orders}",
            description: "Total orders received by the trading system");

        _ordersMatched = meter.CreateCounter<long>(
            "trading.orders.matched",
            unit: "{orders}",
            description: "Orders that resulted in at least one match");

        _tradesExecuted = meter.CreateCounter<long>(
            "trading.trades.executed",
            unit: "{trades}",
            description: "Total trades executed");

        _matchingLatencyUs = meter.CreateHistogram<double>(
            "trading.matching.latency",
            unit: "us",
            description: "Order matching latency in microseconds",
            advice: new InstrumentAdvice<double>
            {
                // Common latency buckets in microseconds
                HistogramBucketBoundaries = [1, 5, 10, 25, 50, 100, 250, 500, 1000, 2500, 5000, 10000]
            });

        _batchSize = meter.CreateHistogram<int>(
            "trading.batch.size",
            unit: "{items}",
            description: "Natural batch sizes processed");

        _orderBookDepth = meter.CreateHistogram<double>(
            "trading.orderbook.depth",
            unit: "{orders}",
            description: "Order book depth (total orders on both sides)");
    }

    /// <summary>Records that an order was received.</summary>
    public void RecordOrderReceived() => _ordersReceived.Add(1);

    /// <summary>Records that an order was matched.</summary>
    public void RecordOrderMatched() => _ordersMatched.Add(1);

    /// <summary>Records that a trade was executed.</summary>
    public void RecordTradeExecuted() => _tradesExecuted.Add(1);

    /// <summary>Records multiple trades executed.</summary>
    public void RecordTradesExecuted(int count) => _tradesExecuted.Add(count);

    /// <summary>Records matching latency in microseconds.</summary>
    public void RecordMatchingLatencyUs(double microseconds) => _matchingLatencyUs.Record(microseconds);

    /// <summary>Records matching latency from a Stopwatch.</summary>
    public void RecordMatchingLatency(Stopwatch stopwatch) =>
        _matchingLatencyUs.Record(stopwatch.Elapsed.TotalMicroseconds);

    /// <summary>Records the size of a processed batch.</summary>
    public void RecordBatchSize(int size) => _batchSize.Record(size);

    /// <summary>Records the current order book depth.</summary>
    public void RecordOrderBookDepth(int bidCount, int askCount) =>
        _orderBookDepth.Record(bidCount + askCount);
}

/// <summary>
/// Activity source for distributed tracing in the trading system.
/// </summary>
public static class TradingActivitySource
{
    /// <summary>
    /// The activity source for trading operations.
    /// </summary>
    public static readonly ActivitySource Source = new("MechanicalSympathy.Trading", "1.0.0");

    /// <summary>
    /// Starts an activity for order placement.
    /// </summary>
    public static Activity? StartPlaceOrder(long orderId, long instrumentId)
    {
        return Source.StartActivity("PlaceOrder")?
            .SetTag("order.id", orderId)
            .SetTag("instrument.id", instrumentId);
    }

    /// <summary>
    /// Starts an activity for order matching.
    /// </summary>
    public static Activity? StartMatchOrder(long orderId)
    {
        return Source.StartActivity("MatchOrder")?
            .SetTag("order.id", orderId);
    }

    /// <summary>
    /// Starts an activity for trade execution.
    /// </summary>
    public static Activity? StartExecuteTrade(long tradeId)
    {
        return Source.StartActivity("ExecuteTrade")?
            .SetTag("trade.id", tradeId);
    }
}
