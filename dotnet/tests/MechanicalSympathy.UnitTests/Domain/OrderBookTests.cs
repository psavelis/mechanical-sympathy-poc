using FluentAssertions;
using MechanicalSympathy.Domain.Entities;
using MechanicalSympathy.Domain.ValueObjects;
using Xunit;

namespace MechanicalSympathy.UnitTests.Domain;

public class OrderBookTests
{
    [Fact]
    public void Constructor_ShouldInitializeWithInstrumentId()
    {
        // Act
        var book = new OrderBook(100);

        // Assert
        book.InstrumentId.Should().Be(100);
        book.BestBid.Should().BeNull();
        book.BestAsk.Should().BeNull();
        book.Spread.Should().BeNull();
    }

    [Fact]
    public void AddOrder_ShouldAddBuyOrderToBids()
    {
        // Arrange
        var book = new OrderBook(1);
        var order = Order.Create(1, 1, Side.Buy, OrderType.Limit, 100m, 100, 1);

        // Act
        book.AddOrder(order);

        // Assert
        book.BestBid.Should().Be(100m);
        book.TotalBidOrders.Should().Be(1);
    }

    [Fact]
    public void AddOrder_ShouldAddSellOrderToAsks()
    {
        // Arrange
        var book = new OrderBook(1);
        var order = Order.Create(1, 1, Side.Sell, OrderType.Limit, 100m, 100, 1);

        // Act
        book.AddOrder(order);

        // Assert
        book.BestAsk.Should().Be(100m);
        book.TotalAskOrders.Should().Be(1);
    }

    [Fact]
    public void BestBid_ShouldReturnHighestBidPrice()
    {
        // Arrange
        var book = new OrderBook(1);
        book.AddOrder(Order.Create(1, 1, Side.Buy, OrderType.Limit, 99m, 100, 1));
        book.AddOrder(Order.Create(2, 1, Side.Buy, OrderType.Limit, 101m, 100, 1));
        book.AddOrder(Order.Create(3, 1, Side.Buy, OrderType.Limit, 100m, 100, 1));

        // Assert
        book.BestBid.Should().Be(101m);
    }

    [Fact]
    public void BestAsk_ShouldReturnLowestAskPrice()
    {
        // Arrange
        var book = new OrderBook(1);
        book.AddOrder(Order.Create(1, 1, Side.Sell, OrderType.Limit, 101m, 100, 1));
        book.AddOrder(Order.Create(2, 1, Side.Sell, OrderType.Limit, 99m, 100, 1));
        book.AddOrder(Order.Create(3, 1, Side.Sell, OrderType.Limit, 100m, 100, 1));

        // Assert
        book.BestAsk.Should().Be(99m);
    }

    [Fact]
    public void Spread_ShouldCalculateCorrectly()
    {
        // Arrange
        var book = new OrderBook(1);
        book.AddOrder(Order.Create(1, 1, Side.Buy, OrderType.Limit, 99m, 100, 1));
        book.AddOrder(Order.Create(2, 1, Side.Sell, OrderType.Limit, 101m, 100, 1));

        // Assert
        book.Spread.Should().Be(2m);
    }

    [Fact]
    public void RemoveOrder_ShouldRemoveOrderFromBook()
    {
        // Arrange
        var book = new OrderBook(1);
        var order = Order.Create(1, 1, Side.Buy, OrderType.Limit, 100m, 100, 1);
        book.AddOrder(order);

        // Act
        var removed = book.RemoveOrder(order);

        // Assert
        removed.Should().BeTrue();
        book.BestBid.Should().BeNull();
        book.TotalBidOrders.Should().Be(0);
    }

    [Fact]
    public void GetOppositeSide_ShouldReturnAsksForBuyOrder()
    {
        // Arrange
        var book = new OrderBook(1);
        book.AddOrder(Order.Create(1, 1, Side.Buy, OrderType.Limit, 99m, 100, 1));
        book.AddOrder(Order.Create(2, 1, Side.Sell, OrderType.Limit, 101m, 100, 1));

        // Act
        var opposite = book.GetOppositeSide(Side.Buy).ToList();

        // Assert
        opposite.Should().HaveCount(1);
        opposite[0].Price.Should().Be(101m);
    }

    [Fact]
    public void Clear_ShouldRemoveAllOrders()
    {
        // Arrange
        var book = new OrderBook(1);
        book.AddOrder(Order.Create(1, 1, Side.Buy, OrderType.Limit, 99m, 100, 1));
        book.AddOrder(Order.Create(2, 1, Side.Sell, OrderType.Limit, 101m, 100, 1));

        // Act
        book.Clear();

        // Assert
        book.BestBid.Should().BeNull();
        book.BestAsk.Should().BeNull();
        book.TotalBidOrders.Should().Be(0);
        book.TotalAskOrders.Should().Be(0);
    }
}
