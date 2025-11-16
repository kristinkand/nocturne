using Nocturne.Tools.Abstractions.Services;

namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Interface for backup operations supporting both MongoDB and PostgreSQL
/// </summary>
public interface IBackupService
{
    /// <summary>
    /// Creates a backup of MongoDB data before migration
    /// </summary>
    /// <param name="config">Backup configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Backup result with file information</returns>
    Task<BackupResult> CreateMongoBackupAsync(
        BackupConfiguration config,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Creates a backup of PostgreSQL data
    /// </summary>
    /// <param name="config">Backup configuration</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Backup result with file information</returns>
    Task<BackupResult> CreatePostgresBackupAsync(
        BackupConfiguration config,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Verifies the integrity of a backup file
    /// </summary>
    /// <param name="backupPath">Path to the backup file</param>
    /// <param name="backupType">Type of backup (MongoDB or PostgreSQL)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Verification result</returns>
    Task<ValidationResult> VerifyBackupAsync(
        string backupPath,
        BackupType backupType,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Cleans up old backup files based on retention policy
    /// </summary>
    /// <param name="backupDirectory">Directory containing backups</param>
    /// <param name="retentionPolicy">Retention policy settings</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Cleanup result</returns>
    Task<CleanupResult> CleanupBackupsAsync(
        string backupDirectory,
        BackupRetentionPolicy retentionPolicy,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Lists available backup files
    /// </summary>
    /// <param name="backupDirectory">Directory containing backups</param>
    /// <param name="backupType">Type of backup to list (optional)</param>
    /// <returns>List of available backup files</returns>
    Task<IEnumerable<BackupInfo>> ListBackupsAsync(
        string backupDirectory,
        BackupType? backupType = null
    );
}

/// <summary>
/// Configuration for backup operations
/// </summary>
public class BackupConfiguration
{
    /// <summary>
    /// Connection string for the database
    /// </summary>
    public required string ConnectionString { get; set; }

    /// <summary>
    /// Database name
    /// </summary>
    public required string DatabaseName { get; set; }

    /// <summary>
    /// Output directory for backup files
    /// </summary>
    public required string OutputDirectory { get; set; }

    /// <summary>
    /// Backup file name (optional, will generate if not provided)
    /// </summary>
    public string? BackupFileName { get; set; }

    /// <summary>
    /// Whether to compress the backup
    /// </summary>
    public bool Compress { get; set; } = true;

    /// <summary>
    /// Collections to backup (MongoDB only, empty means all)
    /// </summary>
    public List<string> CollectionsToBackup { get; set; } = new();

    /// <summary>
    /// Additional backup options specific to database type
    /// </summary>
    public Dictionary<string, object> AdditionalOptions { get; set; } = new();

    /// <summary>
    /// Maximum time to wait for backup completion
    /// </summary>
    public TimeSpan Timeout { get; set; } = TimeSpan.FromHours(2);
}

/// <summary>
/// Result of a backup operation
/// </summary>
public class BackupResult
{
    /// <summary>
    /// Whether the backup was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if backup failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Path to the created backup file
    /// </summary>
    public string? BackupFilePath { get; init; }

    /// <summary>
    /// Size of the backup file in bytes
    /// </summary>
    public long BackupFileSize { get; init; }

    /// <summary>
    /// Duration of the backup operation
    /// </summary>
    public TimeSpan Duration { get; init; }

    /// <summary>
    /// Type of backup created
    /// </summary>
    public BackupType BackupType { get; init; }

    /// <summary>
    /// Metadata about the backup
    /// </summary>
    public BackupMetadata Metadata { get; init; } = new();
}

/// <summary>
/// Information about a backup file
/// </summary>
public class BackupInfo
{
    /// <summary>
    /// Path to the backup file
    /// </summary>
    public required string FilePath { get; init; }

    /// <summary>
    /// Type of backup
    /// </summary>
    public BackupType BackupType { get; init; }

    /// <summary>
    /// When the backup was created
    /// </summary>
    public DateTime CreatedAt { get; init; }

    /// <summary>
    /// Size of the backup file in bytes
    /// </summary>
    public long FileSize { get; init; }

    /// <summary>
    /// Database name that was backed up
    /// </summary>
    public string? DatabaseName { get; init; }

    /// <summary>
    /// Whether the backup is compressed
    /// </summary>
    public bool IsCompressed { get; init; }

    /// <summary>
    /// Backup metadata
    /// </summary>
    public BackupMetadata Metadata { get; init; } = new();
}

/// <summary>
/// Metadata about a backup
/// </summary>
public class BackupMetadata
{
    /// <summary>
    /// Version of the backup tool used
    /// </summary>
    public string? ToolVersion { get; init; }

    /// <summary>
    /// Database version that was backed up
    /// </summary>
    public string? DatabaseVersion { get; init; }

    /// <summary>
    /// Number of collections/tables backed up
    /// </summary>
    public int CollectionCount { get; init; }

    /// <summary>
    /// Total number of documents/rows backed up
    /// </summary>
    public long DocumentCount { get; init; }

    /// <summary>
    /// Checksum of the backup for verification
    /// </summary>
    public string? Checksum { get; init; }

    /// <summary>
    /// Additional metadata as key-value pairs
    /// </summary>
    public Dictionary<string, object> AdditionalData { get; init; } = new();
}

/// <summary>
/// Backup retention policy settings
/// </summary>
public class BackupRetentionPolicy
{
    /// <summary>
    /// Maximum age of backups to keep
    /// </summary>
    public TimeSpan MaxAge { get; set; } = TimeSpan.FromDays(7);

    /// <summary>
    /// Maximum number of backups to keep
    /// </summary>
    public int MaxCount { get; set; } = 10;

    /// <summary>
    /// Maximum total size of all backups in bytes
    /// </summary>
    public long MaxTotalSizeBytes { get; set; } = 10L * 1024 * 1024 * 1024; // 10GB
}

/// <summary>
/// Result of a cleanup operation
/// </summary>
public class CleanupResult
{
    /// <summary>
    /// Whether the cleanup was successful
    /// </summary>
    public bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if cleanup failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Number of files deleted
    /// </summary>
    public int FilesDeleted { get; init; }

    /// <summary>
    /// Total bytes freed by cleanup
    /// </summary>
    public long BytesFreed { get; init; }

    /// <summary>
    /// List of deleted file paths
    /// </summary>
    public List<string> DeletedFiles { get; init; } = new();
}

/// <summary>
/// Type of backup
/// </summary>
public enum BackupType
{
    /// <summary>
    /// MongoDB backup using mongodump
    /// </summary>
    MongoDB,

    /// <summary>
    /// PostgreSQL backup using pg_dump
    /// </summary>
    PostgreSQL,
}
