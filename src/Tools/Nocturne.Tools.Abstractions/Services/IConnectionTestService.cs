namespace Nocturne.Tools.Abstractions.Services;

/// <summary>
/// Represents the result of a connection test.
/// </summary>
public record ConnectionTestResult(bool Success, string Message, TimeSpan Duration);

/// <summary>
/// Service for testing connections to external services.
/// </summary>
public interface IConnectionTestService
{
    /// <summary>
    /// Tests connection to a database.
    /// </summary>
    /// <param name="connectionString">The connection string to test.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connection test result.</returns>
    Task<ConnectionTestResult> TestDatabaseConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Tests connection to an HTTP endpoint.
    /// </summary>
    /// <param name="endpoint">The endpoint URL to test.</param>
    /// <param name="apiSecret">Optional API secret for authentication.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The connection test result.</returns>
    Task<ConnectionTestResult> TestHttpEndpointAsync(
        string endpoint,
        string? apiSecret = null,
        CancellationToken cancellationToken = default
    );
}
