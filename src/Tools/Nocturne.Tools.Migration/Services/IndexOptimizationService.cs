using System.Diagnostics;
using System.Text;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Nocturne.Tools.Migration.Models;
using Npgsql;

namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Service for analyzing MongoDB indexes and creating optimized PostgreSQL index strategies
/// </summary>
public class IndexOptimizationService : IIndexOptimizationService
{
    private readonly ILogger<IndexOptimizationService> _logger;

    public IndexOptimizationService(ILogger<IndexOptimizationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PostgreSqlIndexStrategy>> AnalyzeAndCreateIndexStrategiesAsync(
        IMongoCollection<object> collection,
        string collectionName,
        IndexOptimizationOptions options,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation(
            "Analyzing MongoDB indexes for collection: {CollectionName}",
            collectionName
        );

        var strategies = new List<PostgreSqlIndexStrategy>();

        // Analyze existing MongoDB indexes
        var mongoIndexes = await AnalyzeMongoIndexesAsync(collection, cancellationToken);

        // Convert MongoDB indexes to PostgreSQL strategies
        strategies.AddRange(
            ConvertMongoIndexesToPostgreStrategies(mongoIndexes, collectionName, options)
        );

        // Add collection-specific optimizations
        var collectionStrategies = await CreateCollectionSpecificStrategiesAsync(
            collectionName,
            options
        );
        strategies.AddRange(collectionStrategies);

        // Remove duplicates and prioritize
        strategies = DeduplicateAndPrioritizeStrategies(strategies);

        _logger.LogInformation(
            "Created {StrategyCount} index strategies for collection {CollectionName}",
            strategies.Count,
            collectionName
        );

        return strategies;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<PostgreSqlIndexStrategy>> CreateCollectionSpecificStrategiesAsync(
        string collectionName,
        IndexOptimizationOptions options
    )
    {
        await Task.CompletedTask; // Make async

        var normalizedName = collectionName.ToLowerInvariant();
        return normalizedName switch
        {
            "entries" => CreateEntriesIndexStrategies(options),
            "treatments" => CreateTreatmentsIndexStrategies(options),
            "profiles" => CreateProfilesIndexStrategies(options),
            "devicestatus" => CreateDeviceStatusIndexStrategies(options),
            "food" => CreateFoodIndexStrategies(options),
            "activity" => CreateActivityIndexStrategies(options),
            "settings" => CreateSettingsIndexStrategies(options),
            "auth" => CreateAuthIndexStrategies(options),
            _ => new List<PostgreSqlIndexStrategy>(),
        };
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IndexCreationResult>> CreateIndexesAsync(
        IEnumerable<PostgreSqlIndexStrategy> strategies,
        string connectionString,
        IndexOptimizationOptions options,
        CancellationToken cancellationToken = default
    )
    {
        var results = new List<IndexCreationResult>();

        if (options.SkipIndexCreation)
        {
            _logger.LogInformation("Skipping index creation as requested in options");
            return results;
        }

        if (options.DeferIndexCreation)
        {
            _logger.LogInformation("Deferring index creation to post-migration as requested");
            return results;
        }

        // Sort strategies by priority
        var sortedStrategies = strategies.OrderByDescending(s => s.Priority).ToList();

        // Create indexes with controlled concurrency
        var semaphore = new SemaphoreSlim(
            options.MaxConcurrentIndexCreation,
            options.MaxConcurrentIndexCreation
        );
        var tasks = sortedStrategies.Select(async strategy =>
        {
            await semaphore.WaitAsync(cancellationToken);
            try
            {
                return await CreateSingleIndexAsync(
                    strategy,
                    connectionString,
                    options,
                    cancellationToken
                );
            }
            finally
            {
                semaphore.Release();
            }
        });

        results.AddRange(await Task.WhenAll(tasks));

        var successCount = results.Count(r => r.IsSuccess);
        var failureCount = results.Count(r => !r.IsSuccess);

        _logger.LogInformation(
            "Index creation completed: {SuccessCount} successful, {FailureCount} failed",
            successCount,
            failureCount
        );

        return results;
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<IndexDropResult>> DropExistingIndexesAsync(
        string tableName,
        string connectionString,
        IndexOptimizationOptions options,
        CancellationToken cancellationToken = default
    )
    {
        var results = new List<IndexDropResult>();

        if (!options.DropExistingIndexes)
        {
            return results;
        }

        _logger.LogInformation("Dropping existing indexes for table: {TableName}", tableName);

        using var connection = new NpgsqlConnection(connectionString);
        await connection.OpenAsync(cancellationToken);

        // Get existing indexes (excluding primary key and unique constraints)
        var indexQuery =
            @"
            SELECT indexname 
            FROM pg_indexes 
            WHERE tablename = @tableName 
            AND indexname NOT LIKE '%_pkey' 
            AND indexname NOT LIKE '%_key'";

        using var command = new NpgsqlCommand(indexQuery, connection);
        command.Parameters.AddWithValue("@tableName", tableName);

        var indexes = new List<string>();
        using var reader = await command.ExecuteReaderAsync(cancellationToken);
        while (await reader.ReadAsync(cancellationToken))
        {
            indexes.Add(reader.GetString(0));
        }

        // Drop each index
        foreach (var indexName in indexes)
        {
            var result = await DropSingleIndexAsync(indexName, connectionString, cancellationToken);
            results.Add(result);
        }

        return results;
    }

    private async Task<List<MongoIndexInfo>> AnalyzeMongoIndexesAsync(
        IMongoCollection<object> collection,
        CancellationToken cancellationToken
    )
    {
        var indexes = new List<MongoIndexInfo>();

        try
        {
            var indexesCursor = await collection.Indexes.ListAsync(cancellationToken);
            var indexDocs = await indexesCursor.ToListAsync(cancellationToken);

            foreach (var indexDoc in indexDocs)
            {
                if (indexDoc.TryGetValue("name", out var nameValue) && nameValue.IsString)
                {
                    var name = nameValue.AsString;

                    // Skip the default _id index
                    if (name == "_id_")
                        continue;

                    var indexInfo = new MongoIndexInfo
                    {
                        Name = name,
                        Keys = new Dictionary<string, int>(),
                    };

                    if (indexDoc.TryGetValue("key", out var keyValue) && keyValue.IsBsonDocument)
                    {
                        var keyDoc = keyValue.AsBsonDocument;
                        foreach (var element in keyDoc)
                        {
                            indexInfo.Keys[element.Name] = element.Value.ToInt32();
                        }
                    }

                    if (
                        indexDoc.TryGetValue("unique", out var uniqueValue) && uniqueValue.IsBoolean
                    )
                    {
                        indexInfo.IsUnique = uniqueValue.AsBoolean;
                    }

                    if (
                        indexDoc.TryGetValue("sparse", out var sparseValue) && sparseValue.IsBoolean
                    )
                    {
                        indexInfo.IsSparse = sparseValue.AsBoolean;
                    }

                    if (indexDoc.TryGetValue("partialFilterExpression", out var filterValue))
                    {
                        indexInfo.PartialFilterExpression = filterValue;
                    }

                    indexes.Add(indexInfo);
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to analyze MongoDB indexes for collection");
        }

        return indexes;
    }

    private List<PostgreSqlIndexStrategy> ConvertMongoIndexesToPostgreStrategies(
        List<MongoIndexInfo> mongoIndexes,
        string collectionName,
        IndexOptimizationOptions options
    )
    {
        var strategies = new List<PostgreSqlIndexStrategy>();

        foreach (var mongoIndex in mongoIndexes)
        {
            var strategy = new PostgreSqlIndexStrategy
            {
                IndexName = $"ix_{GetTableName(collectionName)}_{mongoIndex.Name}",
                TableName = GetTableName(collectionName),
                Columns = mongoIndex
                    .Keys.Select(kvp => new IndexColumn
                    {
                        ColumnName = MapMongoFieldToPostgresColumn(kvp.Key, collectionName),
                        Direction =
                            kvp.Value > 0
                                ? Models.SortDirection.Ascending
                                : Models.SortDirection.Descending,
                    })
                    .ToList(),
                IsUnique = mongoIndex.IsUnique,
                CreateConcurrently = options.CreateConcurrently,
                Description = $"Converted from MongoDB index: {mongoIndex.Name}",
                SourceCollection = collectionName,
                Priority = 1, // Lower priority for converted indexes
            };

            // Handle partial indexes
            if (mongoIndex.PartialFilterExpression != null)
            {
                strategy.IsPartial = true;
                strategy.PartialCondition = ConvertMongoFilterToPostgresSql(
                    mongoIndex.PartialFilterExpression,
                    collectionName
                );
            }

            strategies.Add(strategy);
        }

        return strategies;
    }

    private List<PostgreSqlIndexStrategy> CreateEntriesIndexStrategies(
        IndexOptimizationOptions options
    )
    {
        var strategies = new List<PostgreSqlIndexStrategy>();

        if (options.EnableTimeSeriesOptimizations)
        {
            // Time-series optimization: Primary time-based index
            strategies.Add(
                new PostgreSqlIndexStrategy
                {
                    IndexName = "ix_entries_date_mills_type",
                    TableName = "entries",
                    Columns = new List<IndexColumn>
                    {
                        new() { ColumnName = "date" },
                        new() { ColumnName = "type" },
                    },
                    Description = "Time-series optimization for entries by date and type",
                    EstimatedBenefit = PerformanceBenefit.Critical,
                    Priority = 10,
                }
            );

            // Covering index for common queries
            if (options.CreateCoveringIndexes)
            {
                strategies.Add(
                    new PostgreSqlIndexStrategy
                    {
                        IndexName = "ix_entries_date_sgv_type_covering",
                        TableName = "entries",
                        Columns = new List<IndexColumn>
                        {
                            new() { ColumnName = "date" },
                            new() { ColumnName = "sgv" },
                            new() { ColumnName = "type" },
                            new() { ColumnName = "device" },
                        },
                        Description = "Covering index for time-range queries with glucose values",
                        EstimatedBenefit = PerformanceBenefit.High,
                        Priority = 8,
                    }
                );
            }
        }

        // Type-specific partial indexes
        if (options.CreatePartialIndexes)
        {
            strategies.Add(
                new PostgreSqlIndexStrategy
                {
                    IndexName = "ix_entries_sgv_date_partial",
                    TableName = "entries",
                    Columns = new List<IndexColumn>
                    {
                        new() { ColumnName = "date" },
                        new() { ColumnName = "sgv" },
                    },
                    IsPartial = true,
                    PartialCondition = "type = 'sgv' AND sgv IS NOT NULL",
                    Description = "Partial index for sensor glucose values",
                    EstimatedBenefit = PerformanceBenefit.High,
                    Priority = 7,
                }
            );
        }

        // Device-based index
        strategies.Add(
            new PostgreSqlIndexStrategy
            {
                IndexName = "ix_entries_device_date",
                TableName = "entries",
                Columns = new List<IndexColumn>
                {
                    new() { ColumnName = "device" },
                    new() { ColumnName = "date" },
                },
                Description = "Device-specific time-series queries",
                EstimatedBenefit = PerformanceBenefit.Medium,
                Priority = 5,
            }
        );

        return strategies;
    }

    private List<PostgreSqlIndexStrategy> CreateTreatmentsIndexStrategies(
        IndexOptimizationOptions options
    )
    {
        var strategies = new List<PostgreSqlIndexStrategy>();

        // Event type and date compound index
        strategies.Add(
            new PostgreSqlIndexStrategy
            {
                IndexName = "ix_treatments_eventtype_date",
                TableName = "treatments",
                Columns = new List<IndexColumn>
                {
                    new() { ColumnName = "eventType" },
                    new() { ColumnName = "date" },
                },
                Description = "Treatment lookup by type and time",
                EstimatedBenefit = PerformanceBenefit.High,
                Priority = 9,
            }
        );

        // JSONB index for additional properties
        strategies.Add(
            new PostgreSqlIndexStrategy
            {
                IndexName = "ix_treatments_additional_properties_gin",
                TableName = "treatments",
                Columns = new List<IndexColumn> { new() { ColumnName = "additional_properties" } },
                IndexType = IndexType.Gin,
                Description = "GIN index for JSONB additional properties",
                EstimatedBenefit = PerformanceBenefit.Medium,
                Priority = 6,
            }
        );

        // Bolus calc JSONB index
        strategies.Add(
            new PostgreSqlIndexStrategy
            {
                IndexName = "ix_treatments_boluscalc_gin",
                TableName = "treatments",
                Columns = new List<IndexColumn> { new() { ColumnName = "boluscalc" } },
                IndexType = IndexType.Gin,
                Description = "GIN index for JSONB bolus calculations",
                EstimatedBenefit = PerformanceBenefit.Medium,
                Priority = 6,
            }
        );

        // Time-based partial indexes for common treatment types
        if (options.CreatePartialIndexes)
        {
            strategies.Add(
                new PostgreSqlIndexStrategy
                {
                    IndexName = "ix_treatments_insulin_date_partial",
                    TableName = "treatments",
                    Columns = new List<IndexColumn>
                    {
                        new() { ColumnName = "date" },
                        new() { ColumnName = "insulin" },
                    },
                    IsPartial = true,
                    PartialCondition = "insulin IS NOT NULL AND insulin > 0",
                    Description = "Partial index for insulin treatments",
                    EstimatedBenefit = PerformanceBenefit.Medium,
                    Priority = 5,
                }
            );
        }

        return strategies;
    }

    private List<PostgreSqlIndexStrategy> CreateProfilesIndexStrategies(
        IndexOptimizationOptions options
    )
    {
        var strategies = new List<PostgreSqlIndexStrategy>();

        // Time-based profile lookup
        strategies.Add(
            new PostgreSqlIndexStrategy
            {
                IndexName = "ix_profiles_startdate_defaultprofile",
                TableName = "profiles",
                Columns = new List<IndexColumn>
                {
                    new() { ColumnName = "startDate", Direction = Models.SortDirection.Descending },
                    new() { ColumnName = "defaultProfile" },
                },
                Description = "Profile lookup by time range",
                EstimatedBenefit = PerformanceBenefit.High,
                Priority = 8,
            }
        );

        // JSONB index for profile store
        strategies.Add(
            new PostgreSqlIndexStrategy
            {
                IndexName = "ix_profiles_store_gin",
                TableName = "profiles",
                Columns = new List<IndexColumn> { new() { ColumnName = "store" } },
                IndexType = IndexType.Gin,
                Description = "GIN index for JSONB profile store",
                EstimatedBenefit = PerformanceBenefit.Medium,
                Priority = 6,
            }
        );

        return strategies;
    }

    private List<PostgreSqlIndexStrategy> CreateDeviceStatusIndexStrategies(
        IndexOptimizationOptions options
    )
    {
        var strategies = new List<PostgreSqlIndexStrategy>();

        // Device and timestamp combination
        strategies.Add(
            new PostgreSqlIndexStrategy
            {
                IndexName = "ix_devicestatus_device_created",
                TableName = "devicestatus",
                Columns = new List<IndexColumn>
                {
                    new() { ColumnName = "device" },
                    new()
                    {
                        ColumnName = "created_at",
                        Direction = Models.SortDirection.Descending,
                    },
                },
                Description = "Device status lookup by device and time",
                EstimatedBenefit = PerformanceBenefit.High,
                Priority = 8,
            }
        );

        // JSONB index for additional properties
        strategies.Add(
            new PostgreSqlIndexStrategy
            {
                IndexName = "ix_devicestatus_additional_properties_gin",
                TableName = "devicestatus",
                Columns = new List<IndexColumn> { new() { ColumnName = "additional_properties" } },
                IndexType = IndexType.Gin,
                Description = "GIN index for JSONB additional properties",
                EstimatedBenefit = PerformanceBenefit.Medium,
                Priority = 6,
            }
        );

        return strategies;
    }

    private List<PostgreSqlIndexStrategy> CreateFoodIndexStrategies(
        IndexOptimizationOptions options
    )
    {
        var strategies = new List<PostgreSqlIndexStrategy>();

        // Food search optimization
        strategies.Add(
            new PostgreSqlIndexStrategy
            {
                IndexName = "ix_food_name_category",
                TableName = "food",
                Columns = new List<IndexColumn>
                {
                    new() { ColumnName = "name" },
                    new() { ColumnName = "category" },
                },
                Description = "Food search by name and category",
                EstimatedBenefit = PerformanceBenefit.High,
                Priority = 7,
            }
        );

        // Text search index for food names
        strategies.Add(
            new PostgreSqlIndexStrategy
            {
                IndexName = "ix_food_name_gin",
                TableName = "food",
                Columns = new List<IndexColumn>
                {
                    new()
                    {
                        ColumnName = "name",
                        Expression = "to_tsvector('english', COALESCE(name, ''))",
                    },
                },
                IndexType = IndexType.Gin,
                Description = "Full-text search index for food names",
                EstimatedBenefit = PerformanceBenefit.Medium,
                Priority = 5,
            }
        );

        return strategies;
    }

    private List<PostgreSqlIndexStrategy> CreateActivityIndexStrategies(
        IndexOptimizationOptions options
    )
    {
        var strategies = new List<PostgreSqlIndexStrategy>();

        // Activity audit log optimization
        strategies.Add(
            new PostgreSqlIndexStrategy
            {
                IndexName = "ix_activity_created_type",
                TableName = "activity",
                Columns = new List<IndexColumn>
                {
                    new()
                    {
                        ColumnName = "created_at",
                        Direction = Models.SortDirection.Descending,
                    },
                    new() { ColumnName = "activityType" },
                },
                Description = "Activity audit log queries by time and type",
                EstimatedBenefit = PerformanceBenefit.Medium,
                Priority = 6,
            }
        );

        return strategies;
    }

    private List<PostgreSqlIndexStrategy> CreateSettingsIndexStrategies(
        IndexOptimizationOptions options
    )
    {
        var strategies = new List<PostgreSqlIndexStrategy>();

        // Key-value lookup optimization
        strategies.Add(
            new PostgreSqlIndexStrategy
            {
                IndexName = "ix_settings_key_unique",
                TableName = "settings",
                Columns = new List<IndexColumn> { new() { ColumnName = "key" } },
                IsUnique = true,
                Description = "Unique key-value lookup for settings",
                EstimatedBenefit = PerformanceBenefit.High,
                Priority = 8,
            }
        );

        return strategies;
    }

    private List<PostgreSqlIndexStrategy> CreateAuthIndexStrategies(
        IndexOptimizationOptions options
    )
    {
        var strategies = new List<PostgreSqlIndexStrategy>();

        // Username lookup
        strategies.Add(
            new PostgreSqlIndexStrategy
            {
                IndexName = "ix_auth_username_unique",
                TableName = "auth",
                Columns = new List<IndexColumn> { new() { ColumnName = "username" } },
                IsUnique = true,
                Description = "Unique username lookup",
                EstimatedBenefit = PerformanceBenefit.Critical,
                Priority = 10,
            }
        );

        // Email lookup
        strategies.Add(
            new PostgreSqlIndexStrategy
            {
                IndexName = "ix_auth_email_unique",
                TableName = "auth",
                Columns = new List<IndexColumn> { new() { ColumnName = "email" } },
                IsUnique = true,
                Description = "Unique email lookup",
                EstimatedBenefit = PerformanceBenefit.High,
                Priority = 9,
            }
        );

        // Active users lookup
        if (options.CreatePartialIndexes)
        {
            strategies.Add(
                new PostgreSqlIndexStrategy
                {
                    IndexName = "ix_auth_active_users_partial",
                    TableName = "auth",
                    Columns = new List<IndexColumn>
                    {
                        new()
                        {
                            ColumnName = "last_login",
                            Direction = Models.SortDirection.Descending,
                        },
                    },
                    IsPartial = true,
                    PartialCondition = "is_active = true",
                    Description = "Partial index for active users",
                    EstimatedBenefit = PerformanceBenefit.Medium,
                    Priority = 6,
                }
            );
        }

        // JSONB indexes for roles and permissions
        strategies.Add(
            new PostgreSqlIndexStrategy
            {
                IndexName = "ix_auth_roles_gin",
                TableName = "auth",
                Columns = new List<IndexColumn> { new() { ColumnName = "roles" } },
                IndexType = IndexType.Gin,
                Description = "GIN index for JSONB roles",
                EstimatedBenefit = PerformanceBenefit.Medium,
                Priority = 6,
            }
        );

        strategies.Add(
            new PostgreSqlIndexStrategy
            {
                IndexName = "ix_auth_permissions_gin",
                TableName = "auth",
                Columns = new List<IndexColumn> { new() { ColumnName = "permissions" } },
                IndexType = IndexType.Gin,
                Description = "GIN index for JSONB permissions",
                EstimatedBenefit = PerformanceBenefit.Medium,
                Priority = 6,
            }
        );

        return strategies;
    }

    private async Task<IndexCreationResult> CreateSingleIndexAsync(
        PostgreSqlIndexStrategy strategy,
        string connectionString,
        IndexOptimizationOptions options,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var sqlStatement = GenerateIndexCreationSql(strategy);

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            using var command = new NpgsqlCommand(sqlStatement, connection);
            command.CommandTimeout = 0; // No timeout for index creation

            await command.ExecuteNonQueryAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation(
                "Successfully created index {IndexName} in {Duration}",
                strategy.IndexName,
                stopwatch.Elapsed
            );

            return new IndexCreationResult
            {
                IndexName = strategy.IndexName,
                IsSuccess = true,
                Duration = stopwatch.Elapsed,
                Strategy = strategy,
                SqlStatement = sqlStatement,
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Failed to create index {IndexName}: {Error}",
                strategy.IndexName,
                ex.Message
            );

            return new IndexCreationResult
            {
                IndexName = strategy.IndexName,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed,
                Strategy = strategy,
                SqlStatement = sqlStatement,
            };
        }
    }

    private async Task<IndexDropResult> DropSingleIndexAsync(
        string indexName,
        string connectionString,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            using var connection = new NpgsqlConnection(connectionString);
            await connection.OpenAsync(cancellationToken);

            var sql = $"DROP INDEX IF EXISTS {indexName}";
            using var command = new NpgsqlCommand(sql, connection);
            await command.ExecuteNonQueryAsync(cancellationToken);

            stopwatch.Stop();

            _logger.LogInformation("Successfully dropped index {IndexName}", indexName);

            return new IndexDropResult
            {
                IndexName = indexName,
                IsSuccess = true,
                Duration = stopwatch.Elapsed,
            };
        }
        catch (Exception ex)
        {
            stopwatch.Stop();

            _logger.LogError(
                ex,
                "Failed to drop index {IndexName}: {Error}",
                indexName,
                ex.Message
            );

            return new IndexDropResult
            {
                IndexName = indexName,
                IsSuccess = false,
                ErrorMessage = ex.Message,
                Duration = stopwatch.Elapsed,
            };
        }
    }

    private string GenerateIndexCreationSql(PostgreSqlIndexStrategy strategy)
    {
        var sql = new StringBuilder();

        sql.Append("CREATE ");

        if (strategy.IsUnique)
            sql.Append("UNIQUE ");

        sql.Append("INDEX ");

        if (strategy.CreateConcurrently)
            sql.Append("CONCURRENTLY ");

        sql.Append($"{strategy.IndexName} ");
        sql.Append($"ON {strategy.TableName} ");

        if (strategy.IndexType != IndexType.BTree)
            sql.Append($"USING {strategy.IndexType.ToString().ToLowerInvariant()} ");

        sql.Append("(");

        var columnParts = strategy.Columns.Select(col =>
        {
            var part = col.Expression ?? col.ColumnName;
            if (
                col.Direction == Models.SortDirection.Descending
                && strategy.IndexType == IndexType.BTree
            )
                part += " DESC";
            if (col.NullHandling == NullHandling.First && strategy.IndexType == IndexType.BTree)
                part += " NULLS FIRST";
            return part;
        });

        sql.Append(string.Join(", ", columnParts));
        sql.Append(")");

        if (strategy.IsPartial && !string.IsNullOrEmpty(strategy.PartialCondition))
        {
            sql.Append($" WHERE {strategy.PartialCondition}");
        }

        return sql.ToString();
    }

    private List<PostgreSqlIndexStrategy> DeduplicateAndPrioritizeStrategies(
        List<PostgreSqlIndexStrategy> strategies
    )
    {
        // Remove duplicate strategies (same table, same columns)
        var deduplicated = strategies
            .GroupBy(s => new
            {
                s.TableName,
                Columns = string.Join(",", s.Columns.Select(c => c.ColumnName)),
            })
            .Select(g => g.OrderByDescending(s => s.Priority).First())
            .OrderByDescending(s => s.Priority)
            .ToList();

        return deduplicated;
    }

    private string GetTableName(string collectionName)
    {
        return collectionName.ToLowerInvariant() switch
        {
            "entries" => "entries",
            "treatments" => "treatments",
            "profiles" => "profiles",
            "devicestatus" => "devicestatus",
            "food" => "food",
            "activity" => "activity",
            "settings" => "settings",
            "auth" => "auth",
            _ => collectionName.ToLowerInvariant(),
        };
    }

    private string MapMongoFieldToPostgresColumn(string mongoField, string collectionName)
    {
        // Handle common MongoDB field mappings
        return mongoField switch
        {
            "date" => "date",
            "mills" => "date",
            "_id" => "id",
            "created_at" => "created_at",
            _ => mongoField.ToLowerInvariant(),
        };
    }

    private string? ConvertMongoFilterToPostgresSql(object mongoFilter, string collectionName)
    {
        // This is a simplified conversion - in a real implementation,
        // you'd want a more sophisticated MongoDB filter to SQL converter
        try
        {
            if (mongoFilter is BsonDocument doc)
            {
                var conditions = new List<string>();
                foreach (var element in doc)
                {
                    var field = MapMongoFieldToPostgresColumn(element.Name, collectionName);
                    var value = element.Value;

                    if (value.IsString)
                    {
                        conditions.Add($"{field} = '{value.AsString}'");
                    }
                    else if (value.IsNumeric)
                    {
                        conditions.Add($"{field} = {value}");
                    }
                    else if (value.IsBoolean)
                    {
                        conditions.Add(
                            $"{field} = {value.AsBoolean.ToString().ToLowerInvariant()}"
                        );
                    }
                }
                return string.Join(" AND ", conditions);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to convert MongoDB filter to PostgreSQL WHERE clause");
        }

        return null;
    }
}
