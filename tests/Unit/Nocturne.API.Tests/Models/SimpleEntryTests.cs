using FluentAssertions;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Models;

/// <summary>
/// Simple unit tests for the Entry model to verify basic functionality
/// </summary>
public class SimpleEntryTests
{
    [Fact]
    public void Entry_DefaultType_ShouldBeSgv()
    {
        // Arrange & Act
        var entry = new Entry();

        // Assert
        entry.Type.Should().Be("sgv");
    }

    [Fact]
    public void Mills_WhenSetDirectly_ShouldReturnSetValue()
    {
        // Arrange
        var entry = new Entry();
        var expectedMills = 1641024000000; // 2022-01-01 12:00:00 UTC

        // Act
        entry.Mills = expectedMills;

        // Assert
        entry.Mills.Should().Be(expectedMills);
    }

    [Fact]
    public void Mills_WhenNotSetButDateStringExists_ShouldCalculateFromDateString()
    {
        // Arrange
        var entry = new Entry { DateString = "2022-01-01T12:00:00.000Z" };

        // Act
        var result = entry.Mills;
        // Assert
        result.Should().Be(1641038400000); // 2022-01-01 12:00:00 UTC in milliseconds
    }
}
