using Nocturne.Tools.Abstractions.Services;
using Nocturne.Tools.Migration.Services;

namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Interface for the core migration engine that handles data transfer from MongoDB to PostgreSQL
/// with robust batch processing capabilities
/// </summary>
public interface IMigrationEngine
{
    /// <summary>
    /// Migrates data from MongoDB to PostgreSQL with batch processing and checkpoint support
    /// </summary>
    /// <param name="config">Migration configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Migration result</returns>
    Task<MigrationResult> MigrateAsync(
        MigrationEngineConfiguration config,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Resumes a migration from a checkpoint
    /// </summary>
    /// <param name="config">Migration configuration</param>
    /// <param name="checkpointId">Checkpoint identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Migration result</returns>
    Task<MigrationResult> ResumeAsync(
        MigrationEngineConfiguration config,
        string checkpointId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets the status of an ongoing migration
    /// </summary>
    /// <param name="migrationId">Migration identifier</param>
    /// <returns>Migration status</returns>
    Task<MigrationStatus> GetStatusAsync(string migrationId);

    /// <summary>
    /// Validates the migration configuration and connectivity with comprehensive schema and data validation
    /// </summary>
    /// <param name="config">Migration configuration</param>
    /// <param name="options">Validation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comprehensive validation result</returns>
    Task<Abstractions.Services.ValidationResult> ValidateAsync(
        MigrationEngineConfiguration config,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Performs comprehensive pre-migration validation including schema and conflict detection
    /// </summary>
    /// <param name="config">Migration configuration</param>
    /// <param name="options">Validation options</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Comprehensive validation result</returns>
    Task<Abstractions.Services.ValidationResult> ValidatePreMigrationAsync(
        MigrationEngineConfiguration config,
        ValidationOptions? options = null,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Configuration for the migration engine
/// </summary>
public class MigrationEngineConfiguration
{
    /// <summary>
    /// MongoDB connection string
    /// </summary>
    public required string MongoConnectionString { get; set; }

    /// <summary>
    /// MongoDB database name
    /// </summary>
    public required string MongoDatabaseName { get; set; }

    /// <summary>
    /// PostgreSQL connection string
    /// </summary>
    public required string PostgreSqlConnectionString { get; set; }

    /// <summary>
    /// Collections to migrate. If empty, all collections will be migrated.
    /// </summary>
    public List<string> CollectionsToMigrate { get; set; } = new();

    /// <summary>
    /// Batch size for processing documents
    /// </summary>
    public int BatchSize { get; set; } = 1000;

    /// <summary>
    /// Maximum memory usage in MB
    /// </summary>
    public long MaxMemoryUsageMB { get; set; } = 512;

    /// <summary>
    /// Maximum degree of parallelism
    /// </summary>
    public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

    /// <summary>
    /// Enable checkpointing for resume capability
    /// </summary>
    public bool EnableCheckpointing { get; set; } = true;

    /// <summary>
    /// Checkpoint interval (number of batches)
    /// </summary>
    public int CheckpointInterval { get; set; } = 100;

    /// <summary>
    /// Date range filter - start date
    /// </summary>
    public DateTime? StartDate { get; set; }

    /// <summary>
    /// Date range filter - end date
    /// </summary>
    public DateTime? EndDate { get; set; }

    /// <summary>
    /// Retry configuration
    /// </summary>
    public RetryConfiguration RetryConfig { get; set; } = new();

    /// <summary>
    /// Drop existing PostgreSQL tables before migration
    /// </summary>
    public bool DropExistingTables { get; set; } = false;

    /// <summary>
    /// Skip documents that violate unique constraints (duplicates)
    /// </summary>
    public bool SkipDuplicates { get; set; } = true;

    /// <summary>
    /// Validation options for pre-migration checks
    /// </summary>
    public Nocturne.Tools.Abstractions.Services.ValidationOptions ValidationOptions { get; set; } =
        new();

    /// <summary>
    /// Index optimization options for PostgreSQL performance tuning
    /// </summary>
    public IndexOptimizationOptions IndexOptimizationOptions { get; set; } = new();

    /// <summary>
    /// Data transformation options for document processing
    /// </summary>
    public TransformationOptions TransformationOptions { get; set; } = new();

    /// <summary>
    /// Backup configuration for creating backups before migration
    /// </summary>
    public BackupMigrationOptions BackupOptions { get; set; } = new();

    /// <summary>
    /// Rollback configuration options
    /// </summary>
    public RollbackMigrationOptions RollbackOptions { get; set; } = new();

    /// <summary>
    /// Recovery configuration options
    /// </summary>
    public RecoveryMigrationOptions RecoveryOptions { get; set; } = new();
}

/// <summary>
/// Retry configuration for failed operations
/// </summary>
public class RetryConfiguration
{
    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetries { get; set; } = 3;

    /// <summary>
    /// Initial delay between retries
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromSeconds(1);

    /// <summary>
    /// Maximum delay between retries
    /// </summary>
    public TimeSpan MaxDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Backoff multiplier
    /// </summary>
    public double BackoffMultiplier { get; set; } = 2.0;
}

/// <summary>
/// Result of a migration operation
/// </summary>
public class MigrationResult
{
    /// <summary>
    /// Migration identifier
    /// </summary>
    public required string MigrationId { get; init; }

    /// <summary>
    /// Whether the migration was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if migration failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Statistics about the migration
    /// </summary>
    public MigrationStatistics Statistics { get; init; } = new();

    /// <summary>
    /// Checkpoint ID for resuming if interrupted
    /// </summary>
    public string? CheckpointId { get; init; }

    /// <summary>
    /// Backup information created before migration
    /// </summary>
    public BackupInfo? PreMigrationBackup { get; init; }

    /// <summary>
    /// Available rollback points created during migration
    /// </summary>
    public List<RollbackPoint> RollbackPoints { get; init; } = new();
}

/// <summary>
/// Statistics about the migration
/// </summary>
public record MigrationStatistics
{
    /// <summary>
    /// Start time of the migration
    /// </summary>
    public DateTime StartTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// End time of the migration
    /// </summary>
    public DateTime? EndTime { get; init; }

    /// <summary>
    /// Total duration of the migration
    /// </summary>
    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;

    /// <summary>
    /// Total documents processed
    /// </summary>
    public long TotalDocumentsProcessed { get; init; }

    /// <summary>
    /// Total documents failed
    /// </summary>
    public long TotalDocumentsFailed { get; init; }

    /// <summary>
    /// Collections processed
    /// </summary>
    public Dictionary<string, CollectionStatistics> CollectionStats { get; init; } = new();

    /// <summary>
    /// Peak memory usage in MB
    /// </summary>
    public long PeakMemoryUsageMB { get; init; }
}

/// <summary>
/// Statistics for a specific collection
/// </summary>
public class CollectionStatistics
{
    /// <summary>
    /// Collection name
    /// </summary>
    public required string CollectionName { get; init; }

    /// <summary>
    /// Total documents in source collection
    /// </summary>
    public long TotalDocuments { get; init; }

    /// <summary>
    /// Documents successfully migrated
    /// </summary>
    public long DocumentsMigrated { get; init; }

    /// <summary>
    /// Documents that failed migration
    /// </summary>
    public long DocumentsFailed { get; init; }

    /// <summary>
    /// Time taken to migrate this collection
    /// </summary>
    public TimeSpan Duration { get; init; }
}

/// <summary>
/// Status of an ongoing migration
/// </summary>
public record MigrationStatus
{
    /// <summary>
    /// Migration identifier
    /// </summary>
    public required string MigrationId { get; init; }

    /// <summary>
    /// Current state of the migration
    /// </summary>
    public MigrationState State { get; init; }

    /// <summary>
    /// Overall progress percentage (0-100)
    /// </summary>
    public double ProgressPercentage { get; init; }

    /// <summary>
    /// Current operation being performed
    /// </summary>
    public string? CurrentOperation { get; init; }

    /// <summary>
    /// Statistics so far
    /// </summary>
    public MigrationStatistics Statistics { get; init; } = new();

    /// <summary>
    /// Estimated time remaining
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; init; }

    /// <summary>
    /// Current rollback status (if rollback is in progress)
    /// </summary>
    public RollbackStatus? RollbackStatus { get; init; }

    /// <summary>
    /// Current recovery status (if recovery is in progress)
    /// </summary>
    public RecoveryStatus? RecoveryStatus { get; init; }
}

/// <summary>
/// State of a migration
/// </summary>
public enum MigrationState
{
    /// <summary>
    /// Migration is being initialized
    /// </summary>
    Initializing,

    /// <summary>
    /// Migration is running
    /// </summary>
    Running,

    /// <summary>
    /// Migration is paused
    /// </summary>
    Paused,

    /// <summary>
    /// Migration completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Migration failed
    /// </summary>
    Failed,

    /// <summary>
    /// Migration was cancelled
    /// </summary>
    Cancelled,
}

/// <summary>
/// Backup options for migration operations
/// </summary>
public class BackupMigrationOptions
{
    /// <summary>
    /// Whether to create a backup before migration
    /// </summary>
    public bool CreatePreMigrationBackup { get; set; } = false;

    /// <summary>
    /// Whether to create MongoDB backup
    /// </summary>
    public bool CreateMongoBackup { get; set; } = true;

    /// <summary>
    /// Whether to create PostgreSQL backup (if target exists)
    /// </summary>
    public bool CreatePostgresBackup { get; set; } = false;

    /// <summary>
    /// Directory for storing backups
    /// </summary>
    public string BackupDirectory { get; set; } =
        Path.Combine(Path.GetTempPath(), "nocturne_backups");

    /// <summary>
    /// Whether to compress backup files
    /// </summary>
    public bool CompressBackups { get; set; } = true;

    /// <summary>
    /// Backup retention policy
    /// </summary>
    public BackupRetentionPolicy RetentionPolicy { get; set; } = new();

    /// <summary>
    /// Whether to verify backup integrity after creation
    /// </summary>
    public bool VerifyBackupIntegrity { get; set; } = true;
}

/// <summary>
/// Rollback options for migration operations
/// </summary>
public class RollbackMigrationOptions
{
    /// <summary>
    /// Whether to enable automatic rollback on failure
    /// </summary>
    public bool EnableAutoRollback { get; set; } = false;

    /// <summary>
    /// Types of failures that trigger automatic rollback
    /// </summary>
    public List<FailureType> AutoRollbackTriggers { get; set; } =
        new() { FailureType.DataCorruption };

    /// <summary>
    /// Whether to require user confirmation for rollback
    /// </summary>
    public bool RequireConfirmation { get; set; } = true;

    /// <summary>
    /// Maximum time to wait for user confirmation
    /// </summary>
    public TimeSpan ConfirmationTimeout { get; set; } = TimeSpan.FromMinutes(5);

    /// <summary>
    /// Whether to create rollback points during migration
    /// </summary>
    public bool CreateRollbackPoints { get; set; } = true;

    /// <summary>
    /// Interval for creating rollback points (number of collections)
    /// </summary>
    public int RollbackPointInterval { get; set; } = 1;
}

/// <summary>
/// Recovery options for migration operations
/// </summary>
public class RecoveryMigrationOptions
{
    /// <summary>
    /// Whether to enable automatic recovery attempts
    /// </summary>
    public bool EnableAutoRecovery { get; set; } = true;

    /// <summary>
    /// Maximum number of automatic recovery attempts
    /// </summary>
    public int MaxRecoveryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between recovery attempts
    /// </summary>
    public TimeSpan RecoveryDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Whether to create backups before recovery attempts
    /// </summary>
    public bool CreatePreRecoveryBackup { get; set; } = true;

    /// <summary>
    /// Recovery strategies to prefer
    /// </summary>
    public List<string> PreferredStrategies { get; set; } =
        new() { "resume_from_checkpoint", "retry_with_adjustment" };

    /// <summary>
    /// Whether to skip problematic data during recovery
    /// </summary>
    public bool AllowDataSkipping { get; set; } = false;

    /// <summary>
    /// Maximum percentage of data that can be skipped
    /// </summary>
    public double MaxDataSkipPercentage { get; set; } = 5.0;
}
