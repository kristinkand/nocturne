using Microsoft.Extensions.Logging;
using Nocturne.Tools.Abstractions.Services;

namespace Nocturne.Tools.Core.Services;

/// <summary>
/// Console-based implementation of progress reporting.
/// </summary>
public class ConsoleProgressReporter : IProgressReporter
{
    private readonly ILogger<ConsoleProgressReporter> _logger;
    private readonly object _lock = new();

    /// <summary>
    /// Initializes a new instance of the <see cref="ConsoleProgressReporter"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ConsoleProgressReporter(ILogger<ConsoleProgressReporter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public void ReportProgress(ProgressInfo progress)
    {
        lock (_lock)
        {
            var percentage =
                progress.TotalSteps > 0 ? (progress.CurrentStep * 100) / progress.TotalSteps : 0;
            var progressBar = CreateProgressBar(percentage);

            var message = $"[{progressBar}] {percentage}% - {progress.OperationName}";
            if (!string.IsNullOrEmpty(progress.CurrentMessage))
            {
                message += $": {progress.CurrentMessage}";
            }

            if (progress.ElapsedTime.HasValue)
            {
                message += $" (Elapsed: {FormatTimeSpan(progress.ElapsedTime.Value)})";
            }

            Console.WriteLine(message);
            _logger.LogDebug(
                "Progress: {OperationName} - {CurrentStep}/{TotalSteps} ({Percentage}%)",
                progress.OperationName,
                progress.CurrentStep,
                progress.TotalSteps,
                percentage
            );
        }
    }

    /// <inheritdoc/>
    public void ReportCompletion(string operationName, bool success, string? message = null)
    {
        lock (_lock)
        {
            var icon = success ? "✅" : "❌";
            var status = success ? "COMPLETED" : "FAILED";
            var completeMessage = $"{icon} {status}: {operationName}";

            if (!string.IsNullOrEmpty(message))
            {
                completeMessage += $" - {message}";
            }

            Console.WriteLine(completeMessage);

            if (success)
            {
                _logger.LogInformation("Operation completed: {OperationName}", operationName);
            }
            else
            {
                _logger.LogError(
                    "Operation failed: {OperationName} - {Message}",
                    operationName,
                    message
                );
            }
        }
    }

    /// <inheritdoc/>
    public void ReportError(string operationName, Exception error)
    {
        lock (_lock)
        {
            Console.WriteLine($"❌ ERROR: {operationName} - {error.Message}");
            _logger.LogError(error, "Operation error: {OperationName}", operationName);
        }
    }

    private static string CreateProgressBar(int percentage, int width = 20)
    {
        var filled = (percentage * width) / 100;
        var empty = width - filled;

        return new string('█', filled) + new string('░', empty);
    }

    private static string FormatTimeSpan(TimeSpan timeSpan)
    {
        if (timeSpan.TotalMinutes < 1)
        {
            return $"{timeSpan.Seconds}s";
        }

        if (timeSpan.TotalHours < 1)
        {
            return $"{timeSpan.Minutes}m {timeSpan.Seconds}s";
        }

        return $"{timeSpan.Hours}h {timeSpan.Minutes}m {timeSpan.Seconds}s";
    }
}
