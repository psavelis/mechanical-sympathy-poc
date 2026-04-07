using MechanicalSympathy.Domain.ValueObjects;

namespace MechanicalSympathy.Domain.Entities;

/// <summary>
/// Order book for a single instrument.
/// Maintains bid and ask price levels in sorted order.
/// Optimized for sequential access during price level iteration.
/// </summary>
public sealed class OrderBook
{
    // Sorted by price: descending for bids (best bid = highest price first)
    private readonly SortedDictionary<decimal, PriceLevel> _bids =
        new(Comparer<decimal>.Create((a, b) => b.CompareTo(a)));

    // Sorted by price: ascending for asks (best ask = lowest price first)
    private readonly SortedDictionary<decimal, PriceLevel> _asks = new();

    /// <summary>Instrument identifier for this order book.</summary>
    public long InstrumentId { get; }

    /// <summary>Best bid price (highest buy price).</summary>
    public decimal? BestBid => _bids.Count > 0 ? _bids.First().Key : null;

    /// <summary>Best ask price (lowest sell price).</summary>
    public decimal? BestAsk => _asks.Count > 0 ? _asks.First().Key : null;

    /// <summary>Spread between best ask and best bid.</summary>
    public decimal? Spread => BestBid.HasValue && BestAsk.HasValue
        ? BestAsk.Value - BestBid.Value
        : null;

    /// <summary>All bid price levels (sorted by price descending).</summary>
    public IEnumerable<PriceLevel> Bids => _bids.Values;

    /// <summary>All ask price levels (sorted by price ascending).</summary>
    public IEnumerable<PriceLevel> Asks => _asks.Values;

    /// <summary>Total number of bid orders.</summary>
    public int TotalBidOrders => _bids.Values.Sum(l => l.OrderCount);

    /// <summary>Total number of ask orders.</summary>
    public int TotalAskOrders => _asks.Values.Sum(l => l.OrderCount);

    /// <summary>
    /// Creates a new order book for the specified instrument.
    /// </summary>
    public OrderBook(long instrumentId)
    {
        InstrumentId = instrumentId;
    }

    /// <summary>
    /// Adds an order to the appropriate side of the book.
    /// </summary>
    public void AddOrder(Order order)
    {
        var book = order.Side == Side.Buy ? _bids : _asks;

        if (!book.TryGetValue(order.Price, out var level))
        {
            level = new PriceLevel(order.Price);
            book[order.Price] = level;
        }

        level.AddOrder(order);
    }

    /// <summary>
    /// Removes an order from the book.
    /// </summary>
    public bool RemoveOrder(Order order)
    {
        var book = order.Side == Side.Buy ? _bids : _asks;

        if (!book.TryGetValue(order.Price, out var level))
            return false;

        var removed = level.RemoveOrder(order);

        if (level.IsEmpty)
            book.Remove(order.Price);

        return removed;
    }

    /// <summary>
    /// Gets the opposite side of the book for matching.
    /// Buy orders match against asks, sell orders match against bids.
    /// </summary>
    public IEnumerable<PriceLevel> GetOppositeSide(Side side)
    {
        return side == Side.Buy ? _asks.Values : _bids.Values;
    }

    /// <summary>
    /// Gets all price levels on the specified side.
    /// </summary>
    public IEnumerable<PriceLevel> GetSide(Side side)
    {
        return side == Side.Buy ? _bids.Values : _asks.Values;
    }

    /// <summary>
    /// Clears all orders from the book.
    /// </summary>
    public void Clear()
    {
        _bids.Clear();
        _asks.Clear();
    }

    /// <summary>
    /// Removes empty price levels (cleanup after matching).
    /// </summary>
    public void PruneEmptyLevels()
    {
        PruneSide(_bids);
        PruneSide(_asks);
    }

    private static void PruneSide(SortedDictionary<decimal, PriceLevel> side)
    {
        var emptyPrices = side.Where(kvp => kvp.Value.IsEmpty)
                              .Select(kvp => kvp.Key)
                              .ToList();

        foreach (var price in emptyPrices)
        {
            side.Remove(price);
        }
    }
}
