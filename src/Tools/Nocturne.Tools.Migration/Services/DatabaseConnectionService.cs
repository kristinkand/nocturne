using System.Diagnostics;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Nocturne.Tools.Migration.Data;

namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Service for testing database connectivity before migration operations
/// </summary>
public class DatabaseConnectionService : IDatabaseConnectionService
{
    private readonly ILogger<DatabaseConnectionService> _logger;
    private readonly IServiceProvider _serviceProvider;

    public DatabaseConnectionService(
        ILogger<DatabaseConnectionService> logger,
        IServiceProvider serviceProvider
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider =
            serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <inheritdoc/>
    public async Task<DatabaseConnectionResult> TestMongoConnectionAsync(
        string connectionString,
        string databaseName,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "Testing MongoDB connection to database: {DatabaseName}",
            databaseName
        );
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return DatabaseConnectionResult.Failure(
                    "MongoDB connection string is null or empty",
                    stopwatch.Elapsed
                );
            }

            if (string.IsNullOrWhiteSpace(databaseName))
            {
                return DatabaseConnectionResult.Failure(
                    "MongoDB database name is null or empty",
                    stopwatch.Elapsed
                );
            }

            // Validate connection string format
            MongoUrl mongoUrl;
            try
            {
                mongoUrl = new MongoUrl(connectionString);
            }
            catch (Exception ex)
            {
                return DatabaseConnectionResult.Failure(
                    $"Invalid MongoDB connection string format: {ex.Message}",
                    stopwatch.Elapsed
                );
            }

            // Create client with connection timeout
            var clientSettings = MongoClientSettings.FromUrl(mongoUrl);
            clientSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(10);
            clientSettings.ConnectTimeout = TimeSpan.FromSeconds(10);

            var client = new MongoClient(clientSettings);

            // Try to force connection by listing databases
            await client.ListDatabaseNamesAsync(cancellationToken: cancellationToken);

            // Test database access
            var database = client.GetDatabase(databaseName);
            var collections = await database.ListCollectionNamesAsync(
                cancellationToken: cancellationToken
            );
            var collectionList = await collections.ToListAsync(cancellationToken);

            stopwatch.Stop();

            var serverInfo =
                $"Server: {mongoUrl.Server}, Database: {databaseName}, Collections: {collectionList.Count}";
            var details = collectionList.Any()
                ? $"Available collections: {string.Join(", ", collectionList.Take(10))}{(collectionList.Count > 10 ? "..." : "")}"
                : "No collections found in database";

            _logger.LogInformation(
                "MongoDB connection successful. {ServerInfo}. Test duration: {Duration}ms",
                serverInfo,
                stopwatch.Elapsed.TotalMilliseconds
            );

            return DatabaseConnectionResult.Success(stopwatch.Elapsed, serverInfo, details);
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();
            var errorMessage = $"MongoDB connection timeout: {ex.Message}";
            _logger.LogError(ex, "MongoDB connection failed due to timeout");
            return DatabaseConnectionResult.Failure(errorMessage, stopwatch.Elapsed);
        }
        catch (MongoAuthenticationException ex)
        {
            stopwatch.Stop();
            var errorMessage = $"MongoDB authentication failed: {ex.Message}";
            _logger.LogError(ex, "MongoDB connection failed due to authentication error");
            return DatabaseConnectionResult.Failure(errorMessage, stopwatch.Elapsed);
        }
        catch (MongoConnectionException ex)
        {
            stopwatch.Stop();
            var errorMessage = $"MongoDB connection error: {ex.Message}";
            _logger.LogError(ex, "MongoDB connection failed");
            return DatabaseConnectionResult.Failure(errorMessage, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var errorMessage = $"MongoDB connection test failed: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during MongoDB connection test");
            return DatabaseConnectionResult.Failure(errorMessage, stopwatch.Elapsed);
        }
    }

    /// <inheritdoc/>
    public async Task<DatabaseConnectionResult> TestPostgreSqlConnectionAsync(
        string connectionString,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Testing PostgreSQL connection");
        var stopwatch = Stopwatch.StartNew();

        try
        {
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return DatabaseConnectionResult.Failure(
                    "PostgreSQL connection string is null or empty",
                    stopwatch.Elapsed
                );
            }

            // Create a temporary DbContext for testing
            var optionsBuilder = new DbContextOptionsBuilder<MigrationDbContext>();
            optionsBuilder.UseNpgsql(
                connectionString,
                options =>
                {
                    options.CommandTimeout(10); // 10 second timeout
                }
            );

            using var testContext = new MigrationDbContext(optionsBuilder.Options);

            // Test basic connectivity
            var canConnect = await testContext.Database.CanConnectAsync(cancellationToken);
            if (!canConnect)
            {
                stopwatch.Stop();
                return DatabaseConnectionResult.Failure(
                    "Cannot establish connection to PostgreSQL database",
                    stopwatch.Elapsed
                );
            }

            // Get server version and connection info
            var connectionInfo = testContext.Database.GetDbConnection();
            string? serverVersion = null;
            int tablesCount = 0;

            try
            {
                // Test with a simple query first
                await testContext.Database.ExecuteSqlRawAsync("SELECT 1", cancellationToken);

                // Get server version
                using var command = connectionInfo.CreateCommand();
                command.CommandText = "SELECT version()";
                await connectionInfo.OpenAsync(cancellationToken);
                var result = await command.ExecuteScalarAsync(cancellationToken);
                serverVersion = result?.ToString();

                // Get table count
                command.CommandText =
                    "SELECT COUNT(*) FROM information_schema.tables WHERE table_schema = 'public'";
                var countResult = await command.ExecuteScalarAsync(cancellationToken);
                if (countResult != null && int.TryParse(countResult.ToString(), out var count))
                {
                    tablesCount = count;
                }
            }
            catch (Exception ex)
            {
                _logger.LogDebug(
                    ex,
                    "Failed to get additional PostgreSQL information, but basic connection works"
                );
                serverVersion = "Unknown (query failed)";
            }
            finally
            {
                if (connectionInfo.State == System.Data.ConnectionState.Open)
                {
                    await connectionInfo.CloseAsync();
                }
            }

            stopwatch.Stop();

            var serverInfo =
                $"Server: {connectionInfo.DataSource}, Database: {connectionInfo.Database}";
            var details =
                $"PostgreSQL Version: {serverVersion?.Split('\n')[0] ?? "Unknown"}, Public tables: {tablesCount}";

            _logger.LogInformation(
                "PostgreSQL connection successful. {ServerInfo}. Test duration: {Duration}ms",
                serverInfo,
                stopwatch.Elapsed.TotalMilliseconds
            );

            return DatabaseConnectionResult.Success(stopwatch.Elapsed, serverInfo, details);
        }
        catch (Npgsql.NpgsqlException ex)
        {
            stopwatch.Stop();
            var errorMessage = ex.SqlState switch
            {
                "28000" => "PostgreSQL authentication failed - invalid username or password",
                "3D000" => "PostgreSQL database does not exist",
                "28P01" => "PostgreSQL authentication failed - invalid password",
                "42P04" => "PostgreSQL database does not exist",
                _ => $"PostgreSQL error ({ex.SqlState}): {ex.Message}",
            };

            _logger.LogError(
                ex,
                "PostgreSQL connection failed with SQL state: {SqlState}",
                ex.SqlState
            );
            return DatabaseConnectionResult.Failure(errorMessage, stopwatch.Elapsed);
        }
        catch (TimeoutException ex)
        {
            stopwatch.Stop();
            var errorMessage = $"PostgreSQL connection timeout: {ex.Message}";
            _logger.LogError(ex, "PostgreSQL connection failed due to timeout");
            return DatabaseConnectionResult.Failure(errorMessage, stopwatch.Elapsed);
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            var errorMessage = $"PostgreSQL connection test failed: {ex.Message}";
            _logger.LogError(ex, "Unexpected error during PostgreSQL connection test");
            return DatabaseConnectionResult.Failure(errorMessage, stopwatch.Elapsed);
        }
    }

    /// <inheritdoc/>
    public async Task<DatabaseConnectionReport> TestAllConnectionsAsync(
        string mongoConnectionString,
        string mongoDatabaseName,
        string postgreSqlConnectionString,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Starting comprehensive database connectivity test");

        // Test both connections concurrently for faster results
        var mongoTask = TestMongoConnectionAsync(
            mongoConnectionString,
            mongoDatabaseName,
            cancellationToken
        );
        var postgresTask = TestPostgreSqlConnectionAsync(
            postgreSqlConnectionString,
            cancellationToken
        );

        await Task.WhenAll(mongoTask, postgresTask);

        var report = new DatabaseConnectionReport
        {
            MongoResult = await mongoTask,
            PostgreSqlResult = await postgresTask,
        };

        _logger.LogInformation(
            "Database connectivity test completed. MongoDB: {MongoStatus}, PostgreSQL: {PostgreStatus}, Total duration: {Duration}ms",
            report.MongoResult.IsSuccessful ? "Success" : "Failed",
            report.PostgreSqlResult.IsSuccessful ? "Success" : "Failed",
            report.TotalDuration.TotalMilliseconds
        );

        return report;
    }
}
