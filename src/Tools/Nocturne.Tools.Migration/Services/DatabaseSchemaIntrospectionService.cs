using System.Collections.Concurrent;
using Microsoft.Extensions.Logging;
using Npgsql;

namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Service for discovering database schema information from PostgreSQL using system tables
/// </summary>
public class DatabaseSchemaIntrospectionService : IDatabaseSchemaIntrospectionService
{
    private readonly ILogger<DatabaseSchemaIntrospectionService> _logger;
    private readonly ConcurrentDictionary<string, Dictionary<string, TableSchema>> _schemaCache =
        new();

    public DatabaseSchemaIntrospectionService(ILogger<DatabaseSchemaIntrospectionService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<Dictionary<string, TableSchema>> DiscoverAllTablesAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Starting database schema discovery");

        // Check cache first
        var cacheKey = connectionString.GetHashCode().ToString();
        if (_schemaCache.TryGetValue(cacheKey, out var cachedSchema))
        {
            _logger.LogDebug(
                "Returning cached schema with {TableCount} tables",
                cachedSchema.Count
            );
            return cachedSchema;
        }

        var schema = new Dictionary<string, TableSchema>();

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Get all tables with their columns and constraints
            var tablesWithColumns = await GetTablesWithColumnsAsync(connection, cancellationToken);

            foreach (var kvp in tablesWithColumns)
            {
                var tableName = kvp.Key;
                var columns = kvp.Value;

                // Get indexes for this table
                var indexes = await GetTableIndexesAsync(connection, tableName, cancellationToken);

                schema[tableName] = new TableSchema(tableName, columns, indexes);

                _logger.LogDebug(
                    "Discovered table '{TableName}' with {ColumnCount} columns and {IndexCount} indexes",
                    tableName,
                    columns.Count,
                    indexes.Length
                );
            }

            // Cache the result
            _schemaCache[cacheKey] = schema;

            _logger.LogInformation(
                "Schema discovery completed: found {TableCount} tables",
                schema.Count
            );

            return schema;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover database schema");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<bool> DatabaseExistsAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Database connection failed: {Message}", ex.Message);
            return false;
        }
    }

    /// <inheritdoc/>
    public async Task<List<string>> GetExistingTablesAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    )
    {
        var tables = new List<string>();

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            const string query =
                @"
                SELECT table_name
                FROM information_schema.tables
                WHERE table_schema = 'public'
                AND table_type = 'BASE TABLE'
                ORDER BY table_name";

            using var command = new NpgsqlCommand(query, connection);
            using var reader = await command.ExecuteReaderAsync(cancellationToken);

            while (await reader.ReadAsync(cancellationToken))
            {
                tables.Add(reader.GetString(0)); // Use ordinal position instead of column name
            }

            _logger.LogDebug("Found {TableCount} tables in database", tables.Count);
            return tables;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get existing tables");
            throw;
        }
    }

    /// <inheritdoc/>
    public async Task<TableSchema?> DiscoverTableSchemaAsync(
        string connectionString,
        string tableName,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            // Check if table exists
            if (!await TableExistsAsync(connection, tableName, cancellationToken))
            {
                _logger.LogDebug("Table '{TableName}' does not exist", tableName);
                return null;
            }

            // Get columns for this table
            var columns = await GetTableColumnsAsync(connection, tableName, cancellationToken);

            // Get indexes for this table
            var indexes = await GetTableIndexesAsync(connection, tableName, cancellationToken);

            return new TableSchema(tableName, columns, indexes);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to discover schema for table '{TableName}'", tableName);
            throw;
        }
    }

    private async Task<
        Dictionary<string, Dictionary<string, ColumnSchema>>
    > GetTablesWithColumnsAsync(NpgsqlConnection connection, CancellationToken cancellationToken)
    {
        const string query =
            @"
            SELECT
                t.table_name,
                c.column_name,
                c.data_type,
                c.is_nullable,
                c.column_default,
                CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as is_primary_key
            FROM information_schema.tables t
            LEFT JOIN information_schema.columns c ON t.table_name = c.table_name AND c.table_schema = t.table_schema
            LEFT JOIN (
                SELECT kcu.table_name, kcu.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                    ON tc.constraint_name = kcu.constraint_name
                    AND tc.table_schema = kcu.table_schema
                WHERE tc.constraint_type = 'PRIMARY KEY'
                    AND tc.table_schema = 'public'
            ) pk ON c.table_name = pk.table_name AND c.column_name = pk.column_name
            WHERE t.table_schema = 'public'
                AND t.table_type = 'BASE TABLE'
                AND c.column_name IS NOT NULL
            ORDER BY t.table_name, c.ordinal_position";

        var tablesWithColumns = new Dictionary<string, Dictionary<string, ColumnSchema>>();

        using var command = new NpgsqlCommand(query, connection);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var tableName = reader.GetString(0);
            var columnName = reader.GetString(1);
            var dataType = reader.GetString(2);
            var isNullable = reader.GetString(3) == "YES";
            var isPrimaryKey = reader.GetBoolean(5);

            if (!tablesWithColumns.ContainsKey(tableName))
            {
                tablesWithColumns[tableName] = new Dictionary<string, ColumnSchema>();
            }

            tablesWithColumns[tableName][columnName] = new ColumnSchema(
                dataType,
                isNullable,
                isPrimaryKey
            );
        }

        return tablesWithColumns;
    }

    private async Task<Dictionary<string, ColumnSchema>> GetTableColumnsAsync(
        NpgsqlConnection connection,
        string tableName,
        CancellationToken cancellationToken
    )
    {
        const string query =
            @"
            SELECT
                c.column_name,
                c.data_type,
                c.is_nullable,
                c.column_default,
                CASE WHEN pk.column_name IS NOT NULL THEN true ELSE false END as is_primary_key
            FROM information_schema.columns c
            LEFT JOIN (
                SELECT kcu.column_name
                FROM information_schema.table_constraints tc
                JOIN information_schema.key_column_usage kcu
                    ON tc.constraint_name = kcu.constraint_name
                    AND tc.table_schema = kcu.table_schema
                WHERE tc.constraint_type = 'PRIMARY KEY'
                    AND tc.table_schema = 'public'
                    AND tc.table_name = @tableName
            ) pk ON c.column_name = pk.column_name
            WHERE c.table_schema = 'public'
                AND c.table_name = @tableName
            ORDER BY c.ordinal_position";

        var columns = new Dictionary<string, ColumnSchema>();

        using var command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("tableName", tableName);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            var columnName = reader.GetString(0);
            var dataType = reader.GetString(1);
            var isNullable = reader.GetString(2) == "YES";
            var isPrimaryKey = reader.GetBoolean(4);

            columns[columnName] = new ColumnSchema(dataType, isNullable, isPrimaryKey);
        }

        return columns;
    }

    private async Task<string[]> GetTableIndexesAsync(
        NpgsqlConnection connection,
        string tableName,
        CancellationToken cancellationToken
    )
    {
        const string query =
            @"
            SELECT indexname
            FROM pg_indexes
            WHERE schemaname = 'public'
                AND tablename = @tableName
                AND indexname NOT LIKE '%_pkey'
            ORDER BY indexname";

        var indexes = new List<string>();

        using var command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("tableName", tableName);
        using var reader = await command.ExecuteReaderAsync(cancellationToken);

        while (await reader.ReadAsync(cancellationToken))
        {
            indexes.Add(reader.GetString(0));
        }

        return indexes.ToArray();
    }

    private async Task<bool> TableExistsAsync(
        NpgsqlConnection connection,
        string tableName,
        CancellationToken cancellationToken
    )
    {
        const string query =
            @"
            SELECT COUNT(*)
            FROM information_schema.tables
            WHERE table_schema = 'public'
                AND table_name = @tableName
                AND table_type = 'BASE TABLE'";

        using var command = new NpgsqlCommand(query, connection);
        command.Parameters.AddWithValue("tableName", tableName);

        var count = await command.ExecuteScalarAsync(cancellationToken);
        return Convert.ToInt32(count) > 0;
    }

    /// <summary>
    /// Clears the schema cache (useful for testing or when schema changes are expected)
    /// </summary>
    public void ClearCache()
    {
        _schemaCache.Clear();
        _logger.LogDebug("Schema cache cleared");
    }
}

/// <summary>
/// Represents a table schema with columns and indexes (from SchemaValidationService.cs)
/// </summary>
public record TableSchema(
    string Name,
    Dictionary<string, ColumnSchema> Columns,
    string[] ExpectedIndexes
);

/// <summary>
/// Represents a column schema with data type, nullability, and primary key information
/// </summary>
public record ColumnSchema(string DataType, bool IsNullable, bool IsPrimaryKey);
