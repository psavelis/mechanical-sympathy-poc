namespace MechanicalSympathy.Domain.Entities;

/// <summary>
/// Represents an executed trade between two orders.
/// Immutable record for thread-safety and clarity.
/// </summary>
public sealed record Trade
{
    /// <summary>Unique trade identifier.</summary>
    public required long Id { get; init; }

    /// <summary>The buy order ID involved in this trade.</summary>
    public required long BuyOrderId { get; init; }

    /// <summary>The sell order ID involved in this trade.</summary>
    public required long SellOrderId { get; init; }

    /// <summary>Instrument that was traded.</summary>
    public required long InstrumentId { get; init; }

    /// <summary>Execution price.</summary>
    public required decimal Price { get; init; }

    /// <summary>Executed quantity.</summary>
    public required decimal Quantity { get; init; }

    /// <summary>When the trade was executed.</summary>
    public required DateTime ExecutedAt { get; init; }

    /// <summary>
    /// Creates a new trade from matched orders.
    /// </summary>
    public static Trade Create(
        long id,
        long buyOrderId,
        long sellOrderId,
        long instrumentId,
        decimal price,
        decimal quantity)
    {
        return new Trade
        {
            Id = id,
            BuyOrderId = buyOrderId,
            SellOrderId = sellOrderId,
            InstrumentId = instrumentId,
            Price = price,
            Quantity = quantity,
            ExecutedAt = DateTime.UtcNow
        };
    }
}
