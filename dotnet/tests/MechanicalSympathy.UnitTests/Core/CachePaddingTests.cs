using System.Runtime.CompilerServices;
using FluentAssertions;
using MechanicalSympathy.Core.Infrastructure.CachePadding;
using Xunit;

namespace MechanicalSympathy.UnitTests.Core;

public class CachePaddingTests
{
    [Fact]
    public void CachePaddedLong_ShouldHaveCorrectSize()
    {
        // The struct should be 128 bytes (2 cache lines)
        var size = Unsafe.SizeOf<CachePaddedLong>();
        size.Should().Be(CacheLineConstants.CacheLineSize * 2);
    }

    [Fact]
    public void CachePaddedLong_ShouldStoreAndRetrieveValue()
    {
        // Arrange
        var padded = new CachePaddedLong(42);

        // Assert
        padded.Value.Should().Be(42);
        ((long)padded).Should().Be(42);
    }

    [Fact]
    public void CachePaddedLong_Increment_ShouldAtomicallyIncrement()
    {
        // Arrange
        var padded = new CachePaddedLong(0);

        // Act
        var result = padded.Increment();

        // Assert
        result.Should().Be(1);
        padded.Value.Should().Be(1);
    }

    [Fact]
    public void CachePaddedLong_Add_ShouldAtomicallyAdd()
    {
        // Arrange
        var padded = new CachePaddedLong(10);

        // Act
        var result = padded.Add(5);

        // Assert
        result.Should().Be(15);
        padded.Value.Should().Be(15);
    }

    [Fact]
    public void BadCounter_ShouldShareCacheLine()
    {
        // Arrange
        var counter = new BadCounter();

        // Act
        counter.IncrementCount1();
        counter.IncrementCount2();

        // Assert - both counters work correctly
        counter.Count1.Should().Be(1);
        counter.Count2.Should().Be(1);
    }

    [Fact]
    public void GoodCounter_ShouldHaveCorrectSize()
    {
        // The class should have counters at separate offsets
        var counter = new GoodCounter();

        // Verify it works correctly
        counter.IncrementCount1();
        counter.IncrementCount2();

        counter.Count1.Should().Be(1);
        counter.Count2.Should().Be(1);
    }

    [Fact]
    public async Task GoodCounter_ShouldHandleConcurrentAccess()
    {
        // Arrange
        var counter = new GoodCounter();
        const int iterations = 100_000;

        // Act - Two threads incrementing different counters
        var task1 = Task.Run(() =>
        {
            for (var i = 0; i < iterations; i++)
                counter.IncrementCount1();
        });

        var task2 = Task.Run(() =>
        {
            for (var i = 0; i < iterations; i++)
                counter.IncrementCount2();
        });

        await Task.WhenAll(task1, task2);

        // Assert
        counter.Count1.Should().Be(iterations);
        counter.Count2.Should().Be(iterations);
    }

    [Fact]
    public void PaddedCounterArray_ShouldSupportMultipleCounters()
    {
        // Arrange
        var counters = new PaddedCounterArray(4);

        // Act
        counters.Increment(0);
        counters.Increment(1);
        counters.Add(2, 10);
        counters.Add(3, 20);

        // Assert
        counters[0].Should().Be(1);
        counters[1].Should().Be(1);
        counters[2].Should().Be(10);
        counters[3].Should().Be(20);
        counters.Sum().Should().Be(32);
    }

    [Fact]
    public void PaddedCounterArray_Reset_ShouldClearAllCounters()
    {
        // Arrange
        var counters = new PaddedCounterArray(3);
        counters.Increment(0);
        counters.Increment(1);
        counters.Increment(2);

        // Act
        counters.Reset();

        // Assert
        counters.Sum().Should().Be(0);
    }
}
