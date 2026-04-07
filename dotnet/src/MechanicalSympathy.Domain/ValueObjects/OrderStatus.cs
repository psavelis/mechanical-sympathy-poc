namespace MechanicalSympathy.Domain.ValueObjects;

/// <summary>
/// Order status tracking lifecycle.
/// </summary>
public enum OrderStatus
{
    New = 0,
    PartiallyFilled = 1,
    Filled = 2,
    Cancelled = 3,
    Rejected = 4
}
