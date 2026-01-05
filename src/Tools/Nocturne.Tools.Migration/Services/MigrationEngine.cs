using System.Collections.Concurrent;
using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Tools.Abstractions.Services;
using Nocturne.Tools.Migration.Data;
using Nocturne.Tools.Migration.Models;

namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Core migration engine that handles data transfer from MongoDB to PostgreSQL
/// with robust batch processing capabilities and comprehensive data transformation
/// </summary>
/// <summary>
/// Core migration engine that handles data transfer from MongoDB to PostgreSQL
/// with robust batch processing capabilities and comprehensive data transformation
/// </summary>
/// <param name="logger">Logger instance for the migration engine</param>
/// <param name="serviceProvider">Service provider for creating scoped services</param>
/// <param name="transformationService">Service to transform MongoDB documents into entities</param>
/// <param name="validationService">Service to validate configuration, schema, and data</param>
/// <param name="indexOptimizationService">Service to analyze and create indexes for PostgreSQL</param>
public class MigrationEngine(
    ILogger<MigrationEngine> logger,
    IServiceProvider serviceProvider,
    IDataTransformationService transformationService,
    IValidationService validationService,
    IIndexOptimizationService indexOptimizationService
) : IMigrationEngine
{
    private readonly ILogger<MigrationEngine> _logger =
        logger ?? throw new ArgumentNullException(nameof(logger));
    private readonly IServiceProvider _serviceProvider =
        serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    private readonly IDataTransformationService _transformationService =
        transformationService ?? throw new ArgumentNullException(nameof(transformationService));
    private readonly IValidationService _validationService =
        validationService ?? throw new ArgumentNullException(nameof(validationService));
    private readonly IIndexOptimizationService _indexOptimizationService =
        indexOptimizationService
        ?? throw new ArgumentNullException(nameof(indexOptimizationService));
    private readonly ConcurrentDictionary<string, MigrationStatus> _migrationStatuses = new();
    private readonly SemaphoreSlim _memoryManagementSemaphore = new(1, 1);

    /// <inheritdoc/>
    public async Task<MigrationResult> MigrateAsync(
        MigrationEngineConfiguration config,
        CancellationToken cancellationToken = default
    )
    {
        var migrationId = Guid.CreateVersion7().ToString();
        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting migration {MigrationId} from MongoDB to PostgreSQL",
            migrationId
        );

        try
        {
            // Comprehensive validation
            var validationResult = await ValidatePreMigrationAsync(
                config,
                config.ValidationOptions,
                cancellationToken
            );
            if (!validationResult.IsValid)
            {
                var errorMessages = validationResult
                    .Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}")
                    .ToList();
                return new MigrationResult
                {
                    MigrationId = migrationId,
                    IsSuccess = false,
                    ErrorMessage = string.Join("; ", errorMessages),
                };
            }

            // Log conflicts for informational purposes
            if (validationResult.Conflicts.Any())
            {
                _logger.LogWarning(
                    "Migration proceeding with {ConflictCount} conflicts that may need resolution during migration",
                    validationResult.Conflicts.Count
                );

                foreach (var conflict in validationResult.Conflicts)
                {
                    _logger.LogWarning(
                        "Conflict: {Type} - {Description}",
                        conflict.ConflictType,
                        conflict.Description
                    );
                }
            }

            // Initialize migration status
            var initialStatus = new MigrationStatus
            {
                MigrationId = migrationId,
                State = MigrationState.Initializing,
                ProgressPercentage = 0,
                CurrentOperation = "Initializing migration",
                Statistics = new MigrationStatistics { StartTime = DateTime.UtcNow },
            };
            _migrationStatuses[migrationId] = initialStatus;

            // Initialize database connections
            var mongoClient = new MongoClient(config.MongoConnectionString);
            var mongoDatabase = mongoClient.GetDatabase(config.MongoDatabaseName);

            // Set up PostgreSQL contexts
            using var scope = _serviceProvider.CreateScope();
            var migrationDbContext = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();
            var dataContext = scope.ServiceProvider.GetRequiredService<MigrationDataContext>();

            // Initialize PostgreSQL database
            await InitializePostgreSqlDatabaseAsync(
                migrationDbContext,
                dataContext,
                config,
                cancellationToken
            );

            // Optimize indexes if enabled
            if (!config.IndexOptimizationOptions.SkipIndexCreation)
            {
                await OptimizeIndexesAsync(migrationId, mongoDatabase, config, cancellationToken);
            }

            // Determine collections to migrate
            var collectionsToMigrate = await GetCollectionsToMigrateAsync(
                mongoDatabase,
                config,
                cancellationToken
            );

            _logger.LogInformation(
                "Migrating {CollectionCount} collections: {Collections}",
                collectionsToMigrate.Count,
                string.Join(", ", collectionsToMigrate)
            );

            // Update status
            _migrationStatuses[migrationId] = initialStatus with
            {
                State = MigrationState.Running,
                CurrentOperation = "Migrating collections",
            };

            // Migrate collections
            var statistics = await MigrateCollectionsAsync(
                migrationId,
                mongoDatabase,
                collectionsToMigrate,
                config,
                cancellationToken
            );

            stopwatch.Stop();
            statistics = statistics with { EndTime = DateTime.UtcNow };

            // Update final status
            _migrationStatuses[migrationId] = _migrationStatuses[migrationId] with
            {
                State = MigrationState.Completed,
                ProgressPercentage = 100,
                CurrentOperation = "Migration completed",
                Statistics = statistics,
            };

            _logger.LogInformation(
                "Migration {MigrationId} completed successfully in {Duration}",
                migrationId,
                stopwatch.Elapsed
            );

            return new MigrationResult
            {
                MigrationId = migrationId,
                IsSuccess = true,
                Statistics = statistics,
            };
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning("Migration {MigrationId} was cancelled", migrationId);

            if (_migrationStatuses.TryGetValue(migrationId, out var status))
            {
                _migrationStatuses[migrationId] = status with { State = MigrationState.Cancelled };
            }

            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Migration {MigrationId} failed: {Error}",
                migrationId,
                ex.Message
            );

            if (_migrationStatuses.TryGetValue(migrationId, out var status))
            {
                _migrationStatuses[migrationId] = status with { State = MigrationState.Failed };
            }

            return new MigrationResult
            {
                MigrationId = migrationId,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Statistics = new MigrationStatistics
                {
                    StartTime = DateTime.UtcNow,
                    EndTime = DateTime.UtcNow,
                },
            };
        }
    }

    /// <inheritdoc/>
    public async Task<MigrationResult> ResumeAsync(
        MigrationEngineConfiguration config,
        string checkpointId,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Resuming migration from checkpoint {CheckpointId}", checkpointId);

        // TODO: Implement checkpoint resume logic
        // For now, we'll start a new migration
        return await MigrateAsync(config, cancellationToken);
    }

    /// <inheritdoc/>
    public async Task<MigrationStatus> GetStatusAsync(string migrationId)
    {
        await Task.CompletedTask; // Make async

        if (_migrationStatuses.TryGetValue(migrationId, out var status))
        {
            return status;
        }

        throw new ArgumentException($"Migration {migrationId} not found", nameof(migrationId));
    }

    /// <inheritdoc/>
    public async Task<Abstractions.Services.ValidationResult> ValidateAsync(
        MigrationEngineConfiguration config,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= config.ValidationOptions;
        var errors = new List<ValidationError>();
        var conflicts = new List<ValidationConflict>();

        _logger.LogInformation("Starting comprehensive migration validation");

        try
        {
            // Validate basic configuration
            var configValidation = _validationService.ValidateObject(config);
            errors.AddRange(configValidation.Errors);

            // Validate connection strings
            var mongoConnValidation = ValidateMongoConnectionString(config.MongoConnectionString);
            errors.AddRange(mongoConnValidation.Errors);

            var postgresConnValidation = _validationService.ValidateConnectionString(
                config.PostgreSqlConnectionString
            );
            errors.AddRange(postgresConnValidation.Errors);

            // Validate configuration parameters
            ValidateConfigurationParameters(config, errors);

            // Test actual connectivity once validation passes
            if (errors.Count == 0)
            {
                var connectivityErrors = await TestConnectivityAsync(config, cancellationToken);
                errors.AddRange(connectivityErrors);
            }

            return errors.Count == 0
                ? (
                    conflicts.Count == 0
                        ? Abstractions.Services.ValidationResult.Success()
                        : Abstractions.Services.ValidationResult.WithConflicts(conflicts.ToArray())
                )
                : Abstractions.Services.ValidationResult.FailureWithConflicts(
                    errors.ToArray(),
                    conflicts.ToArray()
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration validation failed: {Error}", ex.Message);
            errors.Add(
                new ValidationError("Migration", $"Migration validation failed: {ex.Message}")
            );
            return Abstractions.Services.ValidationResult.Failure(errors.ToArray());
        }
    }

    /// <inheritdoc/>
    public async Task<Abstractions.Services.ValidationResult> ValidatePreMigrationAsync(
        MigrationEngineConfiguration config,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    )
    {
        options ??= config.ValidationOptions;
        var errors = new List<ValidationError>();
        var conflicts = new List<ValidationConflict>();

        _logger.LogInformation("Starting comprehensive pre-migration validation");

        try
        {
            // First, run basic validation
            var basicValidation = await ValidateAsync(config, options, cancellationToken);
            errors.AddRange(basicValidation.Errors);
            conflicts.AddRange(basicValidation.Conflicts);

            if (!basicValidation.IsValid && !options.DryRunMode)
            {
                return Abstractions.Services.ValidationResult.FailureWithConflicts(
                    errors.ToArray(),
                    conflicts.ToArray()
                );
            }

            // Schema validation - skip if dropping existing tables
            if (options.EnableSchemaValidation && !config.DropExistingTables)
            {
                _logger.LogInformation("Performing PostgreSQL schema validation");
                var schemaValidation = await _validationService.ValidateSchemaAsync(
                    config.PostgreSqlConnectionString,
                    options,
                    cancellationToken
                );

                errors.AddRange(schemaValidation.Errors);
                conflicts.AddRange(schemaValidation.Conflicts);
            }
            else if (config.DropExistingTables)
            {
                _logger.LogInformation("Skipping schema validation because drop-tables is enabled");
            }

            // Data compatibility validation - skip if dropping existing tables
            if (options.EnableDataValidation && !config.DropExistingTables)
            {
                _logger.LogInformation("Performing data compatibility validation");
                var dataValidation = await _validationService.ValidateDataCompatibilityAsync(
                    config.MongoConnectionString,
                    config.MongoDatabaseName,
                    config.PostgreSqlConnectionString,
                    options,
                    cancellationToken
                );

                errors.AddRange(dataValidation.Errors);
                conflicts.AddRange(dataValidation.Conflicts);
            }
            else if (config.DropExistingTables && options.EnableDataValidation)
            {
                _logger.LogInformation(
                    "Skipping data compatibility validation because drop-tables is enabled"
                );
            }

            // Conflict detection
            if (options.EnableConflictDetection)
            {
                _logger.LogInformation("Performing conflict detection");
                var conflictValidation = await _validationService.DetectConflictsAsync(
                    config.MongoConnectionString,
                    config.MongoDatabaseName,
                    config.PostgreSqlConnectionString,
                    options,
                    cancellationToken
                );

                errors.AddRange(conflictValidation.Errors);
                conflicts.AddRange(conflictValidation.Conflicts);
            }

            // Referential integrity validation
            var integrityValidation = await _validationService.ValidateReferentialIntegrityAsync(
                config.MongoConnectionString,
                config.MongoDatabaseName,
                config.PostgreSqlConnectionString,
                options,
                cancellationToken
            );

            errors.AddRange(integrityValidation.Errors);
            conflicts.AddRange(integrityValidation.Conflicts);

            _logger.LogInformation(
                "Pre-migration validation completed. {ErrorCount} errors, {ConflictCount} conflicts found",
                errors.Count,
                conflicts.Count
            );

            return errors.Count == 0
                ? (
                    conflicts.Count == 0
                        ? Abstractions.Services.ValidationResult.Success()
                        : Abstractions.Services.ValidationResult.WithConflicts(conflicts.ToArray())
                )
                : Abstractions.Services.ValidationResult.FailureWithConflicts(
                    errors.ToArray(),
                    conflicts.ToArray()
                );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pre-migration validation failed: {Error}", ex.Message);
            errors.Add(
                new ValidationError(
                    "PreMigration",
                    $"Pre-migration validation failed: {ex.Message}"
                )
            );
            return Abstractions.Services.ValidationResult.Failure(errors.ToArray());
        }
    }

    private async Task InitializePostgreSqlDatabaseAsync(
        MigrationDbContext migrationDbContext,
        MigrationDataContext dataContext,
        MigrationEngineConfiguration config,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation(
            "Validating PostgreSQL database schema and initializing migration tracking"
        );

        _logger.LogDebug("Skipping redundant database connection check (already validated)");

        // Validate that main application tables exist (they should be created by NocturneDbContext migrations)
        // Skip validation if user requested it (useful for re-running migrations on existing data)
        if (config.ValidationOptions.EnableSchemaValidation)
        {
            await ValidateMainApplicationSchemaAsync(dataContext, cancellationToken);
        }
        else
        {
            _logger.LogInformation(
                "Skipping main application schema validation as requested (--skip-validation)"
            );
        }

        // Initialize migration tracking tables (these are managed by this context)
        await InitializeMigrationTrackingAsync(migrationDbContext, config, cancellationToken);

        _logger.LogInformation(
            "PostgreSQL database validation and migration tracking initialization completed"
        );
    }

    private async Task ValidateMainApplicationSchemaAsync(
        MigrationDataContext dataContext,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation("Validating main application schema exists");

        try
        {
            // Use raw SQL to check table existence - AnyAsync hangs on large tables
            var connection = dataContext.Database.GetDbConnection();
            await connection.OpenAsync(cancellationToken);

            using var command = connection.CreateCommand();
            command.CommandText =
                @"
                SELECT EXISTS (
                    SELECT FROM information_schema.tables
                    WHERE table_schema = 'public'
                    AND table_name = 'entries'
                ) AND EXISTS (
                    SELECT FROM information_schema.tables
                    WHERE table_schema = 'public'
                    AND table_name = 'treatments'
                ) AND EXISTS (
                    SELECT FROM information_schema.tables
                    WHERE table_schema = 'public'
                    AND table_name = 'devicestatus'
                );";

            var result = await command.ExecuteScalarAsync(cancellationToken);
            var allTablesExist = Convert.ToBoolean(result);

            if (!allTablesExist)
            {
                throw new InvalidOperationException(
                    "One or more required tables (entries, treatments, devicestatus) do not exist."
                );
            }

            _logger.LogInformation(
                "Main application schema validation successful - all required tables exist"
            );
        }
        catch (Exception ex) when (ex is not InvalidOperationException)
        {
            throw new InvalidOperationException(
                "Main application schema validation failed. Please ensure NocturneDbContext migrations have been applied. "
                    + "Run 'dotnet ef database update' in the Infrastructure project first.",
                ex
            );
        }
    }

    private async Task InitializeMigrationTrackingAsync(
        MigrationDbContext migrationDbContext,
        MigrationEngineConfiguration config,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation("Initializing migration tracking tables");

        if (config.DropExistingTables)
        {
            _logger.LogWarning("Dropping existing migration tracking tables");
            await migrationDbContext.Database.EnsureDeletedAsync(cancellationToken);
        }

        // Apply migration tracking table migrations
        var pendingMigrations = await migrationDbContext.Database.GetPendingMigrationsAsync(
            cancellationToken
        );
        if (pendingMigrations.Any())
        {
            _logger.LogInformation(
                "Applying {Count} migration tracking migrations: {Migrations}",
                pendingMigrations.Count(),
                string.Join(", ", pendingMigrations)
            );

            await migrationDbContext.Database.MigrateAsync(cancellationToken);
        }
        else
        {
            // Migration tracking tables should already exist from previous migrations
            // If they don't exist, they will be created on-demand when first used
            _logger.LogInformation("Migration tracking schema is up to date");
        }

        _logger.LogInformation("Migration tracking tables initialized successfully");
    }

    private async Task<List<string>> GetCollectionsToMigrateAsync(
        IMongoDatabase mongoDatabase,
        MigrationEngineConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var allCollections = await mongoDatabase.ListCollectionNamesAsync(
            cancellationToken: cancellationToken
        );
        var allCollectionsList = await allCollections.ToListAsync(cancellationToken);

        // Filter to supported collections from transformation service
        var supportedCollections = _transformationService.GetSupportedCollections().ToHashSet();

        return config.CollectionsToMigrate.Any()
            ? config
                .CollectionsToMigrate.Where(c =>
                    allCollectionsList.Contains(c) && supportedCollections.Contains(c)
                )
                .ToList()
            : allCollectionsList.Where(c => supportedCollections.Contains(c)).ToList();
    }

    private async Task<MigrationStatistics> MigrateCollectionsAsync(
        string migrationId,
        IMongoDatabase mongoDatabase,
        List<string> collectionsToMigrate,
        MigrationEngineConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var statistics = new MigrationStatistics { StartTime = DateTime.UtcNow };
        var collectionStats = new Dictionary<string, CollectionStatistics>();

        // Set up parallel processing options
        var parallelOptions = new ParallelOptions
        {
            MaxDegreeOfParallelism = config.MaxDegreeOfParallelism,
            CancellationToken = cancellationToken,
        };

        // Migrate collections in parallel if configured
        if (config.MaxDegreeOfParallelism > 1)
        {
            await Parallel.ForEachAsync(
                collectionsToMigrate,
                parallelOptions,
                async (collectionName, ct) =>
                {
                    using var scope = _serviceProvider.CreateScope();
                    var scopedMigrationDbContext =
                        scope.ServiceProvider.GetRequiredService<MigrationDbContext>();
                    var scopedDataContext =
                        scope.ServiceProvider.GetRequiredService<MigrationDataContext>();

                    var collectionStat = await MigrateCollectionAsync(
                        migrationId,
                        mongoDatabase,
                        scopedMigrationDbContext,
                        scopedDataContext,
                        collectionName,
                        config,
                        ct
                    );

                    lock (collectionStats)
                    {
                        collectionStats[collectionName] = collectionStat;
                    }
                }
            );
        }
        else
        {
            // Sequential processing
            foreach (var collectionName in collectionsToMigrate)
            {
                using var scope = _serviceProvider.CreateScope();
                var scopedMigrationDbContext =
                    scope.ServiceProvider.GetRequiredService<MigrationDbContext>();
                var scopedDataContext =
                    scope.ServiceProvider.GetRequiredService<MigrationDataContext>();

                var collectionStat = await MigrateCollectionAsync(
                    migrationId,
                    mongoDatabase,
                    scopedMigrationDbContext,
                    scopedDataContext,
                    collectionName,
                    config,
                    cancellationToken
                );
                collectionStats[collectionName] = collectionStat;
            }
        }

        return statistics with
        {
            CollectionStats = collectionStats,
            TotalDocumentsProcessed = collectionStats.Values.Sum(s => s.DocumentsMigrated),
            TotalDocumentsFailed = collectionStats.Values.Sum(s => s.DocumentsFailed),
        };
    }

    private async Task<CollectionStatistics> MigrateCollectionAsync(
        string migrationId,
        IMongoDatabase mongoDatabase,
        MigrationDbContext migrationDbContext,
        MigrationDataContext dataContext,
        string collectionName,
        MigrationEngineConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        _logger.LogInformation(
            "Starting migration of collection: {CollectionName}",
            collectionName
        );

        var collection = mongoDatabase.GetCollection<BsonDocument>(collectionName);

        // Build filter for date range if specified
        var filter = BuildDateRangeFilter(config);

        // Get total document count
        var totalDocuments = await collection.CountDocumentsAsync(
            filter,
            cancellationToken: cancellationToken
        );
        _logger.LogInformation(
            "Collection {CollectionName} has {TotalDocuments} documents to migrate",
            collectionName,
            totalDocuments
        );

        if (totalDocuments == 0)
        {
            return new CollectionStatistics
            {
                CollectionName = collectionName,
                TotalDocuments = 0,
                DocumentsMigrated = 0,
                DocumentsFailed = 0,
                Duration = stopwatch.Elapsed,
            };
        }

        var documentsMigrated = 0L;
        var documentsFailed = 0L;

        // Set up dataflow pipeline for batch processing
        var batchBlock = new BatchBlock<BsonDocument>(config.BatchSize);
        var batchNumber = 0;
        var actionBlock = new ActionBlock<BsonDocument[]>(
            async batch =>
            {
                batchNumber++;
                var result = await ProcessBatchAsync(
                    migrationDbContext,
                    dataContext,
                    migrationId,
                    collectionName,
                    batch,
                    batchNumber,
                    documentsMigrated,
                    config,
                    cancellationToken
                );
                Interlocked.Add(ref documentsMigrated, result.Succeeded);
                Interlocked.Add(ref documentsFailed, result.Failed);

                // Memory management check
                await CheckMemoryUsageAsync(config);
            },
            new ExecutionDataflowBlockOptions
            {
                MaxDegreeOfParallelism = 1, // Process batches sequentially per collection
                CancellationToken = cancellationToken,
            }
        );
        // Ensure the action block completes when the source completes
        batchBlock.LinkTo(actionBlock, new DataflowLinkOptions { PropagateCompletion = true });

        // Read documents from MongoDB and feed to pipeline
        var sort = Builders<BsonDocument>
            .Sort.Ascending("date")
            .Ascending("created_at")
            .Ascending("_id");
        var findOptions = new FindOptions<BsonDocument>
        {
            Sort = sort,
            BatchSize = config.BatchSize,
        };
        var cursor = await collection.FindAsync(filter, findOptions, cancellationToken);

        await cursor.ForEachAsync(
            document =>
            {
                batchBlock.Post(document);
            },
            cancellationToken
        );

        batchBlock.Complete();
        await actionBlock.Completion;

        stopwatch.Stop();
        _logger.LogInformation(
            "Completed migration of collection {CollectionName}: {DocumentsMigrated} succeeded, {DocumentsFailed} failed in {Duration}",
            collectionName,
            documentsMigrated,
            documentsFailed,
            stopwatch.Elapsed
        );

        return new CollectionStatistics
        {
            CollectionName = collectionName,
            TotalDocuments = totalDocuments,
            DocumentsMigrated = documentsMigrated,
            DocumentsFailed = documentsFailed,
            Duration = stopwatch.Elapsed,
        };
    }

    private FilterDefinition<BsonDocument> BuildDateRangeFilter(MigrationEngineConfiguration config)
    {
        var filters = new List<FilterDefinition<BsonDocument>>();

        if (config.StartDate.HasValue)
        {
            var start = config.StartDate.Value;
            var startMills = ((DateTimeOffset)start).ToUnixTimeMilliseconds();
            var startFilter = Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Gte("date", startMills),
                Builders<BsonDocument>.Filter.Gte("mills", startMills),
                Builders<BsonDocument>.Filter.Gte("created_at", start)
            );
            filters.Add(startFilter);
        }

        if (config.EndDate.HasValue)
        {
            var end = config.EndDate.Value;
            var endMills = ((DateTimeOffset)end).ToUnixTimeMilliseconds();
            var endFilter = Builders<BsonDocument>.Filter.Or(
                Builders<BsonDocument>.Filter.Lte("date", endMills),
                Builders<BsonDocument>.Filter.Lte("mills", endMills),
                Builders<BsonDocument>.Filter.Lte("created_at", end)
            );
            filters.Add(endFilter);
        }

        return filters.Any()
            ? Builders<BsonDocument>.Filter.And(filters)
            : Builders<BsonDocument>.Filter.Empty;
    }

    private async Task<(int Succeeded, int Failed)> ProcessBatchAsync(
        MigrationDbContext migrationDbContext,
        MigrationDataContext dataContext,
        string migrationId,
        string collectionName,
        BsonDocument[] batch,
        int batchNumber,
        long totalMigratedBeforeBatch,
        MigrationEngineConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var succeeded = 0;
        var failed = 0;
        var currentSubBatchDocs = new List<BsonDocument>();
        long currentSubBatchSizeBytes = 0;

        try
        {
            using var transaction = await dataContext.Database.BeginTransactionAsync(
                cancellationToken
            );

            foreach (var document in batch)
            {
                // Calculate estimated size of the document
                var docSize = document.ToBson().Length;

                // Check if adding this document would exceed the limit
                // If so, flush the current sub-batch first
                if (currentSubBatchDocs.Count > 0 &&
                    currentSubBatchSizeBytes + docSize > config.MaxBatchPayloadSizeBytes)
                {
                    var (subSucceeded, subFailed) = await SaveSubBatchAsync(
                        dataContext,
                        collectionName,
                        currentSubBatchDocs,
                        config,
                        cancellationToken
                    );

                    succeeded += subSucceeded;
                    failed += subFailed;

                    // Reset for next sub-batch
                    currentSubBatchDocs.Clear();
                    currentSubBatchSizeBytes = 0;
                    dataContext.ChangeTracker.Clear();
                }

                try
                {
                    await TransformAndInsertDocumentAsync(
                        dataContext,
                        collectionName,
                        document,
                        config,
                        cancellationToken
                    );

                    currentSubBatchDocs.Add(document);
                    currentSubBatchSizeBytes += docSize;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to migrate document from collection {CollectionName}: {Error}",
                        collectionName,
                        ex.Message
                    );
                    failed++;
                }
            }

            // Flush any remaining documents in the batch
            if (currentSubBatchDocs.Count > 0)
            {
                var (subSucceeded, subFailed) = await SaveSubBatchAsync(
                    dataContext,
                    collectionName,
                    currentSubBatchDocs,
                    config,
                    cancellationToken
                );

                succeeded += subSucceeded;
                failed += subFailed;

                dataContext.ChangeTracker.Clear();
            }

            await transaction.CommitAsync(cancellationToken);

            // Create checkpoint at configured intervals
            if (config.EnableCheckpointing && config.CheckpointInterval > 0)
            {
                if (batchNumber % config.CheckpointInterval == 0)
                {
                    try
                    {
                        var last = batch.LastOrDefault();
                        var lastId =
                            last != null ? last.GetValue("_id", BsonNull.Value).ToString() : null;
                        var checkpoint = new MigrationCheckpoint
                        {
                            Id = Guid.CreateVersion7(),
                            MigrationId = migrationId,
                            CollectionName = collectionName,
                            LastProcessedId = lastId,
                            DocumentsProcessed = totalMigratedBeforeBatch + succeeded,
                            TotalDocuments = 0, // Will be set when known
                            StartTime = DateTime.UtcNow,
                            LastUpdate = DateTime.UtcNow,
                            Status = "InProgress",
                        };
                        migrationDbContext.MigrationCheckpoints.Add(checkpoint);
                        await migrationDbContext.SaveChangesAsync(cancellationToken);
                    }
                    catch (Exception cpEx)
                    {
                        _logger.LogWarning(
                            cpEx,
                            "Failed to create checkpoint for collection {CollectionName}",
                            collectionName
                        );
                    }
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to process batch for collection {CollectionName}: {Error}",
                collectionName,
                ex.Message
            );
            // If the transaction fails, the entire batch (including sub-batches) is rolled back.
            // We mark all documents in the batch as failed.
            return (0, batch.Length);
        }

        return (succeeded, failed);
    }

    private async Task<(int Succeeded, int Failed)> SaveSubBatchAsync(
        MigrationDataContext dataContext,
        string collectionName,
        List<BsonDocument> documents,
        MigrationEngineConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var succeeded = 0;
        var failed = 0;
        var skipped = 0;

        try
        {
            await dataContext.SaveChangesAsync(cancellationToken);
            succeeded = documents.Count;
        }
        catch (Exception ex)
        {
            // Fallback: try per-document save to isolate failures
            _logger.LogWarning(
                ex,
                "Batch SaveChanges failed for collection {CollectionName} (Count: {Count}). Falling back to per-document saves.",
                collectionName,
                documents.Count
            );

            // Reset change tracker and reprocess documents one by one with save
            dataContext.ChangeTracker.Clear();

            foreach (var document in documents)
            {
                try
                {
                    await TransformAndInsertDocumentAsync(
                        dataContext,
                        collectionName,
                        document,
                        config,
                        cancellationToken
                    );
                    await dataContext.SaveChangesAsync(cancellationToken);
                    succeeded++;
                    // Clear after each successful save to keep context clean
                    dataContext.ChangeTracker.Clear();
                }
                catch (DbUpdateException docEx)
                    when (config.SkipDuplicates && IsDuplicateKeyViolation(docEx))
                {
                    _logger.LogDebug(
                        "Skipping duplicate document in collection {CollectionName}",
                        collectionName
                    );
                    dataContext.ChangeTracker.Clear();
                    skipped++;
                }
                catch (Exception docEx)
                {
                    _logger.LogWarning(
                        docEx,
                        "Per-document save failed in collection {CollectionName}: {Error}",
                        collectionName,
                        docEx.Message
                    );
                    dataContext.ChangeTracker.Clear();
                    failed++;
                }
            }

            if (skipped > 0)
            {
                _logger.LogInformation(
                    "Skipped {SkippedCount} duplicate documents in collection {CollectionName}",
                    skipped,
                    collectionName
                );
            }
        }

        return (succeeded, failed);
    }

    private async Task TransformAndInsertDocumentAsync(
        MigrationDataContext dataContext,
        string collectionName,
        BsonDocument document,
        MigrationEngineConfiguration config,
        CancellationToken cancellationToken
    )
    {
        // Use the data transformation service for proper document transformation
        var transformedEntity = await _transformationService.TransformDocumentAsync(
            document,
            collectionName,
            config.TransformationOptions
        );

        // Add the transformed entity to the appropriate DbSet
        switch (collectionName.ToLowerInvariant())
        {
            case "entries":
                dataContext.Entries.Add((EntryEntity)transformedEntity);
                break;

            case "treatments":
                dataContext.Treatments.Add((TreatmentEntity)transformedEntity);
                break;

            case "profiles":
                dataContext.Profiles.Add((ProfileEntity)transformedEntity);
                break;

            case "devicestatus":
                dataContext.DeviceStatuses.Add((DeviceStatusEntity)transformedEntity);
                break;

            case "settings":
                dataContext.Settings.Add((SettingsEntity)transformedEntity);
                break;

            case "food":
                dataContext.Foods.Add((FoodEntity)transformedEntity);
                break;

            case "activity":
                dataContext.Activities.Add((ActivityEntity)transformedEntity);
                break;

            default:
                _logger.LogWarning("Unknown collection type: {CollectionName}", collectionName);
                break;
        }

        // Note: SaveChanges is performed at the batch level for efficiency
    }

    private async Task CheckMemoryUsageAsync(MigrationEngineConfiguration config)
    {
        await _memoryManagementSemaphore.WaitAsync();
        try
        {
            var currentMemoryMB = GC.GetTotalMemory(false) / (1024 * 1024);

            if (currentMemoryMB > config.MaxMemoryUsageMB)
            {
                _logger.LogWarning(
                    "Memory usage ({CurrentMemoryMB} MB) exceeds limit ({MaxMemoryMB} MB), forcing garbage collection",
                    currentMemoryMB,
                    config.MaxMemoryUsageMB
                );

                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
            }
        }
        finally
        {
            _memoryManagementSemaphore.Release();
        }
    }

    #region Private Validation Helper Methods

    private Abstractions.Services.ValidationResult ValidateMongoConnectionString(
        string connectionString
    )
    {
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return Abstractions.Services.ValidationResult.Failure(
                "MongoConnectionString",
                "MongoDB connection string cannot be null or empty",
                connectionString
            );
        }

        try
        {
            // Basic MongoDB connection string validation
            var mongoUrl = new MongoUrl(connectionString);

            if (string.IsNullOrWhiteSpace(mongoUrl.Server?.ToString()))
            {
                return Abstractions.Services.ValidationResult.Failure(
                    "MongoConnectionString",
                    "MongoDB server is required in connection string",
                    connectionString
                );
            }

            return Abstractions.Services.ValidationResult.Success();
        }
        catch (Exception ex)
        {
            return Abstractions.Services.ValidationResult.Failure(
                "MongoConnectionString",
                $"Invalid MongoDB connection string: {ex.Message}",
                connectionString
            );
        }
    }

    private async Task<List<ValidationError>> TestConnectivityAsync(
        MigrationEngineConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var errors = new List<ValidationError>();

        try
        {
            var mongoSettings = MongoClientSettings.FromConnectionString(config.MongoConnectionString);
            mongoSettings.ServerSelectionTimeout = TimeSpan.FromSeconds(5);
            var mongoClient = new MongoClient(mongoSettings);
            await mongoClient.ListDatabaseNamesAsync(cancellationToken: cancellationToken);

            var database = mongoClient.GetDatabase(config.MongoDatabaseName);
            await database.ListCollectionNamesAsync(cancellationToken: cancellationToken);
        }
        catch (Exception ex)
        {
            errors.Add(
                new ValidationError(
                    "MongoConnectionString",
                    $"MongoDB connection failed: {ex.Message}",
                    config.MongoConnectionString
                )
            );
        }

        try
        {
            using var scope = _serviceProvider.CreateScope();
            var dbContext = scope.ServiceProvider.GetRequiredService<MigrationDbContext>();
            await dbContext.Database.CanConnectAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            errors.Add(
                new ValidationError(
                    "PostgreSqlConnectionString",
                    $"PostgreSQL connection failed: {ex.Message}",
                    config.PostgreSqlConnectionString
                )
            );
        }

        return errors;
    }

    private void ValidateConfigurationParameters(
        MigrationEngineConfiguration config,
        List<ValidationError> errors
    )
    {
        if (config.BatchSize <= 0)
            errors.Add(
                new ValidationError(
                    "BatchSize",
                    "Batch size must be greater than 0",
                    config.BatchSize
                )
            );

        if (config.MaxMemoryUsageMB <= 0)
            errors.Add(
                new ValidationError(
                    "MaxMemoryUsageMB",
                    "Max memory usage must be greater than 0",
                    config.MaxMemoryUsageMB
                )
            );

        if (config.MaxDegreeOfParallelism <= 0)
            errors.Add(
                new ValidationError(
                    "MaxDegreeOfParallelism",
                    "Max degree of parallelism must be greater than 0",
                    config.MaxDegreeOfParallelism
                )
            );

        if (
            config.StartDate.HasValue
            && config.EndDate.HasValue
            && config.StartDate >= config.EndDate
        )
            errors.Add(
                new ValidationError(
                    "DateRange",
                    "Start date must be before end date",
                    new { config.StartDate, config.EndDate }
                )
            );
    }

    private async Task OptimizeIndexesAsync(
        string migrationId,
        IMongoDatabase mongoDatabase,
        MigrationEngineConfiguration config,
        CancellationToken cancellationToken
    )
    {
        _logger.LogInformation(
            "Starting index optimization for migration {MigrationId}",
            migrationId
        );

        try
        {
            // Update migration status
            if (_migrationStatuses.TryGetValue(migrationId, out var currentStatus))
            {
                _migrationStatuses[migrationId] = currentStatus with
                {
                    CurrentOperation = "Optimizing indexes",
                };
            }

            var allStrategies = new List<PostgreSqlIndexStrategy>();

            // Get collections to analyze for indexes
            var collectionsToAnalyze = config.CollectionsToMigrate.Any()
                ? config.CollectionsToMigrate
                : await GetSupportedCollectionsAsync(mongoDatabase, cancellationToken);

            foreach (var collectionName in collectionsToAnalyze)
            {
                try
                {
                    var collection = mongoDatabase.GetCollection<object>(collectionName);

                    // Analyze MongoDB indexes and create PostgreSQL strategies
                    var strategies =
                        await _indexOptimizationService.AnalyzeAndCreateIndexStrategiesAsync(
                            collection,
                            collectionName,
                            config.IndexOptimizationOptions,
                            cancellationToken
                        );

                    allStrategies.AddRange(strategies);

                    _logger.LogInformation(
                        "Created {StrategyCount} index strategies for collection {CollectionName}",
                        strategies.Count(),
                        collectionName
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to analyze indexes for collection {CollectionName}: {Error}",
                        collectionName,
                        ex.Message
                    );
                }
            }

            if (!allStrategies.Any())
            {
                _logger.LogInformation("No index strategies to implement");
                return;
            }

            // Create indexes in PostgreSQL
            _logger.LogInformation(
                "Creating {IndexCount} indexes in PostgreSQL",
                allStrategies.Count
            );

            var indexResults = await _indexOptimizationService.CreateIndexesAsync(
                allStrategies,
                config.PostgreSqlConnectionString,
                config.IndexOptimizationOptions,
                cancellationToken
            );

            var successCount = indexResults.Count(r => r.IsSuccess);
            var failureCount = indexResults.Count(r => !r.IsSuccess);

            _logger.LogInformation(
                "Index optimization completed: {SuccessCount} successful, {FailureCount} failed",
                successCount,
                failureCount
            );

            // Log any failures
            foreach (var failure in indexResults.Where(r => !r.IsSuccess))
            {
                _logger.LogWarning(
                    "Failed to create index {IndexName}: {Error}",
                    failure.IndexName,
                    failure.ErrorMessage
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Index optimization failed for migration {MigrationId}: {Error}",
                migrationId,
                ex.Message
            );
            // Don't throw - index optimization failure shouldn't stop migration
        }
    }

    private async Task<List<string>> GetSupportedCollectionsAsync(
        IMongoDatabase mongoDatabase,
        CancellationToken cancellationToken
    )
    {
        var allCollections = await mongoDatabase.ListCollectionNamesAsync(
            cancellationToken: cancellationToken
        );
        var allCollectionsList = await allCollections.ToListAsync(cancellationToken);

        // Filter to supported collections from transformation service
        var supportedCollections = _transformationService.GetSupportedCollections().ToHashSet();

        return allCollectionsList.Where(c => supportedCollections.Contains(c)).ToList();
    }

    /// <summary>
    /// Check if an exception is a duplicate key violation (unique constraint)
    /// </summary>
    private static bool IsDuplicateKeyViolation(DbUpdateException ex)
    {
        // PostgreSQL error code 23505 = unique_violation
        var innerException = ex.InnerException;
        if (innerException == null)
        {
            return false;
        }

        // Check for Npgsql PostgresException
        var exceptionType = innerException.GetType().Name;
        if (exceptionType == "PostgresException")
        {
            // Use reflection to get the SqlState property
            var sqlStateProperty = innerException.GetType().GetProperty("SqlState");
            if (sqlStateProperty != null)
            {
                var sqlState = sqlStateProperty.GetValue(innerException) as string;
                return sqlState == "23505"; // unique_violation
            }
        }

        // Fallback: check exception message for common duplicate key patterns
        var message = innerException.Message?.ToLowerInvariant() ?? "";
        return message.Contains("duplicate key")
            || message.Contains("unique constraint")
            || message.Contains("violates unique constraint");
    }

    #endregion
}
