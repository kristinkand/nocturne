namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Service for discovering database schema information from PostgreSQL
/// </summary>
public interface IDatabaseSchemaIntrospectionService
{
    /// <summary>
    /// Discovers schema information for all tables in the database
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dictionary mapping table names to their schema information</returns>
    Task<Dictionary<string, TableSchema>> DiscoverAllTablesAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Checks if the database exists and is accessible
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if database exists and is accessible</returns>
    Task<bool> DatabaseExistsAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets a list of all existing table names in the database
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of table names</returns>
    Task<List<string>> GetExistingTablesAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Discovers schema information for a specific table
    /// </summary>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="tableName">Name of the table to inspect</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Table schema information, or null if table doesn't exist</returns>
    Task<TableSchema?> DiscoverTableSchemaAsync(
        string connectionString,
        string tableName,
        CancellationToken cancellationToken = default
    );
}
