namespace MechanicalSympathy.Domain.ValueObjects;

/// <summary>
/// Order side - Buy or Sell.
/// Using int as underlying type for efficient comparison in hot paths.
/// </summary>
public enum Side
{
    Buy = 0,
    Sell = 1
}
