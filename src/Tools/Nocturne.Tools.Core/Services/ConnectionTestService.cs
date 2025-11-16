using System.Data.Common;
using System.Diagnostics;
using Microsoft.Extensions.Logging;
using Nocturne.Tools.Abstractions.Services;

namespace Nocturne.Tools.Core.Services;

/// <summary>
/// Implementation of connection testing services.
/// </summary>
public class ConnectionTestService : IConnectionTestService
{
    private readonly ILogger<ConnectionTestService> _logger;
    private readonly HttpClient _httpClient;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionTestService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="httpClient">The HTTP client.</param>
    public ConnectionTestService(ILogger<ConnectionTestService> logger, HttpClient httpClient)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
    }

    /// <summary>
    /// Initializes a new instance of the <see cref="ConnectionTestService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public ConnectionTestService(ILogger<ConnectionTestService> logger)
        : this(logger, new HttpClient()) { }

    /// <inheritdoc/>
    public async Task<ConnectionTestResult> TestDatabaseConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Testing database connection...");

            // Try to determine the database type from connection string
            var connectionType = DetermineConnectionType(connectionString);

            switch (connectionType)
            {
                case DatabaseType.MongoDB:
                    return await TestMongoDbConnectionAsync(connectionString, cancellationToken);
                case DatabaseType.SqlServer:
                    return await TestSqlServerConnectionAsync(connectionString, cancellationToken);
                default:
                    return await TestGenericConnectionAsync(connectionString, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Database connection test failed");
            return new ConnectionTestResult(
                false,
                $"Connection test failed: {ex.Message}",
                stopwatch.Elapsed
            );
        }
    }

    /// <inheritdoc/>
    public async Task<ConnectionTestResult> TestHttpEndpointAsync(
        string endpoint,
        string? apiSecret = null,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogDebug("Testing HTTP endpoint: {Endpoint}", endpoint);

            using var request = new HttpRequestMessage(HttpMethod.Get, endpoint);

            // Add API secret if provided
            if (!string.IsNullOrEmpty(apiSecret))
            {
                request.Headers.Add("api-secret", apiSecret);
            }

            using var response = await _httpClient.SendAsync(request, cancellationToken);
            stopwatch.Stop();

            if (response.IsSuccessStatusCode)
            {
                _logger.LogDebug("HTTP endpoint test succeeded");
                return new ConnectionTestResult(
                    true,
                    $"Endpoint responded with {response.StatusCode}",
                    stopwatch.Elapsed
                );
            }
            else
            {
                _logger.LogWarning(
                    "HTTP endpoint test failed with status {StatusCode}",
                    response.StatusCode
                );
                return new ConnectionTestResult(
                    false,
                    $"Endpoint responded with {response.StatusCode}: {response.ReasonPhrase}",
                    stopwatch.Elapsed
                );
            }
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "HTTP endpoint test failed");
            return new ConnectionTestResult(
                false,
                $"HTTP request failed: {ex.Message}",
                stopwatch.Elapsed
            );
        }
    }

    private static DatabaseType DetermineConnectionType(string connectionString)
    {
        var lowerConnectionString = connectionString.ToLowerInvariant();

        if (
            lowerConnectionString.Contains("mongodb")
            || lowerConnectionString.Contains("mongodb+srv")
        )
        {
            return DatabaseType.MongoDB;
        }

        if (
            lowerConnectionString.Contains("server=")
            || lowerConnectionString.Contains("data source=")
        )
        {
            return DatabaseType.SqlServer;
        }

        return DatabaseType.Unknown;
    }

    private Task<ConnectionTestResult> TestMongoDbConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // For MongoDB, we'll use a simple approach - try to create a client and ping the server
            // This would normally require MongoDB.Driver, but for now we'll do a basic URL validation
            var uri = new Uri(connectionString);
            stopwatch.Stop();

            _logger.LogDebug("MongoDB connection string appears valid");
            return Task.FromResult(
                new ConnectionTestResult(
                    true,
                    "MongoDB connection string validated",
                    stopwatch.Elapsed
                )
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "MongoDB connection test failed");
            return Task.FromResult(
                new ConnectionTestResult(
                    false,
                    $"MongoDB connection failed: {ex.Message}",
                    stopwatch.Elapsed
                )
            );
        }
    }

    private Task<ConnectionTestResult> TestSqlServerConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // For SQL Server, we'll validate the connection string format
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            stopwatch.Stop();

            _logger.LogDebug("SQL Server connection string appears valid");
            return Task.FromResult(
                new ConnectionTestResult(
                    true,
                    "SQL Server connection string validated",
                    stopwatch.Elapsed
                )
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "SQL Server connection test failed");
            return Task.FromResult(
                new ConnectionTestResult(
                    false,
                    $"SQL Server connection failed: {ex.Message}",
                    stopwatch.Elapsed
                )
            );
        }
    }

    private Task<ConnectionTestResult> TestGenericConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Basic validation - check if it's a valid connection string format
            var builder = new DbConnectionStringBuilder { ConnectionString = connectionString };

            stopwatch.Stop();

            _logger.LogDebug("Generic connection string appears valid");
            return Task.FromResult(
                new ConnectionTestResult(
                    true,
                    "Connection string format validated",
                    stopwatch.Elapsed
                )
            );
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _logger.LogError(ex, "Generic connection test failed");
            return Task.FromResult(
                new ConnectionTestResult(
                    false,
                    $"Connection validation failed: {ex.Message}",
                    stopwatch.Elapsed
                )
            );
        }
    }

    private enum DatabaseType
    {
        Unknown,
        MongoDB,
        SqlServer,
    }
}
