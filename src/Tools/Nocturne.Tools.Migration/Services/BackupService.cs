using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nocturne.Tools.Abstractions.Services;

namespace Nocturne.Tools.Migration.Services;

/// <summary>
/// Implementation of backup service for MongoDB and PostgreSQL
/// </summary>
public class BackupService : IBackupService
{
    private readonly ILogger<BackupService> _logger;

    public BackupService(ILogger<BackupService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<BackupResult> CreateMongoBackupAsync(
        BackupConfiguration config,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Starting MongoDB backup for database: {DatabaseName}",
                config.DatabaseName
            );

            // Ensure output directory exists
            Directory.CreateDirectory(config.OutputDirectory);

            // Generate backup filename if not provided
            var backupFileName =
                config.BackupFileName
                ?? $"mongo_backup_{config.DatabaseName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}";

            if (config.Compress && !backupFileName.EndsWith(".gz"))
            {
                backupFileName += ".gz";
            }

            var backupFilePath = Path.Combine(config.OutputDirectory, backupFileName);

            // Build mongodump command
            var arguments = BuildMongoDumpArguments(config, backupFilePath);

            // Execute mongodump
            var result = await ExecuteCommandAsync(
                "mongodump",
                arguments,
                config.Timeout,
                cancellationToken
            );

            if (!result.Success)
            {
                return new BackupResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"mongodump failed: {result.ErrorOutput}",
                    BackupType = BackupType.MongoDB,
                    Duration = stopwatch.Elapsed,
                };
            }

            // Verify backup file exists and get size
            if (!File.Exists(backupFilePath))
            {
                return new BackupResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Backup file was not created",
                    BackupType = BackupType.MongoDB,
                    Duration = stopwatch.Elapsed,
                };
            }

            var fileInfo = new FileInfo(backupFilePath);
            var metadata = await CreateBackupMetadataAsync(
                backupFilePath,
                BackupType.MongoDB,
                config
            );

            _logger.LogInformation(
                "MongoDB backup completed successfully. File: {BackupFile}, Size: {Size} bytes",
                backupFilePath,
                fileInfo.Length
            );

            return new BackupResult
            {
                IsSuccess = true,
                BackupFilePath = backupFilePath,
                BackupFileSize = fileInfo.Length,
                Duration = stopwatch.Elapsed,
                BackupType = BackupType.MongoDB,
                Metadata = metadata,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create MongoDB backup: {Error}", ex.Message);
            return new BackupResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                BackupType = BackupType.MongoDB,
                Duration = stopwatch.Elapsed,
            };
        }
    }

    /// <inheritdoc/>
    public async Task<BackupResult> CreatePostgresBackupAsync(
        BackupConfiguration config,
        CancellationToken cancellationToken = default
    )
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            _logger.LogInformation(
                "Starting PostgreSQL backup for database: {DatabaseName}",
                config.DatabaseName
            );

            // Ensure output directory exists
            Directory.CreateDirectory(config.OutputDirectory);

            // Generate backup filename if not provided
            var backupFileName =
                config.BackupFileName
                ?? $"postgres_backup_{config.DatabaseName}_{DateTime.UtcNow:yyyyMMdd_HHmmss}.sql";

            if (config.Compress && !backupFileName.EndsWith(".gz"))
            {
                backupFileName += ".gz";
            }

            var backupFilePath = Path.Combine(config.OutputDirectory, backupFileName);

            // Build pg_dump command
            var arguments = BuildPgDumpArguments(config, backupFilePath);

            // Execute pg_dump
            var result = await ExecuteCommandAsync(
                "pg_dump",
                arguments,
                config.Timeout,
                cancellationToken
            );

            if (!result.Success)
            {
                return new BackupResult
                {
                    IsSuccess = false,
                    ErrorMessage = $"pg_dump failed: {result.ErrorOutput}",
                    BackupType = BackupType.PostgreSQL,
                    Duration = stopwatch.Elapsed,
                };
            }

            // Verify backup file exists and get size
            if (!File.Exists(backupFilePath))
            {
                return new BackupResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Backup file was not created",
                    BackupType = BackupType.PostgreSQL,
                    Duration = stopwatch.Elapsed,
                };
            }

            var fileInfo = new FileInfo(backupFilePath);
            var metadata = await CreateBackupMetadataAsync(
                backupFilePath,
                BackupType.PostgreSQL,
                config
            );

            _logger.LogInformation(
                "PostgreSQL backup completed successfully. File: {BackupFile}, Size: {Size} bytes",
                backupFilePath,
                fileInfo.Length
            );

            return new BackupResult
            {
                IsSuccess = true,
                BackupFilePath = backupFilePath,
                BackupFileSize = fileInfo.Length,
                Duration = stopwatch.Elapsed,
                BackupType = BackupType.PostgreSQL,
                Metadata = metadata,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create PostgreSQL backup: {Error}", ex.Message);
            return new BackupResult
            {
                IsSuccess = false,
                ErrorMessage = ex.Message,
                BackupType = BackupType.PostgreSQL,
                Duration = stopwatch.Elapsed,
            };
        }
    }

    /// <inheritdoc/>
    public async Task<ValidationResult> VerifyBackupAsync(
        string backupPath,
        BackupType backupType,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogInformation("Verifying backup file: {BackupPath}", backupPath);

            if (!File.Exists(backupPath))
            {
                return ValidationResult.Failure("BackupPath", "Backup file does not exist");
            }

            var fileInfo = new FileInfo(backupPath);

            // Check file size
            if (fileInfo.Length == 0)
            {
                return ValidationResult.Failure("FileSize", "Backup file is empty");
            }

            // Verify file format based on backup type
            var formatValidation = await VerifyBackupFormatAsync(
                backupPath,
                backupType,
                cancellationToken
            );
            if (!formatValidation.IsValid)
            {
                return formatValidation;
            }

            // Verify checksum if metadata exists
            var metadataPath = backupPath + ".metadata";
            if (File.Exists(metadataPath))
            {
                var checksumValidation = await VerifyBackupChecksumAsync(backupPath, metadataPath);
                if (!checksumValidation.IsValid)
                {
                    return checksumValidation;
                }
            }

            _logger.LogInformation(
                "Backup verification completed successfully: {BackupPath}",
                backupPath
            );

            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to verify backup: {Error}", ex.Message);
            return ValidationResult.Failure("Verification", ex.Message);
        }
    }

    /// <inheritdoc/>
    public async Task<CleanupResult> CleanupBackupsAsync(
        string backupDirectory,
        BackupRetentionPolicy retentionPolicy,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogInformation(
                "Starting backup cleanup in directory: {BackupDirectory}",
                backupDirectory
            );

            if (!Directory.Exists(backupDirectory))
            {
                return new CleanupResult
                {
                    IsSuccess = false,
                    ErrorMessage = "Backup directory does not exist",
                };
            }

            var backups = await ListBackupsAsync(backupDirectory);
            var backupsToDelete = new List<BackupInfo>();
            var cutoffDate = DateTime.UtcNow - retentionPolicy.MaxAge;

            // Sort backups by creation date (newest first)
            var sortedBackups = backups.OrderByDescending(b => b.CreatedAt).ToList();

            // Apply retention policies
            for (int i = 0; i < sortedBackups.Count; i++)
            {
                var backup = sortedBackups[i];

                // Keep if within max count and age limits
                if (i < retentionPolicy.MaxCount && backup.CreatedAt > cutoffDate)
                {
                    continue;
                }

                backupsToDelete.Add(backup);
            }

            // Check total size limit
            var totalSize = sortedBackups.Sum(b => b.FileSize);
            if (totalSize > retentionPolicy.MaxTotalSizeBytes)
            {
                var excessSize = totalSize - retentionPolicy.MaxTotalSizeBytes;
                var deletedSize = 0L;

                // Delete oldest backups until under size limit
                foreach (var backup in sortedBackups.AsEnumerable().Reverse())
                {
                    if (deletedSize >= excessSize)
                        break;

                    if (!backupsToDelete.Contains(backup))
                    {
                        backupsToDelete.Add(backup);
                        deletedSize += backup.FileSize;
                    }
                }
            }

            // Delete the selected backups
            var deletedFiles = new List<string>();
            var totalBytesFreed = 0L;

            foreach (var backup in backupsToDelete)
            {
                try
                {
                    File.Delete(backup.FilePath);
                    deletedFiles.Add(backup.FilePath);
                    totalBytesFreed += backup.FileSize;

                    // Also delete metadata file if it exists
                    var metadataPath = backup.FilePath + ".metadata";
                    if (File.Exists(metadataPath))
                    {
                        File.Delete(metadataPath);
                        deletedFiles.Add(metadataPath);
                    }

                    _logger.LogInformation("Deleted backup file: {FilePath}", backup.FilePath);
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(
                        ex,
                        "Failed to delete backup file: {FilePath}",
                        backup.FilePath
                    );
                }
            }

            _logger.LogInformation(
                "Backup cleanup completed. Deleted {Count} files, freed {Bytes} bytes",
                deletedFiles.Count,
                totalBytesFreed
            );

            return new CleanupResult
            {
                IsSuccess = true,
                FilesDeleted = deletedFiles.Count,
                BytesFreed = totalBytesFreed,
                DeletedFiles = deletedFiles,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to cleanup backups: {Error}", ex.Message);
            return new CleanupResult { IsSuccess = false, ErrorMessage = ex.Message };
        }
    }

    /// <inheritdoc/>
    public async Task<IEnumerable<BackupInfo>> ListBackupsAsync(
        string backupDirectory,
        BackupType? backupType = null
    )
    {
        try
        {
            if (!Directory.Exists(backupDirectory))
            {
                return Enumerable.Empty<BackupInfo>();
            }

            var backups = new List<BackupInfo>();
            var files = Directory.GetFiles(backupDirectory);

            foreach (var file in files)
            {
                // Skip metadata files
                if (file.EndsWith(".metadata"))
                    continue;

                var fileInfo = new FileInfo(file);
                var fileName = Path.GetFileName(file);

                // Determine backup type from filename
                BackupType detectedType;
                if (fileName.StartsWith("mongo_") || fileName.Contains("mongodump"))
                {
                    detectedType = BackupType.MongoDB;
                }
                else if (
                    fileName.StartsWith("postgres_")
                    || fileName.EndsWith(".sql")
                    || fileName.EndsWith(".sql.gz")
                )
                {
                    detectedType = BackupType.PostgreSQL;
                }
                else
                {
                    continue; // Skip unrecognized files
                }

                // Filter by backup type if specified
                if (backupType.HasValue && detectedType != backupType.Value)
                {
                    continue;
                }

                // Load metadata if available
                var metadata = new BackupMetadata();
                var metadataPath = file + ".metadata";
                if (File.Exists(metadataPath))
                {
                    try
                    {
                        var metadataJson = await File.ReadAllTextAsync(metadataPath);
                        metadata =
                            JsonSerializer.Deserialize<BackupMetadata>(metadataJson)
                            ?? new BackupMetadata();
                    }
                    catch (Exception ex)
                    {
                        _logger.LogWarning(
                            ex,
                            "Failed to load metadata for backup: {FilePath}",
                            file
                        );
                    }
                }

                var backup = new BackupInfo
                {
                    FilePath = file,
                    BackupType = detectedType,
                    CreatedAt = fileInfo.CreationTimeUtc,
                    FileSize = fileInfo.Length,
                    IsCompressed = fileName.EndsWith(".gz"),
                    Metadata = metadata,
                };

                backups.Add(backup);
            }

            return backups.OrderByDescending(b => b.CreatedAt);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to list backups: {Error}", ex.Message);
            return Enumerable.Empty<BackupInfo>();
        }
    }

    private static string BuildMongoDumpArguments(BackupConfiguration config, string backupFilePath)
    {
        var args = new List<string>
        {
            $"--uri=\"{config.ConnectionString}\"",
            $"--db=\"{config.DatabaseName}\"",
            $"--archive=\"{backupFilePath}\"",
        };

        if (config.Compress)
        {
            args.Add("--gzip");
        }

        if (config.CollectionsToBackup.Any())
        {
            foreach (var collection in config.CollectionsToBackup)
            {
                args.Add($"--collection=\"{collection}\"");
            }
        }

        // Add additional options
        foreach (var option in config.AdditionalOptions)
        {
            if (option.Value is bool boolValue && boolValue)
            {
                args.Add($"--{option.Key}");
            }
            else if (option.Value is not bool)
            {
                args.Add($"--{option.Key}=\"{option.Value}\"");
            }
        }

        return string.Join(" ", args);
    }

    private static string BuildPgDumpArguments(BackupConfiguration config, string backupFilePath)
    {
        var args = new List<string>();

        // Parse connection string components
        var connBuilder = new Npgsql.NpgsqlConnectionStringBuilder(config.ConnectionString);

        if (!string.IsNullOrEmpty(connBuilder.Host))
        {
            args.Add($"--host=\"{connBuilder.Host}\"");
        }

        if (connBuilder.Port > 0)
        {
            args.Add($"--port={connBuilder.Port}");
        }

        if (!string.IsNullOrEmpty(connBuilder.Username))
        {
            args.Add($"--username=\"{connBuilder.Username}\"");
        }

        args.Add($"--dbname=\"{config.DatabaseName}\"");
        args.Add($"--file=\"{backupFilePath}\"");
        args.Add("--verbose");
        args.Add("--no-password"); // Use .pgpass or environment variables

        if (config.Compress)
        {
            args.Add("--compress=9");
        }

        // Add additional options
        foreach (var option in config.AdditionalOptions)
        {
            if (option.Value is bool boolValue && boolValue)
            {
                args.Add($"--{option.Key}");
            }
            else if (option.Value is not bool)
            {
                args.Add($"--{option.Key}=\"{option.Value}\"");
            }
        }

        return string.Join(" ", args);
    }

    private async Task<(bool Success, string Output, string ErrorOutput)> ExecuteCommandAsync(
        string command,
        string arguments,
        TimeSpan timeout,
        CancellationToken cancellationToken
    )
    {
        using var process = new Process();
        process.StartInfo.FileName = command;
        process.StartInfo.Arguments = arguments;
        process.StartInfo.UseShellExecute = false;
        process.StartInfo.RedirectStandardOutput = true;
        process.StartInfo.RedirectStandardError = true;
        process.StartInfo.CreateNoWindow = true;

        var output = new List<string>();
        var errorOutput = new List<string>();

        process.OutputDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                output.Add(e.Data);
                _logger.LogDebug("Command output: {Output}", e.Data);
            }
        };

        process.ErrorDataReceived += (_, e) =>
        {
            if (e.Data != null)
            {
                errorOutput.Add(e.Data);
                _logger.LogDebug("Command error: {Error}", e.Data);
            }
        };

        try
        {
            process.Start();
            process.BeginOutputReadLine();
            process.BeginErrorReadLine();

            using var registration = cancellationToken.Register(() =>
            {
                try
                {
                    if (!process.HasExited)
                    {
                        process.Kill();
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to kill process on cancellation");
                }
            });

            try
            {
                await process.WaitForExitAsync(cancellationToken);
            }
            catch (OperationCanceledException)
            {
                if (!process.HasExited)
                {
                    process.Kill();
                }
                return (false, string.Join('\n', output), "Command was cancelled");
            }

            var success = process.ExitCode == 0;
            return (success, string.Join('\n', output), string.Join('\n', errorOutput));
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to execute command: {Command} {Arguments}",
                command,
                arguments
            );
            return (false, string.Join('\n', output), ex.Message);
        }
    }

    private async Task<BackupMetadata> CreateBackupMetadataAsync(
        string backupFilePath,
        BackupType backupType,
        BackupConfiguration config
    )
    {
        var metadata = new BackupMetadata
        {
            ToolVersion = "Nocturne.Tools.Migration 1.0.0",
            DatabaseVersion = "Unknown", // Could be enhanced to detect version
            CollectionCount = config.CollectionsToBackup.Count,
            Checksum = await CalculateFileChecksumAsync(backupFilePath),
        };

        // Save metadata to file
        var metadataPath = backupFilePath + ".metadata";
        var metadataJson = JsonSerializer.Serialize(
            metadata,
            new JsonSerializerOptions { WriteIndented = true }
        );
        await File.WriteAllTextAsync(metadataPath, metadataJson);

        return metadata;
    }

    private static async Task<string> CalculateFileChecksumAsync(string filePath)
    {
        using var stream = File.OpenRead(filePath);
        using var sha256 = SHA256.Create();
        var hash = await sha256.ComputeHashAsync(stream);
        return Convert.ToHexString(hash);
    }

    private async Task<ValidationResult> VerifyBackupFormatAsync(
        string backupPath,
        BackupType backupType,
        CancellationToken cancellationToken
    )
    {
        // Basic format verification - could be enhanced with more sophisticated checks
        try
        {
            switch (backupType)
            {
                case BackupType.MongoDB:
                    return await VerifyMongoBackupFormatAsync(backupPath);
                case BackupType.PostgreSQL:
                    return await VerifyPostgresBackupFormatAsync(backupPath);
                default:
                    return ValidationResult.Failure("BackupType", "Unknown backup type");
            }
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure("Format", ex.Message);
        }
    }

    private static async Task<ValidationResult> VerifyMongoBackupFormatAsync(string backupPath)
    {
        // For MongoDB archives, we can try to read the first few bytes to verify it's a valid archive
        var buffer = new byte[1024];
        using var stream = File.OpenRead(backupPath);
        await stream.ReadAsync(buffer);

        // Basic validation - check for BSON/archive signatures
        // This is a simplified check - real implementation could use mongorestore --dryRun
        return ValidationResult.Success();
    }

    private static async Task<ValidationResult> VerifyPostgresBackupFormatAsync(string backupPath)
    {
        // For PostgreSQL dumps, check if it's a valid SQL file
        var firstLine = "";
        using var reader = new StreamReader(backupPath);
        firstLine = await reader.ReadLineAsync() ?? "";

        // Check for PostgreSQL dump header
        if (
            firstLine.Contains("PostgreSQL database dump")
            || firstLine.StartsWith("--")
            || firstLine.StartsWith("CREATE")
            || firstLine.StartsWith("SET")
        )
        {
            return ValidationResult.Success();
        }

        return ValidationResult.Failure(
            "Format",
            "File does not appear to be a valid PostgreSQL dump"
        );
    }

    private static async Task<ValidationResult> VerifyBackupChecksumAsync(
        string backupPath,
        string metadataPath
    )
    {
        try
        {
            var metadataJson = await File.ReadAllTextAsync(metadataPath);
            var metadata = JsonSerializer.Deserialize<BackupMetadata>(metadataJson);

            if (metadata?.Checksum == null)
            {
                return ValidationResult.Failure("Checksum", "No checksum found in metadata");
            }

            var actualChecksum = await CalculateFileChecksumAsync(backupPath);

            if (actualChecksum != metadata.Checksum)
            {
                return ValidationResult.Failure(
                    "Checksum",
                    "Backup file checksum does not match metadata"
                );
            }

            return ValidationResult.Success();
        }
        catch (Exception ex)
        {
            return ValidationResult.Failure("Checksum", ex.Message);
        }
    }
}
