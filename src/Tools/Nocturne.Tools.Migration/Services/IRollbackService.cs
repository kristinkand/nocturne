using Nocturne.Tools.Abstractions.Services;

namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Interface for rollback operations
/// </summary>
public interface IRollbackService
{
    /// <summary>
    /// Performs a full rollback of a migration
    /// </summary>
    /// <param name="config">Rollback configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rollback result</returns>
    Task<RollbackResult> RollbackAsync(
        RollbackConfiguration config,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Performs a partial rollback for specific collections or time ranges
    /// </summary>
    /// <param name="config">Partial rollback configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Rollback result</returns>
    Task<RollbackResult> PartialRollbackAsync(
        PartialRollbackConfiguration config,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Validates a rollback operation without executing it (dry-run)
    /// </summary>
    /// <param name="config">Rollback configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result</returns>
    Task<ValidationResult> ValidateRollbackAsync(
        RollbackConfiguration config,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the status of an ongoing rollback operation
    /// </summary>
    /// <param name="rollbackId">Rollback identifier</param>
    /// <returns>Rollback status</returns>
    Task<RollbackStatus> GetRollbackStatusAsync(string rollbackId);

    /// <summary>
    /// Lists available rollback points for a migration
    /// </summary>
    /// <param name="migrationId">Migration identifier</param>
    /// <returns>List of available rollback points</returns>
    Task<IEnumerable<RollbackPoint>> ListRollbackPointsAsync(string migrationId);

    /// <summary>
    /// Creates a rollback point during migration
    /// </summary>
    /// <param name="migrationId">Migration identifier</param>
    /// <param name="description">Description of the rollback point</param>
    /// <param name="metadata">Additional metadata</param>
    /// <returns>Created rollback point</returns>
    Task<RollbackPoint> CreateRollbackPointAsync(
        string migrationId,
        string description,
        Dictionary<string, object>? metadata = null
    );
}

/// <summary>
/// Configuration for rollback operations
/// </summary>
public class RollbackConfiguration
{
    /// <summary>
    /// Migration ID to rollback
    /// </summary>
    public required string MigrationId { get; set; }

    /// <summary>
    /// PostgreSQL connection string
    /// </summary>
    public required string PostgreSqlConnectionString { get; set; }

    /// <summary>
    /// MongoDB connection string for restoration
    /// </summary>
    public string? MongoConnectionString { get; set; }

    /// <summary>
    /// MongoDB database name for restoration
    /// </summary>
    public string? MongoDatabaseName { get; set; }

    /// <summary>
    /// Type of rollback to perform
    /// </summary>
    public RollbackType RollbackType { get; set; } = RollbackType.Full;

    /// <summary>
    /// Path to backup file for restoration
    /// </summary>
    public string? BackupFilePath { get; set; }

    /// <summary>
    /// Rollback point to restore to (if using checkpoint-based rollback)
    /// </summary>
    public string? RollbackPointId { get; set; }

    /// <summary>
    /// Whether to drop PostgreSQL tables during rollback
    /// </summary>
    public bool DropPostgreTables { get; set; } = true;

    /// <summary>
    /// Whether to restore MongoDB data from backup
    /// </summary>
    public bool RestoreMongoData { get; set; } = false;

    /// <summary>
    /// Whether to require user confirmation before proceeding
    /// </summary>
    public bool RequireConfirmation { get; set; } = true;

    /// <summary>
    /// Whether this is a dry-run (validation only)
    /// </summary>
    public bool DryRun { get; set; } = false;

    /// <summary>
    /// Maximum time to wait for rollback completion
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromHours(1);

    /// <summary>
    /// Additional rollback options
    /// </summary>
    public Dictionary<string, object> AdditionalOptions { get; set; } = new();
}

/// <summary>
/// Configuration for partial rollback operations
/// </summary>
public class PartialRollbackConfiguration : RollbackConfiguration
{
    /// <summary>
    /// Specific collections to rollback (empty means all)
    /// </summary>
    public List<string> CollectionsToRollback { get; set; } = new();

    /// <summary>
    /// Start date for data to rollback (inclusive)
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// End date for data to rollback (inclusive)
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Specific document IDs to rollback
    /// </summary>
    public List<string> DocumentIds { get; set; } = new();

    /// <summary>
    /// Custom filter criteria for partial rollback
    /// </summary>
    public Dictionary<string, object> FilterCriteria { get; set; } = new();
}

/// <summary>
/// Result of a rollback operation
/// </summary>
public class RollbackResult
{
    /// <summary>
    /// Rollback identifier
    /// </summary>
    public required string RollbackId { get; init; }

    /// <summary>
    /// Whether the rollback was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if rollback failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Statistics about the rollback operation
    /// </summary>
    public RollbackStatistics Statistics { get; init; } = new();

    /// <summary>
    /// Operations performed during rollback
    /// </summary>
    public List<RollbackOperation> Operations { get; init; } = new();

    /// <summary>
    /// Whether data integrity was verified after rollback
    /// </summary>
    public bool IntegrityVerified { get; init; }

    /// <summary>
    /// Integrity verification details
    /// </summary>
    public string? IntegrityDetails { get; init; }
}

/// <summary>
/// Statistics about a rollback operation
/// </summary>
public class RollbackStatistics
{
    /// <summary>
    /// Start time of the rollback
    /// </summary>
    public DateTime StartTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// End time of the rollback
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Total duration of the rollback
    /// </summary>
    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;

    /// <summary>
    /// Number of tables dropped
    /// </summary>
    public int TablesDropped { get; set; }

    /// <summary>
    /// Number of indexes dropped
    /// </summary>
    public int IndexesDropped { get; set; }

    /// <summary>
    /// Number of documents restored (if applicable)
    /// </summary>
    public long DocumentsRestored { get; set; }

    /// <summary>
    /// Size of data restored in bytes
    /// </summary>
    public long DataSizeRestored { get; set; }

    /// <summary>
    /// Collections processed during rollback
    /// </summary>
    public Dictionary<string, RollbackCollectionStatistics> CollectionStats { get; init; } = new();
}

/// <summary>
/// Statistics for a specific collection during rollback
/// </summary>
public class RollbackCollectionStatistics
{
    /// <summary>
    /// Collection name
    /// </summary>
    public required string CollectionName { get; init; }

    /// <summary>
    /// Documents restored
    /// </summary>
    public long DocumentsRestored { get; init; }

    /// <summary>
    /// Documents failed to restore
    /// </summary>
    public long DocumentsFailed { get; init; }

    /// <summary>
    /// Time taken to rollback this collection
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Status of an ongoing rollback operation
/// </summary>
public class RollbackStatus
{
    /// <summary>
    /// Rollback identifier
    /// </summary>
    public required string RollbackId { get; init; }

    /// <summary>
    /// Current state of the rollback
    /// </summary>
    public RollbackState State { get; set; }

    /// <summary>
    /// Overall progress percentage (0-100)
    /// </summary>
    public double ProgressPercentage { get; set; }

    /// <summary>
    /// Current operation being performed
    /// </summary>
    public string? CurrentOperation { get; set; }

    /// <summary>
    /// Statistics so far
    /// </summary>
    public RollbackStatistics Statistics { get; set; } = new();

    /// <summary>
    /// Estimated time remaining
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }
}

/// <summary>
/// A rollback point that can be restored to
/// </summary>
public class RollbackPoint
{
    /// <summary>
    /// Unique identifier for the rollback point
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Migration this rollback point belongs to
    /// </summary>
    public required string MigrationId { get; init; }

    /// <summary>
    /// When this rollback point was created
    /// </summary>
    public DateTime CreatedAt { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Description of the rollback point
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Migration state at this point
    /// </summary>
    public RollbackPointState State { get; init; }

    /// <summary>
    /// Collections that had been migrated at this point
    /// </summary>
    public List<string> MigratedCollections { get; init; } = new();

    /// <summary>
    /// Statistics at this rollback point
    /// </summary>
    public Dictionary<string, object> Statistics { get; init; } = new();

    /// <summary>
    /// Additional metadata
    /// </summary>
    public Dictionary<string, object> Metadata { get; init; } = new();

    /// <summary>
    /// Path to backup file associated with this point (if any)
    /// </summary>
    public string? BackupFilePath { get; init; }
}

/// <summary>
/// An operation performed during rollback
/// </summary>
public class RollbackOperation
{
    /// <summary>
    /// Type of operation
    /// </summary>
    public RollbackOperationType Type { get; init; }

    /// <summary>
    /// Description of the operation
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// When the operation was performed
    /// </summary>
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the operation was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if operation failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Duration of the operation
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Additional operation details
    /// </summary>
    public Dictionary<string, object> Details { get; init; } = new();
}

/// <summary>
/// Type of rollback operation
/// </summary>
public enum RollbackType
{
    /// <summary>
    /// Full rollback - drop all migrated data and restore from backup
    /// </summary>
    Full,

    /// <summary>
    /// Schema only - drop tables and indexes but don't restore data
    /// </summary>
    SchemaOnly,

    /// <summary>
    /// Partial - rollback specific collections or data ranges
    /// </summary>
    Partial,

    /// <summary>
    /// Point in time - rollback to a specific checkpoint
    /// </summary>
    PointInTime,
}

/// <summary>
/// State of a rollback operation
/// </summary>
public enum RollbackState
{
    /// <summary>
    /// Rollback is being initialized
    /// </summary>
    Initializing,

    /// <summary>
    /// Validating rollback configuration
    /// </summary>
    Validating,

    /// <summary>
    /// Waiting for user confirmation
    /// </summary>
    AwaitingConfirmation,

    /// <summary>
    /// Rollback is running
    /// </summary>
    Running,

    /// <summary>
    /// Verifying data integrity after rollback
    /// </summary>
    Verifying,

    /// <summary>
    /// Rollback completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Rollback failed
    /// </summary>
    Failed,

    /// <summary>
    /// Rollback was cancelled
    /// </summary>
    Cancelled,
}

/// <summary>
/// State of a rollback point
/// </summary>
public enum RollbackPointState
{
    /// <summary>
    /// Before migration started
    /// </summary>
    PreMigration,

    /// <summary>
    /// During schema creation
    /// </summary>
    SchemaCreated,

    /// <summary>
    /// During data migration
    /// </summary>
    DataMigration,

    /// <summary>
    /// During index creation
    /// </summary>
    IndexCreation,

    /// <summary>
    /// Migration completed
    /// </summary>
    PostMigration,
}

/// <summary>
/// Type of rollback operation
/// </summary>
public enum RollbackOperationType
{
    /// <summary>
    /// Validation operation
    /// </summary>
    Validation,

    /// <summary>
    /// User confirmation
    /// </summary>
    Confirmation,

    /// <summary>
    /// Backup verification
    /// </summary>
    BackupVerification,

    /// <summary>
    /// Drop PostgreSQL table
    /// </summary>
    DropTable,

    /// <summary>
    /// Drop PostgreSQL index
    /// </summary>
    DropIndex,

    /// <summary>
    /// Restore MongoDB data
    /// </summary>
    RestoreData,

    /// <summary>
    /// Verify data integrity
    /// </summary>
    IntegrityCheck,

    /// <summary>
    /// Cleanup operation
    /// </summary>
    Cleanup,
}
