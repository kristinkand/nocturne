namespace Nocturne.Tools.Migration.Models;

/// <summary>
/// Represents a PostgreSQL index creation strategy
/// </summary>
public class PostgreSqlIndexStrategy
{
    /// <summary>
    /// Index name
    /// </summary>
    public required string IndexName { get; set; }

    /// <summary>
    /// Target table name
    /// </summary>
    public required string TableName { get; set; }

    /// <summary>
    /// Columns to include in the index
    /// </summary>
    public required List<IndexColumn> Columns { get; set; }

    /// <summary>
    /// Index type (btree, gin, gist, hash, spgist, brin)
    /// </summary>
    public IndexType IndexType { get; set; } = IndexType.BTree;

    /// <summary>
    /// Whether this is a unique index
    /// </summary>
    public bool IsUnique { get; set; } = false;

    /// <summary>
    /// Whether this is a partial index with a WHERE condition
    /// </summary>
    public bool IsPartial { get; set; } = false;

    /// <summary>
    /// Partial index condition (WHERE clause)
    /// </summary>
    public string? PartialCondition { get; set; }

    /// <summary>
    /// Whether to create the index concurrently
    /// </summary>
    public bool CreateConcurrently { get; set; } = true;

    /// <summary>
    /// Index priority (higher number = higher priority)
    /// </summary>
    public int Priority { get; set; } = 0;

    /// <summary>
    /// Description of the index purpose
    /// </summary>
    public string? Description { get; set; }

    /// <summary>
    /// Estimated performance benefit
    /// </summary>
    public PerformanceBenefit EstimatedBenefit { get; set; } = PerformanceBenefit.Medium;

    /// <summary>
    /// Collection this index is optimized for
    /// </summary>
    public string? SourceCollection { get; set; }

    /// <summary>
    /// Additional index options (fillfactor, etc.)
    /// </summary>
    public Dictionary<string, object> Options { get; set; } = new();
}

/// <summary>
/// Represents a column in an index
/// </summary>
public class IndexColumn
{
    /// <summary>
    /// Column name
    /// </summary>
    public required string ColumnName { get; set; }

    /// <summary>
    /// Sort direction for ordered indexes
    /// </summary>
    public SortDirection Direction { get; set; } = SortDirection.Ascending;

    /// <summary>
    /// Null handling for ordered indexes
    /// </summary>
    public NullHandling NullHandling { get; set; } = NullHandling.Last;

    /// <summary>
    /// Expression for functional indexes (e.g., "LOWER(column_name)")
    /// </summary>
    public string? Expression { get; set; }

    /// <summary>
    /// Operator class for specialized indexing
    /// </summary>
    public string? OperatorClass { get; set; }
}

/// <summary>
/// Types of PostgreSQL indexes
/// </summary>
public enum IndexType
{
    /// <summary>
    /// B-tree index (default for most queries)
    /// </summary>
    BTree,

    /// <summary>
    /// GIN index (for JSONB, arrays, full-text search)
    /// </summary>
    Gin,

    /// <summary>
    /// GiST index (for geometric data, full-text search)
    /// </summary>
    Gist,

    /// <summary>
    /// Hash index (for equality comparisons only)
    /// </summary>
    Hash,

    /// <summary>
    /// SP-GiST index (for non-balanced data structures)
    /// </summary>
    SpGist,

    /// <summary>
    /// BRIN index (for very large tables with natural ordering)
    /// </summary>
    Brin,
}

/// <summary>
/// Sort direction for index columns
/// </summary>
public enum SortDirection
{
    /// <summary>
    /// Ascending order (default)
    /// </summary>
    Ascending,

    /// <summary>
    /// Descending order
    /// </summary>
    Descending,
}

/// <summary>
/// Null value handling in indexes
/// </summary>
public enum NullHandling
{
    /// <summary>
    /// NULL values first
    /// </summary>
    First,

    /// <summary>
    /// NULL values last (default)
    /// </summary>
    Last,
}

/// <summary>
/// Estimated performance benefit of an index
/// </summary>
public enum PerformanceBenefit
{
    /// <summary>
    /// Low performance improvement
    /// </summary>
    Low,

    /// <summary>
    /// Medium performance improvement
    /// </summary>
    Medium,

    /// <summary>
    /// High performance improvement
    /// </summary>
    High,

    /// <summary>
    /// Critical performance improvement
    /// </summary>
    Critical,
}

/// <summary>
/// Result of index creation operation
/// </summary>
public class IndexCreationResult
{
    /// <summary>
    /// Index name that was created
    /// </summary>
    public required string IndexName { get; set; }

    /// <summary>
    /// Whether the creation was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Error message if creation failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Time taken to create the index
    /// </summary>
    public TimeSpan Duration { get; set; }

    /// <summary>
    /// Index strategy that was used
    /// </summary>
    public required PostgreSqlIndexStrategy Strategy { get; set; }

    /// <summary>
    /// SQL statement that was executed
    /// </summary>
    public string? SqlStatement { get; set; }
}

/// <summary>
/// Result of index drop operation
/// </summary>
public class IndexDropResult
{
    /// <summary>
    /// Index name that was dropped
    /// </summary>
    public required string IndexName { get; set; }

    /// <summary>
    /// Whether the drop was successful
    /// </summary>
    public bool IsSuccess { get; set; }

    /// <summary>
    /// Error message if drop failed
    /// </summary>
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Time taken to drop the index
    /// </summary>
    public TimeSpan Duration { get; set; }
}

/// <summary>
/// Index analysis result from MongoDB
/// </summary>
public class MongoIndexAnalysis
{
    /// <summary>
    /// Collection name
    /// </summary>
    public required string CollectionName { get; set; }

    /// <summary>
    /// Existing MongoDB indexes
    /// </summary>
    public List<MongoIndexInfo> ExistingIndexes { get; set; } = new();

    /// <summary>
    /// Detected query patterns
    /// </summary>
    public List<QueryPattern> QueryPatterns { get; set; } = new();

    /// <summary>
    /// Recommended PostgreSQL indexes
    /// </summary>
    public List<PostgreSqlIndexStrategy> RecommendedIndexes { get; set; } = new();
}

/// <summary>
/// Information about a MongoDB index
/// </summary>
public class MongoIndexInfo
{
    /// <summary>
    /// Index name
    /// </summary>
    public required string Name { get; set; }

    /// <summary>
    /// Index keys and directions
    /// </summary>
    public required Dictionary<string, int> Keys { get; set; }

    /// <summary>
    /// Whether the index is unique
    /// </summary>
    public bool IsUnique { get; set; }

    /// <summary>
    /// Whether the index is sparse
    /// </summary>
    public bool IsSparse { get; set; }

    /// <summary>
    /// Partial filter expression
    /// </summary>
    public object? PartialFilterExpression { get; set; }
}

/// <summary>
/// Detected query pattern for optimization
/// </summary>
public class QueryPattern
{
    /// <summary>
    /// Fields commonly queried together
    /// </summary>
    public required List<string> Fields { get; set; }

    /// <summary>
    /// Query operation type
    /// </summary>
    public QueryOperationType OperationType { get; set; }

    /// <summary>
    /// Estimated frequency of this pattern
    /// </summary>
    public QueryFrequency Frequency { get; set; }

    /// <summary>
    /// Whether the pattern benefits from partial indexing
    /// </summary>
    public bool BenefitsFromPartialIndex { get; set; }

    /// <summary>
    /// Recommended partial index condition
    /// </summary>
    public string? PartialIndexCondition { get; set; }
}

/// <summary>
/// Types of query operations
/// </summary>
public enum QueryOperationType
{
    /// <summary>
    /// Equality queries
    /// </summary>
    Equality,

    /// <summary>
    /// Range queries
    /// </summary>
    Range,

    /// <summary>
    /// Sort operations
    /// </summary>
    Sort,

    /// <summary>
    /// Text search
    /// </summary>
    TextSearch,

    /// <summary>
    /// Array operations
    /// </summary>
    Array,

    /// <summary>
    /// JSONB operations
    /// </summary>
    JsonB,
}

/// <summary>
/// Query pattern frequency
/// </summary>
public enum QueryFrequency
{
    /// <summary>
    /// Rarely used
    /// </summary>
    Rare,

    /// <summary>
    /// Occasionally used
    /// </summary>
    Occasional,

    /// <summary>
    /// Frequently used
    /// </summary>
    Frequent,

    /// <summary>
    /// Very frequently used
    /// </summary>
    VeryFrequent,
}
