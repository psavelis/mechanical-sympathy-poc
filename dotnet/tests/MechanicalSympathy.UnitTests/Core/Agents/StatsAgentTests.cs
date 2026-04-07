using FluentAssertions;
using MechanicalSympathy.Core.Infrastructure.Agents;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace MechanicalSympathy.UnitTests.Core.Agents;

public class StatsAgentTests
{
    [Fact]
    public async Task SendAsync_ShouldProcessAllMessages()
    {
        // Arrange
        var agent = new StatsAgent(NullLogger<StatsAgent>.Instance, capacity: 1024);
        using var cts = new CancellationTokenSource();
        var agentTask = agent.StartAsync(cts.Token);

        // Act
        const int messageCount = 1000;
        for (var i = 0; i < messageCount; i++)
        {
            await agent.SendAsync(new StatsMessage(1));
        }

        await agent.StopAsync();
        await agentTask;

        // Assert
        agent.Total.Should().Be(messageCount);
        agent.Count.Should().Be(messageCount);
    }

    [Fact]
    public async Task SendAsync_ShouldCalculateCorrectStatistics()
    {
        // Arrange
        var agent = new StatsAgent(NullLogger<StatsAgent>.Instance, capacity: 1024);
        using var cts = new CancellationTokenSource();
        var agentTask = agent.StartAsync(cts.Token);

        // Act - Send values 1-10
        for (var i = 1; i <= 10; i++)
        {
            await agent.SendAsync(new StatsMessage(i));
        }

        await agent.StopAsync();
        await agentTask;

        // Assert
        agent.Total.Should().Be(55); // Sum of 1-10
        agent.Count.Should().Be(10);
        agent.Min.Should().Be(1);
        agent.Max.Should().Be(10);
        agent.Average.Should().Be(5.5);
    }

    [Fact]
    public async Task SendAsync_ShouldHandleMultipleProducers()
    {
        // Arrange
        var agent = new StatsAgent(NullLogger<StatsAgent>.Instance, capacity: 4096);
        using var cts = new CancellationTokenSource();
        var agentTask = agent.StartAsync(cts.Token);

        // Act - Multiple producers sending concurrently
        const int producerCount = 10;
        const int messagesPerProducer = 1000;

        var producerTasks = Enumerable.Range(0, producerCount)
            .Select(_ => Task.Run(async () =>
            {
                for (var i = 0; i < messagesPerProducer; i++)
                {
                    await agent.SendAsync(new StatsMessage(1));
                }
            }))
            .ToArray();

        await Task.WhenAll(producerTasks);
        await agent.StopAsync();
        await agentTask;

        // Assert
        agent.Total.Should().Be(producerCount * messagesPerProducer);
        agent.Count.Should().Be(producerCount * messagesPerProducer);
    }

    [Fact]
    public async Task GetSnapshot_ShouldReturnImmutableSnapshot()
    {
        // Arrange
        var agent = new StatsAgent(NullLogger<StatsAgent>.Instance, capacity: 1024);
        using var cts = new CancellationTokenSource();
        var agentTask = agent.StartAsync(cts.Token);

        for (var i = 1; i <= 5; i++)
        {
            await agent.SendAsync(new StatsMessage(i * 10));
        }

        await agent.StopAsync();
        await agentTask;

        // Act
        var snapshot = agent.GetSnapshot();

        // Assert
        snapshot.Total.Should().Be(150);
        snapshot.Count.Should().Be(5);
        snapshot.Min.Should().Be(10);
        snapshot.Max.Should().Be(50);
        snapshot.Average.Should().Be(30);
    }

    [Fact]
    public async Task Reset_ShouldClearAllStatistics()
    {
        // Arrange
        var agent = new StatsAgent(NullLogger<StatsAgent>.Instance, capacity: 1024);
        using var cts = new CancellationTokenSource();
        var agentTask = agent.StartAsync(cts.Token);

        for (var i = 0; i < 100; i++)
        {
            await agent.SendAsync(new StatsMessage(i));
        }

        await agent.StopAsync();
        await agentTask;

        // Act
        agent.Reset();

        // Assert
        agent.Total.Should().Be(0);
        agent.Count.Should().Be(0);
    }
}
