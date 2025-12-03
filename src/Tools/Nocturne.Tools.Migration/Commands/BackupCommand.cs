using System.ComponentModel;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nocturne.Tools.Migration.Services;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Migration.Commands;

/// <summary>
/// Command to create a MongoDB backup
/// </summary>
public class BackupCommand : AsyncCommand<BackupCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--mongo-connection")]
        [Description("MongoDB connection string")]
        public required string MongoConnectionString { get; init; }

        [CommandOption("--mongo-database")]
        [Description("MongoDB database name")]
        public required string MongoDatabaseName { get; init; }

        [CommandOption("--output-directory")]
        [Description("Output directory for backup files")]
        public required string OutputDirectory { get; init; }

        [CommandOption("--backup-filename")]
        [Description("Backup file name (optional)")]
        public string? BackupFileName { get; init; }

        [CommandOption("--collections")]
        [Description("Comma-separated list of collections to backup (optional)")]
        public string? Collections { get; init; }

        [CommandOption("--compress")]
        [Description("Whether to compress the backup")]
        [DefaultValue(true)]
        public bool Compress { get; init; } = true;

        [CommandOption("--verify")]
        [Description("Whether to verify backup integrity after creation")]
        [DefaultValue(true)]
        public bool Verify { get; init; } = true;
    }

    private readonly ILogger<BackupCommand> _logger;
    private readonly IServiceProvider _serviceProvider;

    public BackupCommand(ILogger<BackupCommand> logger, IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider =
            serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Create a MongoDB backup
    /// </summary>
    /// <param name="context">Command context</param>
    /// <param name="settings">Command settings</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exit code</returns>
    public override async Task<int> ExecuteAsync(
        CommandContext context,
        Settings settings,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogInformation(
                "Starting MongoDB backup for database: {DatabaseName}",
                settings.MongoDatabaseName
            );

            // Create backup configuration
            var config = new BackupConfiguration
            {
                ConnectionString = settings.MongoConnectionString,
                DatabaseName = settings.MongoDatabaseName,
                OutputDirectory = settings.OutputDirectory,
                BackupFileName = settings.BackupFileName,
                Compress = settings.Compress,
            };

            // Parse collections
            if (!string.IsNullOrWhiteSpace(settings.Collections))
            {
                config.CollectionsToBackup = settings
                    .Collections.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .ToList();
            }

            // Create backup service
            using var scope = _serviceProvider.CreateScope();
            var backupService = scope.ServiceProvider.GetRequiredService<IBackupService>();

            // Create backup
            _logger.LogInformation("Creating MongoDB backup...");
            var result = await backupService.CreateMongoBackupAsync(config);

            if (result.IsSuccess)
            {
                _logger.LogInformation("MongoDB backup created successfully!");
                _logger.LogInformation("Backup file: {BackupFile}", result.BackupFilePath);
                _logger.LogInformation("File size: {FileSize} bytes", result.BackupFileSize);
                _logger.LogInformation("Duration: {Duration}", result.Duration);

                if (result.Metadata.Checksum != null)
                {
                    _logger.LogInformation("Checksum: {Checksum}", result.Metadata.Checksum);
                }

                // Verify backup if requested
                if (settings.Verify && !string.IsNullOrEmpty(result.BackupFilePath))
                {
                    _logger.LogInformation("Verifying backup integrity...");
                    var verification = await backupService.VerifyBackupAsync(
                        result.BackupFilePath,
                        BackupType.MongoDB
                    );

                    if (verification.IsValid)
                    {
                        _logger.LogInformation("Backup verification successful");
                    }
                    else
                    {
                        _logger.LogError("Backup verification failed:");
                        foreach (var error in verification.Errors)
                        {
                            _logger.LogError(
                                "  - {PropertyName}: {ErrorMessage}",
                                error.PropertyName,
                                error.ErrorMessage
                            );
                        }
                        return 1;
                    }
                }

                return 0;
            }
            else
            {
                _logger.LogError("MongoDB backup failed: {Error}", result.ErrorMessage);
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "MongoDB backup command failed: {Error}", ex.Message);
            return 1;
        }
    }
}
