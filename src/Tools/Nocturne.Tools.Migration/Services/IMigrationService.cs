namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Service interface for MongoDB database migration operations
/// </summary>
public interface IMigrationService
{
    /// <summary>
    /// Copies a database from source to target MongoDB instance
    /// </summary>
    /// <param name="config">Migration configuration (legacy - uses old config)</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task representing the asynchronous operation</returns>
    Task CopyDatabaseAsync(object config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tests connectivity to both source and target databases
    /// </summary>
    /// <param name="config">Migration configuration (legacy - uses old config)</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task representing the asynchronous operation with connection test results</returns>
    Task<bool> TestConnectionsAsync(object config, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a list of available collections in the source database
    /// </summary>
    /// <param name="connectionString">Source database connection string</param>
    /// <param name="databaseName">Source database name</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task representing the asynchronous operation with collection names</returns>
    Task<List<string>> GetCollectionsAsync(
        string connectionString,
        string databaseName,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets information about a database
    /// </summary>
    /// <param name="connectionString">Database connection string</param>
    /// <param name="databaseName">Database name</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>A task representing the asynchronous operation with database information</returns>
    Task<DatabaseInfo> GetDatabaseInfoAsync(
        string connectionString,
        string databaseName,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Information about a database
/// </summary>
public record DatabaseInfo(
    string Name,
    long SizeOnDisk,
    int CollectionCount,
    long DocumentCount,
    DateTime LastModified,
    List<CollectionInfo> Collections
);

/// <summary>
/// Information about a collection
/// </summary>
public record CollectionInfo(
    string Name,
    long DocumentCount,
    long AverageObjectSize,
    long StorageSize
);
