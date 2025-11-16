using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Driver;
using Npgsql;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Xunit;

namespace Nocturne.Tools.Migration.Tests.Infrastructure;

/// <summary>
/// Manages test database infrastructure and lifecycle
/// </summary>
public class TestDatabaseManager : IAsyncDisposable
{
    private readonly ITestOutputHelper? _output;
    private readonly MongoDbContainer _mongoContainer;
    private readonly PostgreSqlContainer _postgresContainer;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public string MongoConnectionString { get; private set; } = "";
    public string PostgreSqlConnectionString { get; private set; } = "";
    public bool IsInitialized { get; private set; }

    public TestDatabaseManager(ITestOutputHelper? output = null)
    {
        _output = output;
        _cancellationTokenSource = new CancellationTokenSource();

        _mongoContainer = new MongoDbBuilder()
            .WithImage("mongo:7")
            .WithPortBinding(0, true) // Use random port
            .WithEnvironment("MONGO_INITDB_ROOT_USERNAME", "admin")
            .WithEnvironment("MONGO_INITDB_ROOT_PASSWORD", "testpass123")
            .WithEnvironment("MONGO_INITDB_DATABASE", "nocturne_test")
            .Build();

        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15")
            .WithDatabase("nocturne_test")
            .WithUsername("testuser")
            .WithPassword("testpass123")
            .WithPortBinding(0, true) // Use random port
            .Build();
    }

    /// <summary>
    /// Initializes and starts the test databases
    /// </summary>
    public async Task InitializeAsync(TimeSpan? timeout = null)
    {
        var actualTimeout = timeout ?? TimeSpan.FromMinutes(5);
        using var cts = CancellationTokenSource.CreateLinkedTokenSource(
            _cancellationTokenSource.Token
        );
        cts.CancelAfter(actualTimeout);

        try
        {
            _output?.WriteLine("Starting test databases...");

            // Start containers in parallel
            var mongoTask = _mongoContainer.StartAsync(cts.Token);
            var postgresTask = _postgresContainer.StartAsync(cts.Token);

            await Task.WhenAll(mongoTask, postgresTask);

            MongoConnectionString = _mongoContainer.GetConnectionString();
            PostgreSqlConnectionString = _postgresContainer.GetConnectionString();

            _output?.WriteLine($"MongoDB started: {MongoConnectionString}");
            _output?.WriteLine($"PostgreSQL started: {PostgreSqlConnectionString}");

            // Verify connections
            await VerifyConnectionsAsync(cts.Token);

            // Initialize test data
            await InitializeTestDataAsync(cts.Token);

            IsInitialized = true;
            _output?.WriteLine("Test databases initialized successfully");
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"Failed to initialize test databases: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Verifies that database connections are working
    /// </summary>
    public async Task VerifyConnectionsAsync(CancellationToken cancellationToken = default)
    {
        // Verify MongoDB connection
        var mongoClient = new MongoClient(MongoConnectionString);
        await mongoClient.ListDatabaseNamesAsync(cancellationToken: cancellationToken);

        // Verify PostgreSQL connection
        await using var pgConnection = new NpgsqlConnection(PostgreSqlConnectionString);
        await pgConnection.OpenAsync(cancellationToken);
        await using var command = pgConnection.CreateCommand();
        command.CommandText = "SELECT 1";
        await command.ExecuteScalarAsync(cancellationToken);
    }

    /// <summary>
    /// Cleans up all test data while keeping containers running
    /// </summary>
    public async Task CleanupTestDataAsync(CancellationToken cancellationToken = default)
    {
        if (!IsInitialized)
            return;

        try
        {
            // Clean MongoDB collections
            var mongoClient = new MongoClient(MongoConnectionString);
            var database = mongoClient.GetDatabase("nocturne_test");

            var collections = new[]
            {
                "entries",
                "treatments",
                "profiles",
                "devicestatus",
                "settings",
                "food",
                "activity",
                "auth",
            };
            foreach (var collectionName in collections)
            {
                var collection = database.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);
                await collection.DeleteManyAsync(
                    new MongoDB.Bson.BsonDocument(),
                    cancellationToken
                );
            }

            // Clean PostgreSQL tables
            await using var pgConnection = new NpgsqlConnection(PostgreSqlConnectionString);
            await pgConnection.OpenAsync(cancellationToken);

            var tables = new[]
            {
                "entries",
                "treatments",
                "profiles",
                "device_statuses",
                "settings",
                "foods",
                "activities",
                "auth",
            };
            foreach (var tableName in tables)
            {
                await using var command = pgConnection.CreateCommand();
                command.CommandText = $"TRUNCATE TABLE IF EXISTS {tableName} CASCADE";
                await command.ExecuteNonQueryAsync(cancellationToken);
            }

            _output?.WriteLine("Test data cleaned up successfully");
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"Failed to cleanup test data: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Seeds test databases with sample data
    /// </summary>
    public async Task SeedTestDataAsync(
        Dictionary<string, List<MongoDB.Bson.BsonDocument>> testData,
        CancellationToken cancellationToken = default
    )
    {
        if (!IsInitialized)
            return;

        var mongoClient = new MongoClient(MongoConnectionString);
        var database = mongoClient.GetDatabase("nocturne_test");

        foreach (var (collectionName, documents) in testData)
        {
            if (documents.Count == 0)
                continue;

            var collection = database.GetCollection<MongoDB.Bson.BsonDocument>(collectionName);
            await collection.InsertManyAsync(documents, cancellationToken: cancellationToken);

            _output?.WriteLine(
                $"Seeded {documents.Count} documents to {collectionName} collection"
            );
        }
    }

    /// <summary>
    /// Gets database performance metrics
    /// </summary>
    public async Task<DatabasePerformanceMetrics> GetPerformanceMetricsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var metrics = new DatabasePerformanceMetrics();

        if (!IsInitialized)
            return metrics;

        try
        {
            // MongoDB metrics
            var mongoClient = new MongoClient(MongoConnectionString);
            var database = mongoClient.GetDatabase("nocturne_test");
            var mongoStats = await database.RunCommandAsync<MongoDB.Bson.BsonDocument>(
                new MongoDB.Bson.BsonDocument("dbStats", 1),
                cancellationToken: cancellationToken
            );

            metrics.MongoDbSize = mongoStats.GetValue("dataSize", 0).ToInt64();
            metrics.MongoCollectionCount = mongoStats.GetValue("collections", 0).ToInt32();

            // PostgreSQL metrics
            await using var pgConnection = new NpgsqlConnection(PostgreSqlConnectionString);
            await pgConnection.OpenAsync(cancellationToken);

            await using var command = pgConnection.CreateCommand();
            command.CommandText = "SELECT pg_database_size(current_database())";
            var pgSize = await command.ExecuteScalarAsync(cancellationToken);
            metrics.PostgreSqlDbSize = Convert.ToInt64(pgSize);

            return metrics;
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"Failed to get performance metrics: {ex.Message}");
            return metrics;
        }
    }

    /// <summary>
    /// Executes a database health check
    /// </summary>
    public async Task<HealthCheckResult> PerformHealthCheckAsync(
        CancellationToken cancellationToken = default
    )
    {
        var result = new HealthCheckResult();
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Check MongoDB
            var mongoClient = new MongoClient(MongoConnectionString);
            await mongoClient.ListDatabaseNamesAsync(cancellationToken: cancellationToken);
            result.MongoDbHealthy = true;
            result.MongoDbResponseTime = stopwatch.Elapsed;

            stopwatch.Restart();

            // Check PostgreSQL
            await using var pgConnection = new NpgsqlConnection(PostgreSqlConnectionString);
            await pgConnection.OpenAsync(cancellationToken);
            await using var command = pgConnection.CreateCommand();
            command.CommandText = "SELECT 1";
            await command.ExecuteScalarAsync(cancellationToken);
            result.PostgreSqlHealthy = true;
            result.PostgreSqlResponseTime = stopwatch.Elapsed;

            result.OverallHealthy = result.MongoDbHealthy && result.PostgreSqlHealthy;
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"Health check failed: {ex.Message}");
            result.ErrorMessage = ex.Message;
        }

        return result;
    }

    public async ValueTask DisposeAsync()
    {
        _cancellationTokenSource.Cancel();

        try
        {
            if (_mongoContainer != null)
                await _mongoContainer.DisposeAsync();

            if (_postgresContainer != null)
                await _postgresContainer.DisposeAsync();
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"Error disposing test databases: {ex.Message}");
        }
        finally
        {
            _cancellationTokenSource.Dispose();
        }
    }

    private async Task InitializeTestDataAsync(CancellationToken cancellationToken)
    {
        try
        {
            // Initialize MongoDB collections
            var mongoClient = new MongoClient(MongoConnectionString);
            var database = mongoClient.GetDatabase("nocturne_test");

            var collections = new[]
            {
                "entries",
                "treatments",
                "profiles",
                "devicestatus",
                "settings",
                "food",
                "activity",
                "auth",
            };
            foreach (var collectionName in collections)
            {
                await database.CreateCollectionAsync(
                    collectionName,
                    cancellationToken: cancellationToken
                );
            }

            // Create indexes
            var entriesCollection = database.GetCollection<MongoDB.Bson.BsonDocument>("entries");
            await entriesCollection.Indexes.CreateOneAsync(
                new MongoDB.Driver.CreateIndexModel<MongoDB.Bson.BsonDocument>(
                    MongoDB.Driver.Builders<MongoDB.Bson.BsonDocument>.IndexKeys.Ascending("date")
                ),
                cancellationToken: cancellationToken
            );

            _output?.WriteLine("MongoDB collections and indexes created");
        }
        catch (Exception ex)
        {
            _output?.WriteLine($"Failed to initialize test data: {ex.Message}");
            throw;
        }
    }
}

/// <summary>
/// Database performance metrics
/// </summary>
public class DatabasePerformanceMetrics
{
    public long MongoDbSize { get; set; }
    public int MongoCollectionCount { get; set; }
    public long PostgreSqlDbSize { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Health check result
/// </summary>
public class HealthCheckResult
{
    public bool MongoDbHealthy { get; set; }
    public bool PostgreSqlHealthy { get; set; }
    public bool OverallHealthy { get; set; }
    public TimeSpan MongoDbResponseTime { get; set; }
    public TimeSpan PostgreSqlResponseTime { get; set; }
    public string? ErrorMessage { get; set; }
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Test orchestration utilities
/// </summary>
public static class TestOrchestrator
{
    /// <summary>
    /// Runs a test with automatic database setup and cleanup
    /// </summary>
    public static async Task RunWithDatabaseAsync(
        Func<TestDatabaseManager, Task> testAction,
        ITestOutputHelper? output = null,
        TimeSpan? timeout = null
    )
    {
        await using var dbManager = new TestDatabaseManager(output);
        await dbManager.InitializeAsync(timeout);

        try
        {
            await testAction(dbManager);
        }
        finally
        {
            await dbManager.CleanupTestDataAsync();
        }
    }

    /// <summary>
    /// Runs a test with database metrics collection
    /// </summary>
    public static async Task<DatabasePerformanceMetrics> RunWithMetricsAsync(
        Func<TestDatabaseManager, Task> testAction,
        ITestOutputHelper? output = null
    )
    {
        await using var dbManager = new TestDatabaseManager(output);
        await dbManager.InitializeAsync();

        try
        {
            await testAction(dbManager);
            return await dbManager.GetPerformanceMetricsAsync();
        }
        finally
        {
            await dbManager.CleanupTestDataAsync();
        }
    }

    /// <summary>
    /// Runs multiple tests in parallel with separate database instances
    /// </summary>
    public static async Task RunParallelTestsAsync(
        IEnumerable<Func<TestDatabaseManager, Task>> testActions,
        ITestOutputHelper? output = null,
        int maxParallelism = 4
    )
    {
        var semaphore = new SemaphoreSlim(maxParallelism, maxParallelism);
        var tasks = testActions.Select(async testAction =>
        {
            await semaphore.WaitAsync();
            try
            {
                await RunWithDatabaseAsync(testAction, output);
            }
            finally
            {
                semaphore.Release();
            }
        });

        await Task.WhenAll(tasks);
    }
}
