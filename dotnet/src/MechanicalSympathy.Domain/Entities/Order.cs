using System.Runtime.InteropServices;
using MechanicalSympathy.Domain.ValueObjects;

namespace MechanicalSympathy.Domain.Entities;

/// <summary>
/// Order entity optimized for sequential memory access.
/// Fields are laid out to maximize cache efficiency during matching.
/// Hot fields (accessed frequently during matching) are grouped together
/// in the first cache line (64 bytes).
/// </summary>
[StructLayout(LayoutKind.Sequential)]
public sealed class Order
{
    // ================================================================
    // HOT PATH FIELDS - First cache line (64 bytes)
    // These fields are accessed during every matching operation
    // ================================================================

    /// <summary>Unique order identifier.</summary>
    public long Id { get; init; }               // 8 bytes (offset 0)

    /// <summary>Instrument/symbol identifier.</summary>
    public long InstrumentId { get; init; }     // 8 bytes (offset 8)

    /// <summary>Limit price for the order.</summary>
    public decimal Price { get; init; }          // 16 bytes (offset 16)

    /// <summary>Remaining quantity (mutable for partial fills).</summary>
    public decimal Quantity { get; set; }        // 16 bytes (offset 32)

    /// <summary>Buy or Sell side.</summary>
    public Side Side { get; init; }              // 4 bytes (offset 48)

    /// <summary>Limit or Market order.</summary>
    public OrderType Type { get; init; }         // 4 bytes (offset 52)

    /// <summary>Current order status (mutable).</summary>
    public OrderStatus Status { get; set; }      // 4 bytes (offset 56)

    // 4 bytes padding to align to 64 bytes         (offset 60-63)

    // ================================================================
    // COLD PATH FIELDS - Second cache line
    // These fields are accessed less frequently
    // ================================================================

    /// <summary>Client/account identifier.</summary>
    public long ClientId { get; init; }

    /// <summary>Original quantity before any fills.</summary>
    public decimal OriginalQuantity { get; init; }

    /// <summary>When the order was created.</summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>Last update timestamp.</summary>
    public DateTime? UpdatedAt { get; set; }

    /// <summary>Client-assigned order reference.</summary>
    public string? ClientOrderId { get; init; }

    /// <summary>
    /// Creates a new order with the specified parameters.
    /// </summary>
    public static Order Create(
        long id,
        long instrumentId,
        Side side,
        OrderType type,
        decimal price,
        decimal quantity,
        long clientId,
        string? clientOrderId = null)
    {
        return new Order
        {
            Id = id,
            InstrumentId = instrumentId,
            Side = side,
            Type = type,
            Price = price,
            Quantity = quantity,
            OriginalQuantity = quantity,
            ClientId = clientId,
            ClientOrderId = clientOrderId,
            Status = OrderStatus.New,
            CreatedAt = DateTime.UtcNow
        };
    }
}
