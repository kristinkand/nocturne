using System.Collections.Concurrent;
using System.Diagnostics;

namespace Nocturne.API.Tests.Integration.Infrastructure;

/// <summary>
/// Utility for tracking and measuring integration test performance improvements
/// </summary>
public static class TestPerformanceTracker
{
    private static readonly ConcurrentDictionary<string, TestMetrics> _metrics = new();

    public static IDisposable MeasureTest(string testName)
    {
        return new TestMeasurement(testName);
    }

    public static void LogContainerEvent(string eventType, TimeSpan duration)
    {
        var message = $"Container {eventType}: {duration.TotalMilliseconds:F2}ms";
        Console.WriteLine($"[PERF] {message}");
    }

    public static void LogCleanupEvent(string cleanupType, TimeSpan duration)
    {
        var message = $"Cleanup {cleanupType}: {duration.TotalMilliseconds:F2}ms";
        Console.WriteLine($"[PERF] {message}");
    }

    public static TestMetrics GetMetrics(string testName)
    {
        return _metrics.GetValueOrDefault(testName, new TestMetrics());
    }

    public static void PrintSummary()
    {
        if (!_metrics.Any())
            return;

        Console.WriteLine("\n=== Integration Test Performance Summary ===");
        foreach (var kvp in _metrics.OrderBy(x => x.Key))
        {
            var metrics = kvp.Value;
            Console.WriteLine($"{kvp.Key}:");
            Console.WriteLine($"  Execution Time: {metrics.ExecutionTime.TotalMilliseconds:F2}ms");
            Console.WriteLine($"  Runs: {metrics.RunCount}");
            Console.WriteLine(
                $"  Average: {metrics.ExecutionTime.TotalMilliseconds / metrics.RunCount:F2}ms"
            );
        }

        var totalTime = _metrics.Values.Sum(m => m.ExecutionTime.TotalMilliseconds);
        var totalRuns = _metrics.Values.Sum(m => m.RunCount);
        Console.WriteLine($"\nTotal: {totalTime:F2}ms across {totalRuns} test runs");
        Console.WriteLine($"Average per test: {totalTime / totalRuns:F2}ms");
    }

    private class TestMeasurement : IDisposable
    {
        private readonly string _testName;
        private readonly Stopwatch _stopwatch;

        public TestMeasurement(string testName)
        {
            _testName = testName;
            _stopwatch = Stopwatch.StartNew();
        }

        public void Dispose()
        {
            _stopwatch.Stop();
            _metrics.AddOrUpdate(
                _testName,
                new TestMetrics { ExecutionTime = _stopwatch.Elapsed, RunCount = 1 },
                (_, existing) =>
                    new TestMetrics
                    {
                        ExecutionTime = existing.ExecutionTime + _stopwatch.Elapsed,
                        RunCount = existing.RunCount + 1,
                    }
            );

            Console.WriteLine($"[PERF] {_testName}: {_stopwatch.ElapsedMilliseconds}ms");
        }
    }
}

public class TestMetrics
{
    public TimeSpan ExecutionTime { get; set; }
    public int RunCount { get; set; }
}
