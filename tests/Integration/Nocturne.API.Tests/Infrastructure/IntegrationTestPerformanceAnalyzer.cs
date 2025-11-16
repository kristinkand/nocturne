using System.Diagnostics;
using System.Text;

namespace Nocturne.API.Tests.Integration.Infrastructure;

/// <summary>
/// Performance comparison calculator for integration test optimizations.
/// This class calculates the theoretical and actual performance improvement from the optimizations.
/// </summary>
public static class IntegrationTestPerformanceAnalyzer
{
    /// <summary>
    /// Calculates theoretical performance improvement from the optimizations
    /// </summary>
    /// <returns>Performance analysis results</returns>
    public static PerformanceAnalysisResult CalculateTheoreticalImprovement()
    {
        const int numTestClasses = 8; // Approximate number of integration test classes
        const double containerStartupTime = 20.0; // seconds per test class
        const double cleanupTimePerCollection = 0.2; // 200ms per collection
        const int collectionsPerTest = 5; // entries, treatments, devicestatus, profile, settings
        const int testsPerClass = 5; // average tests per class
        const double testExecutionTime = 2.0; // seconds per test

        // Before optimization calculations
        var totalContainerTimeBefore = numTestClasses * containerStartupTime;
        var totalCleanupTimeBefore =
            numTestClasses * testsPerClass * (cleanupTimePerCollection * collectionsPerTest);
        var totalTestExecutionTime = numTestClasses * testsPerClass * testExecutionTime;
        var totalTimeBefore =
            totalContainerTimeBefore + totalCleanupTimeBefore + totalTestExecutionTime;

        // After optimization calculations
        var totalContainerTimeAfter = containerStartupTime; // Only once for the entire collection
        var cleanupTimeOptimized = 0.01; // 10ms database drop/recreate
        var totalCleanupTimeAfter = numTestClasses * testsPerClass * cleanupTimeOptimized;
        var totalTimeAfter =
            totalContainerTimeAfter + totalCleanupTimeAfter + totalTestExecutionTime;

        // Calculate improvements
        var timeSaved = totalTimeBefore - totalTimeAfter;
        var percentageImprovement = (timeSaved / totalTimeBefore) * 100;
        var speedMultiplier = totalTimeBefore / totalTimeAfter;

        var containerImprovement = totalContainerTimeBefore - totalContainerTimeAfter;
        var cleanupImprovement = totalCleanupTimeBefore - totalCleanupTimeAfter;

        return new PerformanceAnalysisResult
        {
            TimeBefore = TimeSpan.FromSeconds(totalTimeBefore),
            TimeAfter = TimeSpan.FromSeconds(totalTimeAfter),
            TimeSaved = TimeSpan.FromSeconds(timeSaved),
            PercentageImprovement = percentageImprovement,
            SpeedMultiplier = speedMultiplier,
            ContainerStartupTimeBefore = TimeSpan.FromSeconds(totalContainerTimeBefore),
            ContainerStartupTimeAfter = TimeSpan.FromSeconds(totalContainerTimeAfter),
            ContainerImprovement = TimeSpan.FromSeconds(containerImprovement),
            CleanupTimeBefore = TimeSpan.FromSeconds(totalCleanupTimeBefore),
            CleanupTimeAfter = TimeSpan.FromSeconds(totalCleanupTimeAfter),
            CleanupImprovement = TimeSpan.FromSeconds(cleanupImprovement),
            TestExecutionTime = TimeSpan.FromSeconds(totalTestExecutionTime),
            NumTestClasses = numTestClasses,
            TestsPerClass = testsPerClass,
        };
    }

    /// <summary>
    /// Generates a comprehensive performance analysis report
    /// </summary>
    /// <param name="result">Performance analysis results to format</param>
    /// <returns>Formatted analysis report</returns>
    public static string GenerateAnalysisReport(PerformanceAnalysisResult result)
    {
        var report = new StringBuilder();

        report.AppendLine("Integration Test Performance Optimization Analysis");
        report.AppendLine(new string('=', 55));
        report.AppendLine();

        // Before optimization
        report.AppendLine("BEFORE OPTIMIZATION:");
        report.AppendLine(new string('-', 20));
        report.AppendLine(
            $"Container startup time: {result.NumTestClasses} classes × {result.ContainerStartupTimeBefore.TotalSeconds / result.NumTestClasses:F0}s = {result.ContainerStartupTimeBefore.TotalSeconds:F0}s"
        );
        report.AppendLine(
            $"Database cleanup time: {result.NumTestClasses * result.TestsPerClass} tests × {result.CleanupTimeBefore.TotalSeconds / (result.NumTestClasses * result.TestsPerClass):F1}s = {result.CleanupTimeBefore.TotalSeconds:F1}s"
        );
        report.AppendLine($"Test execution time: {result.TestExecutionTime.TotalSeconds:F0}s");
        report.AppendLine(
            $"TOTAL TIME BEFORE: {result.TimeBefore.TotalSeconds:F0}s ({result.TimeBefore.TotalMinutes:F1} minutes)"
        );
        report.AppendLine();

        // After optimization
        report.AppendLine("AFTER OPTIMIZATION:");
        report.AppendLine(new string('-', 19));
        report.AppendLine(
            $"Container startup time: 1 shared container × {result.ContainerStartupTimeAfter.TotalSeconds:F0}s = {result.ContainerStartupTimeAfter.TotalSeconds:F0}s"
        );
        report.AppendLine(
            $"Database cleanup time: {result.NumTestClasses * result.TestsPerClass} tests × {result.CleanupTimeAfter.TotalSeconds / (result.NumTestClasses * result.TestsPerClass):F3}s = {result.CleanupTimeAfter.TotalSeconds:F2}s"
        );
        report.AppendLine($"Test execution time: {result.TestExecutionTime.TotalSeconds:F0}s");
        report.AppendLine(
            $"TOTAL TIME AFTER: {result.TimeAfter.TotalSeconds:F0}s ({result.TimeAfter.TotalMinutes:F1} minutes)"
        );
        report.AppendLine();

        // Improvement analysis
        report.AppendLine("IMPROVEMENT ANALYSIS:");
        report.AppendLine(new string('-', 21));
        report.AppendLine(
            $"Time saved: {result.TimeSaved.TotalSeconds:F0}s ({result.TimeSaved.TotalMinutes:F1} minutes)"
        );
        report.AppendLine($"Performance improvement: {result.PercentageImprovement:F1}%");
        report.AppendLine($"Speed multiplier: {result.SpeedMultiplier:F1}x faster");
        report.AppendLine();

        // Breakdown
        report.AppendLine("Breakdown:");
        var containerPercentage =
            (result.ContainerImprovement.TotalSeconds / result.TimeBefore.TotalSeconds) * 100;
        var cleanupPercentage =
            (result.CleanupImprovement.TotalSeconds / result.TimeBefore.TotalSeconds) * 100;
        report.AppendLine(
            $"- Container startup improvement: {result.ContainerImprovement.TotalSeconds:F0}s ({containerPercentage:F1}% of total)"
        );
        report.AppendLine(
            $"- Database cleanup improvement: {result.CleanupImprovement.TotalSeconds:F1}s ({cleanupPercentage:F1}% of total)"
        );
        report.AppendLine();

        // CI/CD impact
        report.AppendLine("CI/CD IMPACT:");
        report.AppendLine(new string('-', 12));
        var dailySavings = result.TimeSaved.TotalMinutes * 20; // 20 runs per day
        var weeklySavings = dailySavings * 7 / 60; // convert to hours
        report.AppendLine(
            $"Daily CI runs (assume 20 runs/day): {dailySavings:F1} minutes saved per day"
        );
        report.AppendLine($"Weekly time savings: {weeklySavings:F1} hours saved per week");
        report.AppendLine();

        // Developer productivity
        report.AppendLine("DEVELOPER PRODUCTIVITY:");
        report.AppendLine(new string('-', 23));
        var developerSavingsPerDay = result.TimeSaved.TotalMinutes * 10; // 10 runs per developer per day
        var teamSavingsPerDay = developerSavingsPerDay * 5; // 5 developers
        report.AppendLine(
            $"Local test runs per developer per day (assume 10): {developerSavingsPerDay:F1} minutes saved per developer per day"
        );
        report.AppendLine(
            $"With 5 developers: {teamSavingsPerDay:F1} minutes saved per day across team"
        );

        return report.ToString();
    }

    /// <summary>
    /// Measures actual test execution time using high-precision timing
    /// </summary>
    /// <param name="testAction">Test action to measure</param>
    /// <param name="testName">Name of the test for reporting</param>
    /// <returns>Measured execution time</returns>
    public static async Task<TimeSpan> MeasureTestExecutionAsync(
        Func<Task> testAction,
        string testName
    )
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            await testAction();
        }
        finally
        {
            stopwatch.Stop();
            TestPerformanceTracker.LogCleanupEvent($"Test[{testName}]", stopwatch.Elapsed);
        }

        return stopwatch.Elapsed;
    }

    /// <summary>
    /// Measures synchronous test execution time using high-precision timing
    /// </summary>
    /// <param name="testAction">Test action to measure</param>
    /// <param name="testName">Name of the test for reporting</param>
    /// <returns>Measured execution time</returns>
    public static TimeSpan MeasureTestExecution(Action testAction, string testName)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            testAction();
        }
        finally
        {
            stopwatch.Stop();
            TestPerformanceTracker.LogCleanupEvent($"Test[{testName}]", stopwatch.Elapsed);
        }

        return stopwatch.Elapsed;
    }

    /// <summary>
    /// Compares actual measured performance against theoretical improvements
    /// </summary>
    /// <param name="actualResults">Dictionary of test names to actual execution times</param>
    /// <returns>Comparison analysis</returns>
    public static string CompareActualVsTheoretical(Dictionary<string, TimeSpan> actualResults)
    {
        var theoretical = CalculateTheoreticalImprovement();
        var actualTotal = actualResults.Values.Aggregate(TimeSpan.Zero, (acc, time) => acc + time);

        var report = new StringBuilder();
        report.AppendLine("ACTUAL vs THEORETICAL PERFORMANCE COMPARISON");
        report.AppendLine(new string('=', 47));
        report.AppendLine();

        report.AppendLine(
            $"Theoretical total time: {theoretical.TimeAfter.TotalMinutes:F1} minutes"
        );
        report.AppendLine($"Actual measured time: {actualTotal.TotalMinutes:F1} minutes");

        var variance = Math.Abs(actualTotal.TotalSeconds - theoretical.TimeAfter.TotalSeconds);
        var variancePercentage = (variance / theoretical.TimeAfter.TotalSeconds) * 100;

        report.AppendLine($"Variance: {variance:F1} seconds ({variancePercentage:F1}%)");

        if (variancePercentage < 10)
        {
            report.AppendLine("✅ Actual performance aligns well with theoretical projections");
        }
        else if (variancePercentage < 25)
        {
            report.AppendLine(
                "⚠️ Moderate variance from theoretical - investigate potential bottlenecks"
            );
        }
        else
        {
            report.AppendLine(
                "❌ Significant variance from theoretical - optimization may not be working as expected"
            );
        }

        report.AppendLine();
        report.AppendLine("Individual Test Results:");
        foreach (var result in actualResults.OrderBy(kvp => kvp.Key))
        {
            report.AppendLine($"- {result.Key}: {result.Value.TotalMilliseconds:F0}ms");
        }

        return report.ToString();
    }
}

/// <summary>
/// Results from performance analysis calculations
/// </summary>
public class PerformanceAnalysisResult
{
    public TimeSpan TimeBefore { get; set; }
    public TimeSpan TimeAfter { get; set; }
    public TimeSpan TimeSaved { get; set; }
    public double PercentageImprovement { get; set; }
    public double SpeedMultiplier { get; set; }
    public TimeSpan ContainerStartupTimeBefore { get; set; }
    public TimeSpan ContainerStartupTimeAfter { get; set; }
    public TimeSpan ContainerImprovement { get; set; }
    public TimeSpan CleanupTimeBefore { get; set; }
    public TimeSpan CleanupTimeAfter { get; set; }
    public TimeSpan CleanupImprovement { get; set; }
    public TimeSpan TestExecutionTime { get; set; }
    public int NumTestClasses { get; set; }
    public int TestsPerClass { get; set; }
}
