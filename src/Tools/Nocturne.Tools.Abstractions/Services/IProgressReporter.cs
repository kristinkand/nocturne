namespace Nocturne.Tools.Abstractions.Services;

/// <summary>
/// Represents progress information for an operation.
/// </summary>
public record ProgressInfo(
    string OperationName,
    int CurrentStep,
    int TotalSteps,
    string? CurrentMessage = null,
    TimeSpan? ElapsedTime = null
);

/// <summary>
/// Service for reporting progress of long-running operations.
/// </summary>
public interface IProgressReporter
{
    /// <summary>
    /// Reports progress of an operation.
    /// </summary>
    /// <param name="progress">The progress information.</param>
    void ReportProgress(ProgressInfo progress);

    /// <summary>
    /// Reports completion of an operation.
    /// </summary>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="success">Whether the operation was successful.</param>
    /// <param name="message">Optional completion message.</param>
    void ReportCompletion(string operationName, bool success, string? message = null);

    /// <summary>
    /// Reports an error during an operation.
    /// </summary>
    /// <param name="operationName">The name of the operation.</param>
    /// <param name="error">The error that occurred.</param>
    void ReportError(string operationName, Exception error);
}
