using FluentAssertions;
using Nocturne.API.Tests.Integration.Infrastructure;
using Xunit;

namespace Nocturne.API.Tests.Integration;

/// <summary>
/// Tests for the C# performance analysis functionality that replaced the Python script
/// </summary>
public class PerformanceAnalysisTests
{
    [Fact]
    public void CalculateTheoreticalImprovement_ShouldProduceValidResults()
    {
        // Act
        var result = IntegrationTestPerformanceAnalyzer.CalculateTheoreticalImprovement();

        // Assert
        result.Should().NotBeNull();
        result.TimeBefore.Should().BeGreaterThan(result.TimeAfter);
        result.PercentageImprovement.Should().BeGreaterThan(0);
        result.SpeedMultiplier.Should().BeGreaterThan(1);
        result.TimeSaved.Should().BeGreaterThan(TimeSpan.Zero);

        // Verify expected performance metrics (based on the calculations)
        result.PercentageImprovement.Should().BeApproximately(64.1, 0.1);
        result.SpeedMultiplier.Should().BeApproximately(2.8, 0.1);
    }

    [Fact]
    public void GenerateAnalysisReport_ShouldProduceValidReport()
    {
        // Arrange
        var result = IntegrationTestPerformanceAnalyzer.CalculateTheoreticalImprovement();

        // Act
        var report = IntegrationTestPerformanceAnalyzer.GenerateAnalysisReport(result);

        // Assert
        report.Should().NotBeNullOrWhiteSpace();
        report.Should().Contain("Integration Test Performance Optimization Analysis");
        report.Should().Contain("BEFORE OPTIMIZATION:");
        report.Should().Contain("AFTER OPTIMIZATION:");
        report.Should().Contain("IMPROVEMENT ANALYSIS:");
        report.Should().Contain("CI/CD IMPACT:");
        report.Should().Contain("DEVELOPER PRODUCTIVITY:");
        report.Should().Contain("Container startup improvement:");
        report.Should().Contain("Database cleanup improvement:");
    }

    [Fact]
    public async Task MeasureTestExecutionAsync_ShouldMeasureTime()
    {
        // Arrange
        const int delayMs = 100;
        var testAction = async () => await Task.Delay(delayMs);

        // Act
        var executionTime = await IntegrationTestPerformanceAnalyzer.MeasureTestExecutionAsync(
            testAction,
            "TestAction"
        );

        // Assert
        executionTime.Should().BeGreaterThan(TimeSpan.FromMilliseconds(delayMs * 0.8)); // Allow for some variance
        executionTime.Should().BeLessThan(TimeSpan.FromMilliseconds(delayMs * 2)); // Allow for reasonable overhead
    }

    [Fact]
    public void MeasureTestExecution_ShouldMeasureTime()
    {
        // Arrange
        const int delayMs = 100;
        var testAction = () => Thread.Sleep(delayMs);

        // Act
        var executionTime = IntegrationTestPerformanceAnalyzer.MeasureTestExecution(
            testAction,
            "TestAction"
        );

        // Assert
        executionTime.Should().BeGreaterThan(TimeSpan.FromMilliseconds(delayMs * 0.8)); // Allow for some variance
        executionTime.Should().BeLessThan(TimeSpan.FromMilliseconds(delayMs * 2)); // Allow for reasonable overhead
    }

    [Fact]
    public void CompareActualVsTheoretical_ShouldProduceValidComparison()
    {
        // Arrange
        var actualResults = new Dictionary<string, TimeSpan>
        {
            ["Test1"] = TimeSpan.FromSeconds(1),
            ["Test2"] = TimeSpan.FromSeconds(2),
            ["Test3"] = TimeSpan.FromSeconds(3),
        };

        // Act
        var comparison = IntegrationTestPerformanceAnalyzer.CompareActualVsTheoretical(
            actualResults
        );

        // Assert
        comparison.Should().NotBeNullOrWhiteSpace();
        comparison.Should().Contain("ACTUAL vs THEORETICAL PERFORMANCE COMPARISON");
        comparison.Should().Contain("Theoretical total time:");
        comparison.Should().Contain("Actual measured time:");
        comparison.Should().Contain("Variance:");
        comparison.Should().Contain("Individual Test Results:");
        comparison.Should().Contain("Test1: 1000ms");
        comparison.Should().Contain("Test2: 2000ms");
        comparison.Should().Contain("Test3: 3000ms");
    }
}
