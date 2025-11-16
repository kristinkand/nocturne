namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Service for testing database connectivity
/// </summary>
public interface IDatabaseConnectionService
{
    /// <summary>
    /// Tests connectivity to MongoDB with the provided connection string
    /// </summary>
    /// <param name="connectionString">MongoDB connection string</param>
    /// <param name="databaseName">Database name to test</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Connection test result</returns>
    Task<DatabaseConnectionResult> TestMongoConnectionAsync(
        string connectionString,
        string databaseName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Tests connectivity to PostgreSQL with the provided connection string
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Connection test result</returns>
    Task<DatabaseConnectionResult> TestPostgreSqlConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Tests both database connections and returns a comprehensive report
    /// </summary>
    /// <param name="mongoConnectionString">MongoDB connection string</param>
    /// <param name="mongoDatabaseName">MongoDB database name</param>
    /// <param name="postgreSqlConnectionString">PostgreSQL connection string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comprehensive connection test results</returns>
    Task<DatabaseConnectionReport> TestAllConnectionsAsync(
        string mongoConnectionString,
        string mongoDatabaseName,
        string postgreSqlConnectionString,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Result of a database connection test
/// </summary>
public record DatabaseConnectionResult
{
    /// <summary>
    /// Whether the connection test was successful
    /// </summary>
    public bool IsSuccessful { get; init; }

    /// <summary>
    /// Error message if the connection failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Additional details about the connection or error
    /// </summary>
    public string? Details { get; init; }

    /// <summary>
    /// Time taken for the connection test
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Database server information if connection was successful
    /// </summary>
    public string? ServerInfo { get; init; }

    /// <summary>
    /// Creates a successful result
    /// </summary>
    public static DatabaseConnectionResult Success(
        TimeSpan duration,
        string? serverInfo = null,
        string? details = null
    ) =>
        new()
        {
            IsSuccessful = true,
            Duration = duration,
            ServerInfo = serverInfo,
            Details = details,
        };

    /// <summary>
    /// Creates a failed result
    /// </summary>
    public static DatabaseConnectionResult Failure(
        string errorMessage,
        TimeSpan duration,
        string? details = null
    ) =>
        new()
        {
            IsSuccessful = false,
            ErrorMessage = errorMessage,
            Duration = duration,
            Details = details,
        };
}

/// <summary>
/// Comprehensive report of database connection tests
/// </summary>
public record DatabaseConnectionReport
{
    /// <summary>
    /// MongoDB connection test result
    /// </summary>
    public DatabaseConnectionResult MongoResult { get; init; } = null!;

    /// <summary>
    /// PostgreSQL connection test result
    /// </summary>
    public DatabaseConnectionResult PostgreSqlResult { get; init; } = null!;

    /// <summary>
    /// Whether all connections were successful
    /// </summary>
    public bool AllConnectionsSuccessful =>
        MongoResult.IsSuccessful && PostgreSqlResult.IsSuccessful;

    /// <summary>
    /// Total time for all connection tests
    /// </summary>
    public TimeSpan TotalDuration => MongoResult.Duration + PostgreSqlResult.Duration;
}
