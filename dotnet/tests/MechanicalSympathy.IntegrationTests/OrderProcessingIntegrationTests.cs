using System.Diagnostics.Metrics;
using System.Threading.Channels;
using FluentAssertions;
using MechanicalSympathy.Core.Infrastructure.Agents;
using MechanicalSympathy.Domain.Entities;
using MechanicalSympathy.Domain.ValueObjects;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MechanicalSympathy.IntegrationTests;

public class OrderProcessingIntegrationTests
{
    private readonly Meter _meter = new("TestMeter");

    [Fact]
    public async Task MatchingEngine_ShouldMatchCrossingOrders()
    {
        // Arrange
        var tradeChannel = Channel.CreateUnbounded<Trade>();
        var agent = new OrderMatchingAgent(
            tradeChannel,
            _meter,
            NullLogger<OrderMatchingAgent>.Instance);

        using var cts = new CancellationTokenSource();
        var agentTask = agent.StartAsync(cts.Token);

        // Act - Place a resting sell order
        var sellOrder = Order.Create(
            id: 1,
            instrumentId: 1,
            side: Side.Sell,
            type: OrderType.Limit,
            price: 100m,
            quantity: 100,
            clientId: 1
        );
        await agent.SendAsync(new PlaceOrderCommand(sellOrder));

        // Place a crossing buy order
        var buyOrder = Order.Create(
            id: 2,
            instrumentId: 1,
            side: Side.Buy,
            type: OrderType.Limit,
            price: 100m,
            quantity: 100,
            clientId: 2
        );
        await agent.SendAsync(new PlaceOrderCommand(buyOrder));

        // Wait for processing
        await Task.Delay(100);
        await agent.StopAsync();
        await agentTask;

        // Assert
        tradeChannel.Reader.TryRead(out var trade).Should().BeTrue();
        trade!.Quantity.Should().Be(100);
        trade.Price.Should().Be(100m);
        trade.BuyOrderId.Should().Be(2);
        trade.SellOrderId.Should().Be(1);
    }

    [Fact]
    public async Task MatchingEngine_ShouldHandlePartialFills()
    {
        // Arrange
        var tradeChannel = Channel.CreateUnbounded<Trade>();
        var agent = new OrderMatchingAgent(
            tradeChannel,
            _meter,
            NullLogger<OrderMatchingAgent>.Instance);

        using var cts = new CancellationTokenSource();
        var agentTask = agent.StartAsync(cts.Token);

        // Place a large sell order
        var sellOrder = Order.Create(
            id: 1,
            instrumentId: 1,
            side: Side.Sell,
            type: OrderType.Limit,
            price: 100m,
            quantity: 1000,
            clientId: 1
        );
        await agent.SendAsync(new PlaceOrderCommand(sellOrder));

        // Place a smaller buy order
        var buyOrder = Order.Create(
            id: 2,
            instrumentId: 1,
            side: Side.Buy,
            type: OrderType.Limit,
            price: 100m,
            quantity: 300,
            clientId: 2
        );
        await agent.SendAsync(new PlaceOrderCommand(buyOrder));

        await Task.Delay(100);
        await agent.StopAsync();
        await agentTask;

        // Assert - Partial fill trade
        tradeChannel.Reader.TryRead(out var trade).Should().BeTrue();
        trade!.Quantity.Should().Be(300);
    }

    [Fact]
    public async Task MatchingEngine_ShouldMatchMultipleOrdersAtSamePrice()
    {
        // Arrange
        var tradeChannel = Channel.CreateUnbounded<Trade>();
        var agent = new OrderMatchingAgent(
            tradeChannel,
            _meter,
            NullLogger<OrderMatchingAgent>.Instance);

        using var cts = new CancellationTokenSource();
        var agentTask = agent.StartAsync(cts.Token);

        // Place two small sell orders
        await agent.SendAsync(new PlaceOrderCommand(Order.Create(
            id: 1, instrumentId: 1, side: Side.Sell, type: OrderType.Limit,
            price: 100m, quantity: 50, clientId: 1)));

        await agent.SendAsync(new PlaceOrderCommand(Order.Create(
            id: 2, instrumentId: 1, side: Side.Sell, type: OrderType.Limit,
            price: 100m, quantity: 50, clientId: 1)));

        // Place a buy order that matches both
        await agent.SendAsync(new PlaceOrderCommand(Order.Create(
            id: 3, instrumentId: 1, side: Side.Buy, type: OrderType.Limit,
            price: 100m, quantity: 100, clientId: 2)));

        await Task.Delay(100);
        await agent.StopAsync();
        await agentTask;

        // Assert - Two trades generated
        var trades = new List<Trade>();
        while (tradeChannel.Reader.TryRead(out var trade))
        {
            trades.Add(trade);
        }

        trades.Should().HaveCount(2);
        trades.Sum(t => t.Quantity).Should().Be(100);
    }

    [Fact]
    public async Task MatchingEngine_ShouldNotMatchNonCrossingOrders()
    {
        // Arrange
        var tradeChannel = Channel.CreateUnbounded<Trade>();
        var agent = new OrderMatchingAgent(
            tradeChannel,
            _meter,
            NullLogger<OrderMatchingAgent>.Instance);

        using var cts = new CancellationTokenSource();
        var agentTask = agent.StartAsync(cts.Token);

        // Place sell order at 102
        await agent.SendAsync(new PlaceOrderCommand(Order.Create(
            id: 1, instrumentId: 1, side: Side.Sell, type: OrderType.Limit,
            price: 102m, quantity: 100, clientId: 1)));

        // Place buy order at 98 - doesn't cross
        await agent.SendAsync(new PlaceOrderCommand(Order.Create(
            id: 2, instrumentId: 1, side: Side.Buy, type: OrderType.Limit,
            price: 98m, quantity: 100, clientId: 2)));

        await Task.Delay(100);
        await agent.StopAsync();
        await agentTask;

        // Assert - No trades
        tradeChannel.Reader.TryRead(out _).Should().BeFalse();

        // Order book should have both orders
        var snapshot = agent.GetOrderBookSnapshot(1);
        snapshot.Should().NotBeNull();
        snapshot!.Value.BestBid.Should().Be(98m);
        snapshot.Value.BestAsk.Should().Be(102m);
        snapshot.Value.Spread.Should().Be(4m);
    }

    [Fact]
    public async Task MatchingEngine_ShouldHandleHighVolume()
    {
        // Arrange
        var tradeChannel = Channel.CreateBounded<Trade>(new BoundedChannelOptions(10000)
        {
            FullMode = BoundedChannelFullMode.Wait
        });
        var agent = new OrderMatchingAgent(
            tradeChannel,
            _meter,
            NullLogger<OrderMatchingAgent>.Instance,
            capacity: 8192);

        using var cts = new CancellationTokenSource();
        var agentTask = agent.StartAsync(cts.Token);

        // Act - Send many orders
        const int orderCount = 1000;
        var tasks = new List<Task>();

        // Buy orders
        for (var i = 0; i < orderCount; i++)
        {
            var order = Order.Create(
                id: i,
                instrumentId: 1,
                side: Side.Buy,
                type: OrderType.Limit,
                price: 100m,
                quantity: 10,
                clientId: 1
            );
            tasks.Add(agent.SendAsync(new PlaceOrderCommand(order)).AsTask());
        }

        // Sell orders
        for (var i = orderCount; i < orderCount * 2; i++)
        {
            var order = Order.Create(
                id: i,
                instrumentId: 1,
                side: Side.Sell,
                type: OrderType.Limit,
                price: 100m,
                quantity: 10,
                clientId: 2
            );
            tasks.Add(agent.SendAsync(new PlaceOrderCommand(order)).AsTask());
        }

        await Task.WhenAll(tasks);
        await Task.Delay(500); // Allow processing
        await agent.StopAsync();
        await agentTask;

        // Assert
        agent.TotalOrdersProcessed.Should().Be(orderCount * 2);
        agent.TotalTradesExecuted.Should().BeGreaterThan(0);
    }
}
