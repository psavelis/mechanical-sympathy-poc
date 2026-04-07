using FluentAssertions;
using MechanicalSympathy.Domain.Entities;
using MechanicalSympathy.Domain.ValueObjects;
using Xunit;

namespace MechanicalSympathy.UnitTests.Domain;

public class OrderTests
{
    [Fact]
    public void Create_ShouldInitializeOrderWithCorrectValues()
    {
        // Arrange & Act
        var order = Order.Create(
            id: 1,
            instrumentId: 100,
            side: Side.Buy,
            type: OrderType.Limit,
            price: 150.50m,
            quantity: 1000,
            clientId: 42,
            clientOrderId: "client-123"
        );

        // Assert
        order.Id.Should().Be(1);
        order.InstrumentId.Should().Be(100);
        order.Side.Should().Be(Side.Buy);
        order.Type.Should().Be(OrderType.Limit);
        order.Price.Should().Be(150.50m);
        order.Quantity.Should().Be(1000);
        order.OriginalQuantity.Should().Be(1000);
        order.ClientId.Should().Be(42);
        order.ClientOrderId.Should().Be("client-123");
        order.Status.Should().Be(OrderStatus.New);
        order.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Theory]
    [InlineData(Side.Buy)]
    [InlineData(Side.Sell)]
    public void Create_ShouldSupportBothSides(Side side)
    {
        // Act
        var order = Order.Create(
            id: 1,
            instrumentId: 1,
            side: side,
            type: OrderType.Limit,
            price: 100m,
            quantity: 100,
            clientId: 1
        );

        // Assert
        order.Side.Should().Be(side);
    }

    [Theory]
    [InlineData(OrderType.Limit)]
    [InlineData(OrderType.Market)]
    public void Create_ShouldSupportBothOrderTypes(OrderType type)
    {
        // Act
        var order = Order.Create(
            id: 1,
            instrumentId: 1,
            side: Side.Buy,
            type: type,
            price: 100m,
            quantity: 100,
            clientId: 1
        );

        // Assert
        order.Type.Should().Be(type);
    }

    [Fact]
    public void Quantity_ShouldBeMutable()
    {
        // Arrange
        var order = Order.Create(
            id: 1,
            instrumentId: 1,
            side: Side.Buy,
            type: OrderType.Limit,
            price: 100m,
            quantity: 1000,
            clientId: 1
        );

        // Act
        order.Quantity = 500;

        // Assert
        order.Quantity.Should().Be(500);
        order.OriginalQuantity.Should().Be(1000);
    }

    [Fact]
    public void Status_ShouldBeMutable()
    {
        // Arrange
        var order = Order.Create(
            id: 1,
            instrumentId: 1,
            side: Side.Buy,
            type: OrderType.Limit,
            price: 100m,
            quantity: 1000,
            clientId: 1
        );

        // Act
        order.Status = OrderStatus.PartiallyFilled;

        // Assert
        order.Status.Should().Be(OrderStatus.PartiallyFilled);
    }
}
