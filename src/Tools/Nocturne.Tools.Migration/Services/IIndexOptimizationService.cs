using MongoDB.Driver;
using Nocturne.Tools.Migration.Models;

namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Service for analyzing MongoDB indexes and creating optimized PostgreSQL index strategies
/// </summary>
public interface IIndexOptimizationService
{
    /// <summary>
    /// Analyzes MongoDB indexes for a collection and creates optimized PostgreSQL index strategies
    /// </summary>
    /// <param name="collection">MongoDB collection</param>
    /// <param name="collectionName">Name of the collection</param>
    /// <param name="options">Index optimization options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of PostgreSQL index strategies</returns>
    Task<IEnumerable<PostgreSqlIndexStrategy>> AnalyzeAndCreateIndexStrategiesAsync(
        IMongoCollection<object> collection,
        string collectionName,
        IndexOptimizationOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates collection-specific optimized index strategies based on common query patterns
    /// </summary>
    /// <param name="collectionName">Name of the collection</param>
    /// <param name="options">Index optimization options</param>
    /// <returns>Collection of PostgreSQL index strategies</returns>
    Task<IEnumerable<PostgreSqlIndexStrategy>> CreateCollectionSpecificStrategiesAsync(
        string collectionName,
        IndexOptimizationOptions options
    );

    /// <summary>
    /// Creates indexes in PostgreSQL based on the provided strategies
    /// </summary>
    /// <param name="strategies">Index strategies to implement</param>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="options">Index optimization options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Results of index creation operations</returns>
    Task<IEnumerable<IndexCreationResult>> CreateIndexesAsync(
        IEnumerable<PostgreSqlIndexStrategy> strategies,
        string connectionString,
        IndexOptimizationOptions options,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Drops existing indexes if specified in the options
    /// </summary>
    /// <param name="tableName">Table name</param>
    /// <param name="connectionString">PostgreSQL connection string</param>
    /// <param name="options">Index optimization options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Results of index drop operations</returns>
    Task<IEnumerable<IndexDropResult>> DropExistingIndexesAsync(
        string tableName,
        string connectionString,
        IndexOptimizationOptions options,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Options for index optimization strategies
/// </summary>
public class IndexOptimizationOptions
{
    /// <summary>
    /// Whether to create indexes concurrently (non-blocking)
    /// </summary>
    public bool CreateConcurrently { get; set; } = true;

    /// <summary>
    /// Whether to drop existing indexes before creating new ones
    /// </summary>
    public bool DropExistingIndexes { get; set; } = false;

    /// <summary>
    /// Whether to skip index creation entirely
    /// </summary>
    public bool SkipIndexCreation { get; set; } = false;

    /// <summary>
    /// Whether to defer index creation to post-migration
    /// </summary>
    public bool DeferIndexCreation { get; set; } = false;

    /// <summary>
    /// Maximum number of indexes to create concurrently
    /// </summary>
    public int MaxConcurrentIndexCreation { get; set; } = 2;

    /// <summary>
    /// Whether to analyze query patterns for optimization hints
    /// </summary>
    public bool AnalyzeQueryPatterns { get; set; } = true;

    /// <summary>
    /// Whether to create covering indexes where beneficial
    /// </summary>
    public bool CreateCoveringIndexes { get; set; } = true;

    /// <summary>
    /// Whether to create partial indexes for common filtered queries
    /// </summary>
    public bool CreatePartialIndexes { get; set; } = true;

    /// <summary>
    /// Whether to enable time-series optimizations for relevant collections
    /// </summary>
    public bool EnableTimeSeriesOptimizations { get; set; } = true;

    /// <summary>
    /// Custom index configurations for specific tables
    /// </summary>
    public Dictionary<string, List<CustomIndexConfiguration>> CustomIndexes { get; set; } = new();
}

/// <summary>
/// Custom index configuration for specific requirements
/// </summary>
public class CustomIndexConfiguration
{
    /// <summary>
    /// Index name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Columns to include in the index
    /// </summary>
    public required List<string> Columns { get; set; }

    /// <summary>
    /// Index type (btree, gin, gist, etc.)
    /// </summary>
    public string IndexType { get; set; } = "btree";

    /// <summary>
    /// Whether this is a unique index
    /// </summary>
    public bool IsUnique { get; set; } = false;

    /// <summary>
    /// Partial index condition (WHERE clause)
    /// </summary>
    public string? PartialCondition { get; set; }

    /// <summary>
    /// Additional index options
    /// </summary>
    public Dictionary<string, object> Options { get; set; } = new();
}
