using System.Text;

namespace Nocturne.Tools.PerformanceAnalysis;

/// <summary>
/// Console application for analyzing integration test performance improvements.
/// Provides the same functionality as the previous Python script but using C# performance profiling tools.
/// </summary>
internal class Program
{
    private static void Main(string[] args)
    {
        Console.WriteLine("Nocturne Integration Test Performance Analysis Tool");
        Console.WriteLine(new string('=', 53));
        Console.WriteLine();

        if (args.Length > 0 && args[0] == "--help")
        {
            ShowHelp();
            return;
        }

        try
        {
            // Calculate theoretical improvements
            var result = IntegrationTestPerformanceAnalyzer.CalculateTheoreticalImprovement();
            var report = IntegrationTestPerformanceAnalyzer.GenerateAnalysisReport(result);

            Console.WriteLine(report);

            // Additional performance insights
            ShowPerformanceInsights(result);

            // Show recommendations
            ShowRecommendations();
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error: {ex.Message}");
            Environment.Exit(1);
        }
    }

    private static void ShowHelp()
    {
        Console.WriteLine("Usage: Nocturne.Tools.PerformanceAnalysis [options]");
        Console.WriteLine();
        Console.WriteLine("Options:");
        Console.WriteLine("  --help     Show this help message");
        Console.WriteLine();
        Console.WriteLine("This tool calculates the theoretical performance improvement");
        Console.WriteLine("from integration test optimizations using C# performance profiling.");
        Console.WriteLine();
        Console.WriteLine("The analysis includes:");
        Console.WriteLine("- Container startup time improvements");
        Console.WriteLine("- Database cleanup optimizations");
        Console.WriteLine("- Overall execution time reductions");
        Console.WriteLine("- CI/CD and developer productivity impact");
    }

    private static void ShowPerformanceInsights(PerformanceAnalysisResult result)
    {
        Console.WriteLine();
        Console.WriteLine("PERFORMANCE INSIGHTS:");
        Console.WriteLine(new string('-', 21));

        // Calculate key metrics
        var containerSavingsPercent =
            (result.ContainerImprovement.TotalSeconds / result.TimeSaved.TotalSeconds) * 100;
        var cleanupSavingsPercent =
            (result.CleanupImprovement.TotalSeconds / result.TimeSaved.TotalSeconds) * 100;

        Console.WriteLine(
            $"Primary optimization driver: {(containerSavingsPercent > cleanupSavingsPercent ? "Container sharing" : "Database cleanup")}"
        );
        Console.WriteLine(
            $"Container sharing contributes: {containerSavingsPercent:F1}% of total improvement"
        );
        Console.WriteLine(
            $"Database optimization contributes: {cleanupSavingsPercent:F1}% of total improvement"
        );

        // ROI Analysis
        Console.WriteLine();
        Console.WriteLine("RETURN ON INVESTMENT:");
        Console.WriteLine(new string('-', 21));
        Console.WriteLine($"Implementation effort: ~8 hours (estimated)");
        Console.WriteLine(
            $"Daily time savings (team): {result.TimeSaved.TotalMinutes * 10 * 5:F0} minutes"
        );
        Console.WriteLine(
            $"Weekly time savings (team): {result.TimeSaved.TotalMinutes * 10 * 5 * 7 / 60:F1} hours"
        );
        Console.WriteLine(
            $"ROI payback period: ~{8 / (result.TimeSaved.TotalMinutes * 10 * 5 * 7 / 60):F1} weeks"
        );
    }

    private static void ShowRecommendations()
    {
        Console.WriteLine();
        Console.WriteLine("RECOMMENDATIONS:");
        Console.WriteLine(new string('-', 16));
        Console.WriteLine(
            "1. Monitor actual vs theoretical performance using TestPerformanceTracker"
        );
        Console.WriteLine("2. Consider BenchmarkDotNet for more detailed performance profiling");
        Console.WriteLine("3. Implement performance regression tests in CI/CD pipeline");
        Console.WriteLine("4. Use System.Diagnostics.Activity for distributed tracing if needed");
        Console.WriteLine(
            "5. Consider memory profiling with dotMemory or PerfView for memory optimizations"
        );
        Console.WriteLine();
        Console.WriteLine("C# Performance Profiling Tools Available:");
        Console.WriteLine("- System.Diagnostics.Stopwatch (high-precision timing)");
        Console.WriteLine("- BenchmarkDotNet (comprehensive benchmarking)");
        Console.WriteLine("- dotTrace (CPU profiling)");
        Console.WriteLine("- dotMemory (memory profiling)");
        Console.WriteLine("- PerfView (ETW-based profiling)");
        Console.WriteLine("- Application Insights (production monitoring)");
    }
}

/// <summary>
/// Performance analysis calculations (embedded version for the console tool)
/// </summary>
internal static class IntegrationTestPerformanceAnalyzer
{
    public static PerformanceAnalysisResult CalculateTheoreticalImprovement()
    {
        const int numTestClasses = 8;
        const double containerStartupTime = 20.0;
        const double cleanupTimePerCollection = 0.2;
        const int collectionsPerTest = 5;
        const int testsPerClass = 5;
        const double testExecutionTime = 2.0;

        // Before optimization
        var totalContainerTimeBefore = numTestClasses * containerStartupTime;
        var totalCleanupTimeBefore =
            numTestClasses * testsPerClass * (cleanupTimePerCollection * collectionsPerTest);
        var totalTestExecutionTime = numTestClasses * testsPerClass * testExecutionTime;
        var totalTimeBefore =
            totalContainerTimeBefore + totalCleanupTimeBefore + totalTestExecutionTime;

        // After optimization
        var totalContainerTimeAfter = containerStartupTime;
        var cleanupTimeOptimized = 0.01;
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
        var dailySavings = result.TimeSaved.TotalMinutes * 20;
        var weeklySavings = dailySavings * 7 / 60;
        report.AppendLine(
            $"Daily CI runs (assume 20 runs/day): {dailySavings:F1} minutes saved per day"
        );
        report.AppendLine($"Weekly time savings: {weeklySavings:F1} hours saved per week");
        report.AppendLine();

        // Developer productivity
        report.AppendLine("DEVELOPER PRODUCTIVITY:");
        report.AppendLine(new string('-', 23));
        var developerSavingsPerDay = result.TimeSaved.TotalMinutes * 10;
        var teamSavingsPerDay = developerSavingsPerDay * 5;
        report.AppendLine(
            $"Local test runs per developer per day (assume 10): {developerSavingsPerDay:F1} minutes saved per developer per day"
        );
        report.AppendLine(
            $"With 5 developers: {teamSavingsPerDay:F1} minutes saved per day across team"
        );

        return report.ToString();
    }
}

internal class PerformanceAnalysisResult
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
