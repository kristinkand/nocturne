namespace Nocturne.Tools.Abstractions.Commands;

/// <summary>
/// Represents the result of a command execution.
/// </summary>
public record CommandResult(bool IsSuccess, string Message, int ExitCode = 0)
{
    /// <summary>
    /// Creates a successful command result.
    /// </summary>
    /// <param name="message">The success message.</param>
    /// <returns>A successful command result.</returns>
    public static CommandResult Success(string message = "Operation completed successfully") =>
        new(true, message, 0);

    /// <summary>
    /// Creates a failed command result.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="exitCode">The exit code (default is 1).</param>
    /// <returns>A failed command result.</returns>
    public static CommandResult Failure(string message, int exitCode = 1) =>
        new(false, message, exitCode);

    /// <summary>
    /// Creates a command result from an exception.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="exitCode">The exit code (default is 1).</param>
    /// <returns>A failed command result.</returns>
    public static CommandResult FromException(Exception exception, int exitCode = 1) =>
        new(false, exception.Message, exitCode);
}

/// <summary>
/// Represents the result of a command execution with data.
/// </summary>
/// <typeparam name="T">The type of data returned by the command.</typeparam>
public record CommandResult<T>(bool IsSuccess, string Message, T? Data = default, int ExitCode = 0)
{
    /// <summary>
    /// Creates a successful command result with data.
    /// </summary>
    /// <param name="data">The result data.</param>
    /// <param name="message">The success message.</param>
    /// <returns>A successful command result with data.</returns>
    public static CommandResult<T> Success(
        T data,
        string message = "Operation completed successfully"
    ) => new(true, message, data, 0);

    /// <summary>
    /// Creates a failed command result.
    /// </summary>
    /// <param name="message">The error message.</param>
    /// <param name="exitCode">The exit code (default is 1).</param>
    /// <returns>A failed command result.</returns>
    public static CommandResult<T> Failure(string message, int exitCode = 1) =>
        new(false, message, default, exitCode);

    /// <summary>
    /// Creates a command result from an exception.
    /// </summary>
    /// <param name="exception">The exception that occurred.</param>
    /// <param name="exitCode">The exit code (default is 1).</param>
    /// <returns>A failed command result.</returns>
    public static CommandResult<T> FromException(Exception exception, int exitCode = 1) =>
        new(false, exception.Message, default, exitCode);
}
