namespace MechanicalSympathy.Domain.Entities;

/// <summary>
/// Represents all orders at a specific price point.
/// Uses a List for sequential iteration during matching (cache-friendly).
/// Orders are stored in FIFO order for price-time priority.
/// </summary>
public sealed class PriceLevel
{
    private readonly List<Order> _orders;

    /// <summary>The price for this level.</summary>
    public decimal Price { get; }

    /// <summary>Read-only view of orders at this level.</summary>
    public IReadOnlyList<Order> Orders => _orders;

    /// <summary>Total quantity available at this price level.</summary>
    public decimal TotalQuantity
    {
        get
        {
            decimal sum = 0;
            // Sequential iteration for cache efficiency
            for (var i = 0; i < _orders.Count; i++)
            {
                sum += _orders[i].Quantity;
            }
            return sum;
        }
    }

    /// <summary>Number of orders at this price level.</summary>
    public int OrderCount => _orders.Count;

    /// <summary>Whether this price level is empty.</summary>
    public bool IsEmpty => _orders.Count == 0;

    /// <summary>
    /// Creates a new price level with the specified price.
    /// </summary>
    /// <param name="price">The price for this level.</param>
    /// <param name="initialCapacity">Initial capacity for order storage.</param>
    public PriceLevel(decimal price, int initialCapacity = 16)
    {
        Price = price;
        _orders = new List<Order>(initialCapacity);
    }

    /// <summary>
    /// Adds an order to this price level (FIFO order).
    /// </summary>
    public void AddOrder(Order order) => _orders.Add(order);

    /// <summary>
    /// Removes an order from this price level.
    /// </summary>
    public bool RemoveOrder(Order order) => _orders.Remove(order);

    /// <summary>
    /// Removes the first order from this price level.
    /// </summary>
    public Order? RemoveFirst()
    {
        if (_orders.Count == 0)
            return null;

        var order = _orders[0];
        _orders.RemoveAt(0);
        return order;
    }

    /// <summary>
    /// Clears all orders from this price level.
    /// </summary>
    public void Clear() => _orders.Clear();

    /// <summary>
    /// Gets the internal list for direct manipulation (advanced usage).
    /// Used by optimized matching algorithms for sequential access.
    /// </summary>
    public List<Order> GetOrdersInternal() => _orders;
}
