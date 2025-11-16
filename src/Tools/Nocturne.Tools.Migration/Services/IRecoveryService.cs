using Nocturne.Tools.Abstractions.Services;

namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Interface for recovery operations during migration failures
/// </summary>
public interface IRecoveryService
{
    /// <summary>
    /// Attempts to recover from a migration failure
    /// </summary>
    /// <param name="config">Recovery configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Recovery result</returns>
    Task<RecoveryResult> RecoverAsync(
        RecoveryConfiguration config,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Validates that recovery is possible for a failed migration
    /// </summary>
    /// <param name="migrationId">Migration identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result indicating if recovery is possible</returns>
    Task<ValidationResult> ValidateRecoveryAsync(
        string migrationId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Analyzes a migration failure to determine recovery options
    /// </summary>
    /// <param name="migrationId">Migration identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Analysis result with recommended recovery actions</returns>
    Task<FailureAnalysisResult> AnalyzeFailureAsync(
        string migrationId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Gets recovery status for an ongoing recovery operation
    /// </summary>
    /// <param name="recoveryId">Recovery identifier</param>
    /// <returns>Recovery status</returns>
    Task<RecoveryStatus> GetRecoveryStatusAsync(string recoveryId);

    /// <summary>
    /// Lists available recovery strategies for a specific failure type
    /// </summary>
    /// <param name="failureType">Type of failure</param>
    /// <returns>List of available recovery strategies</returns>
    Task<IEnumerable<RecoveryStrategy>> GetRecoveryStrategiesAsync(FailureType failureType);
}

/// <summary>
/// Configuration for recovery operations
/// </summary>
public class RecoveryConfiguration
{
    /// <summary>
    /// Migration ID to recover
    /// </summary>
    public required string MigrationId { get; set; }

    /// <summary>
    /// Type of recovery to perform
    /// </summary>
    public RecoveryType RecoveryType { get; set; } = RecoveryType.Auto;

    /// <summary>
    /// Specific recovery strategy to use
    /// </summary>
    public string? RecoveryStrategy { get; set; }

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
    /// Original migration configuration
    /// </summary>
    public MigrationEngineConfiguration? OriginalMigrationConfig { get; set; }

    /// <summary>
    /// Whether to create a backup before recovery
    /// </summary>
    public bool CreateBackupBeforeRecovery { get; set; } = true;

    /// <summary>
    /// Maximum number of retry attempts
    /// </summary>
    public int MaxRetryAttempts { get; set; } = 3;

    /// <summary>
    /// Delay between retry attempts
    /// </summary>
    public TimeSpan RetryDelay { get; set; } = TimeSpan.FromMinutes(1);

    /// <summary>
    /// Whether to skip problematic collections/documents
    /// </summary>
    public bool SkipProblematicData { get; set; } = false;

    /// <summary>
    /// Timeout for recovery operations
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromHours(2);

    /// <summary>
    /// Additional recovery options
    /// </summary>
    public Dictionary<string, object> AdditionalOptions { get; set; } = new();
}

/// <summary>
/// Result of a recovery operation
/// </summary>
public class RecoveryResult
{
    /// <summary>
    /// Recovery identifier
    /// </summary>
    public required string RecoveryId { get; init; }

    /// <summary>
    /// Whether the recovery was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if recovery failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Recovery strategy that was applied
    /// </summary>
    public required string RecoveryStrategy { get; init; }

    /// <summary>
    /// Statistics about the recovery operation
    /// </summary>
    public RecoveryStatistics Statistics { get; init; } = new();

    /// <summary>
    /// Operations performed during recovery
    /// </summary>
    public List<RecoveryOperation> Operations { get; init; } = new();

    /// <summary>
    /// Whether the migration can be resumed after recovery
    /// </summary>
    public bool CanResumeMigration { get; init; }

    /// <summary>
    /// Checkpoint ID for resuming migration (if applicable)
    /// </summary>
    public string? ResumeCheckpointId { get; init; }
}

/// <summary>
/// Statistics about a recovery operation
/// </summary>
public class RecoveryStatistics
{
    /// <summary>
    /// Start time of the recovery
    /// </summary>
    public DateTime StartTime { get; init; } = DateTime.UtcNow;

    /// <summary>
    /// End time of the recovery
    /// </summary>
    public DateTime? EndTime { get; set; }

    /// <summary>
    /// Total duration of the recovery
    /// </summary>
    public TimeSpan Duration => (EndTime ?? DateTime.UtcNow) - StartTime;

    /// <summary>
    /// Number of retry attempts made
    /// </summary>
    public int RetryAttempts { get; set; }

    /// <summary>
    /// Number of problematic documents skipped
    /// </summary>
    public long DocumentsSkipped { get; set; }

    /// <summary>
    /// Number of documents recovered
    /// </summary>
    public long DocumentsRecovered { get; set; }

    /// <summary>
    /// Number of connections restored
    /// </summary>
    public int ConnectionsRestored { get; set; }

    /// <summary>
    /// Resources freed during recovery (bytes)
    /// </summary>
    public long ResourcesFreed { get; set; }
}

/// <summary>
/// Status of an ongoing recovery operation
/// </summary>
public class RecoveryStatus
{
    /// <summary>
    /// Recovery identifier
    /// </summary>
    public required string RecoveryId { get; init; }

    /// <summary>
    /// Current state of the recovery
    /// </summary>
    public RecoveryState State { get; set; }

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
    public RecoveryStatistics Statistics { get; set; } = new();

    /// <summary>
    /// Estimated time remaining
    /// </summary>
    public TimeSpan? EstimatedTimeRemaining { get; set; }
}

/// <summary>
/// Result of failure analysis
/// </summary>
public class FailureAnalysisResult
{
    /// <summary>
    /// Type of failure detected
    /// </summary>
    public FailureType FailureType { get; init; }

    /// <summary>
    /// Detailed description of the failure
    /// </summary>
    public required string FailureDescription { get; init; }

    /// <summary>
    /// Root cause of the failure (if determined)
    /// </summary>
    public string? RootCause { get; init; }

    /// <summary>
    /// Recommended recovery strategies
    /// </summary>
    public List<RecoveryStrategy> RecommendedStrategies { get; init; } = new();

    /// <summary>
    /// Likelihood of successful recovery (0-100)
    /// </summary>
    public double RecoveryLikelihood { get; init; }

    /// <summary>
    /// Whether immediate action is required
    /// </summary>
    public bool RequiresImmediateAction { get; init; }

    /// <summary>
    /// Additional diagnostic information
    /// </summary>
    public Dictionary<string, object> DiagnosticData { get; init; } = new();
}

/// <summary>
/// A recovery strategy that can be applied
/// </summary>
public class RecoveryStrategy
{
    /// <summary>
    /// Unique identifier for the strategy
    /// </summary>
    public required string Id { get; init; }

    /// <summary>
    /// Human-readable name of the strategy
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Detailed description of the strategy
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Applicable failure types
    /// </summary>
    public List<FailureType> ApplicableFailureTypes { get; init; } = new();

    /// <summary>
    /// Estimated success rate (0-100)
    /// </summary>
    public double SuccessRate { get; init; }

    /// <summary>
    /// Estimated recovery time
    /// </summary>
    public TimeSpan EstimatedTime { get; init; }

    /// <summary>
    /// Risk level of this strategy
    /// </summary>
    public RiskLevel RiskLevel { get; init; }

    /// <summary>
    /// Prerequisites for this strategy
    /// </summary>
    public List<string> Prerequisites { get; init; } = new();

    /// <summary>
    /// Configuration parameters for the strategy
    /// </summary>
    public Dictionary<string, object> Parameters { get; init; } = new();
}

/// <summary>
/// An operation performed during recovery
/// </summary>
public class RecoveryOperation
{
    /// <summary>
    /// Type of operation
    /// </summary>
    public RecoveryOperationType Type { get; init; }

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
/// Type of recovery operation
/// </summary>
public enum RecoveryType
{
    /// <summary>
    /// Automatic recovery based on failure analysis
    /// </summary>
    Auto,

    /// <summary>
    /// Manual recovery with specific strategy
    /// </summary>
    Manual,

    /// <summary>
    /// Resume from last successful checkpoint
    /// </summary>
    Resume,

    /// <summary>
    /// Retry with modified configuration
    /// </summary>
    Retry,

    /// <summary>
    /// Skip problematic data and continue
    /// </summary>
    Skip,
}

/// <summary>
/// State of a recovery operation
/// </summary>
public enum RecoveryState
{
    /// <summary>
    /// Recovery is being initialized
    /// </summary>
    Initializing,

    /// <summary>
    /// Analyzing failure
    /// </summary>
    Analyzing,

    /// <summary>
    /// Preparing recovery
    /// </summary>
    Preparing,

    /// <summary>
    /// Recovery is running
    /// </summary>
    Running,

    /// <summary>
    /// Verifying recovery
    /// </summary>
    Verifying,

    /// <summary>
    /// Recovery completed successfully
    /// </summary>
    Completed,

    /// <summary>
    /// Recovery failed
    /// </summary>
    Failed,

    /// <summary>
    /// Recovery was cancelled
    /// </summary>
    Cancelled,
}

/// <summary>
/// Type of failure that occurred
/// </summary>
public enum FailureType
{
    /// <summary>
    /// Network connectivity issues
    /// </summary>
    NetworkFailure,

    /// <summary>
    /// Database connection failure
    /// </summary>
    DatabaseConnectionFailure,

    /// <summary>
    /// Out of memory condition
    /// </summary>
    OutOfMemory,

    /// <summary>
    /// Disk space exhaustion
    /// </summary>
    DiskSpaceExhaustion,

    /// <summary>
    /// Data corruption detected
    /// </summary>
    DataCorruption,

    /// <summary>
    /// User-initiated cancellation
    /// </summary>
    UserCancellation,

    /// <summary>
    /// System crash or unexpected shutdown
    /// </summary>
    SystemCrash,

    /// <summary>
    /// Timeout during operation
    /// </summary>
    Timeout,

    /// <summary>
    /// Authentication or authorization failure
    /// </summary>
    AuthenticationFailure,

    /// <summary>
    /// Schema validation failure
    /// </summary>
    SchemaValidationFailure,

    /// <summary>
    /// Data transformation failure
    /// </summary>
    DataTransformationFailure,

    /// <summary>
    /// Unknown or unclassified failure
    /// </summary>
    Unknown,
}

/// <summary>
/// Risk level of a recovery strategy
/// </summary>
public enum RiskLevel
{
    /// <summary>
    /// Low risk - safe to execute automatically
    /// </summary>
    Low,

    /// <summary>
    /// Medium risk - requires careful consideration
    /// </summary>
    Medium,

    /// <summary>
    /// High risk - requires manual approval
    /// </summary>
    High,

    /// <summary>
    /// Critical risk - should only be used as last resort
    /// </summary>
    Critical,
}

/// <summary>
/// Type of recovery operation
/// </summary>
public enum RecoveryOperationType
{
    /// <summary>
    /// Failure analysis
    /// </summary>
    FailureAnalysis,

    /// <summary>
    /// Connection restoration
    /// </summary>
    ConnectionRestore,

    /// <summary>
    /// Memory cleanup
    /// </summary>
    MemoryCleanup,

    /// <summary>
    /// Disk space cleanup
    /// </summary>
    DiskCleanup,

    /// <summary>
    /// Data validation
    /// </summary>
    DataValidation,

    /// <summary>
    /// Checkpoint restoration
    /// </summary>
    CheckpointRestore,

    /// <summary>
    /// Configuration adjustment
    /// </summary>
    ConfigurationAdjustment,

    /// <summary>
    /// Retry operation
    /// </summary>
    Retry,

    /// <summary>
    /// Skip problematic data
    /// </summary>
    SkipData,

    /// <summary>
    /// Resource allocation
    /// </summary>
    ResourceAllocation,
}
