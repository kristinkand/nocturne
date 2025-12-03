using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nocturne.Tools.Migration.Data;
using Nocturne.Tools.Migration.Services;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Migration.Commands;

/// <summary>
/// Command to perform rollback operations
/// </summary>
public class RollbackCommand : AsyncCommand<RollbackCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--migration-id")]
        [Description("Migration ID to rollback")]
        public required string MigrationId { get; init; }

        [CommandOption("--postgres-connection")]
        [Description("PostgreSQL connection string")]
        public required string PostgresConnectionString { get; init; }

        [CommandOption("--backup-file")]
        [Description("Path to backup file for restoration (optional)")]
        public string? BackupFilePath { get; init; }

        [CommandOption("--mongo-connection")]
        [Description("MongoDB connection string for data restoration (optional)")]
        public string? MongoConnectionString { get; init; }

        [CommandOption("--mongo-database")]
        [Description("MongoDB database name for data restoration (optional)")]
        public string? MongoDatabaseName { get; init; }

        [CommandOption("--drop-tables")]
        [Description("Whether to drop PostgreSQL tables during rollback")]
        [DefaultValue(true)]
        public bool DropTables { get; init; } = true;

        [CommandOption("--restore-mongo-data")]
        [Description("Whether to restore MongoDB data from backup")]
        [DefaultValue(false)]
        public bool RestoreMongoData { get; init; } = false;

        [CommandOption("--dry-run")]
        [Description("Whether this is a dry-run (validation only)")]
        [DefaultValue(false)]
        public bool DryRun { get; init; } = false;

        [CommandOption("--skip-confirmation")]
        [Description("Skip user confirmation prompt")]
        [DefaultValue(false)]
        public bool SkipConfirmation { get; init; } = false;
    }

    private readonly ILogger<RollbackCommand> _logger;
    private readonly IServiceProvider _serviceProvider;

    public RollbackCommand(ILogger<RollbackCommand> logger, IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider =
            serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Perform a full rollback of a migration
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
                "Starting rollback for migration {MigrationId}",
                settings.MigrationId
            );

            // Create rollback configuration
            var config = new RollbackConfiguration
            {
                MigrationId = settings.MigrationId,
                PostgreSqlConnectionString = settings.PostgresConnectionString,
                MongoConnectionString = settings.MongoConnectionString,
                MongoDatabaseName = settings.MongoDatabaseName,
                RollbackType = RollbackType.Full,
                BackupFilePath = settings.BackupFilePath,
                DropPostgreTables = settings.DropTables,
                RestoreMongoData = settings.RestoreMongoData,
                RequireConfirmation = !settings.SkipConfirmation,
                DryRun = settings.DryRun,
            };

            // Set up services
            using var scope = _serviceProvider.CreateScope();
            var serviceCollection = new ServiceCollection();
            serviceCollection.AddDbContext<MigrationDbContext>(options =>
                options.UseNpgsql(settings.PostgresConnectionString)
            );

            serviceCollection.AddLogging();
            serviceCollection.AddSingleton<IBackupService, BackupService>();
            serviceCollection.AddSingleton<IRollbackService, RollbackService>();

            var rollbackServiceProvider = serviceCollection.BuildServiceProvider();

            // Create rollback service
            var rollbackService = rollbackServiceProvider.GetRequiredService<IRollbackService>();

            // Validate rollback configuration
            _logger.LogInformation("Validating rollback configuration...");
            var validation = await rollbackService.ValidateRollbackAsync(config);

            if (!validation.IsValid)
            {
                _logger.LogError("Rollback validation failed:");
                foreach (var error in validation.Errors)
                {
                    _logger.LogError(
                        "  - {PropertyName}: {ErrorMessage}",
                        error.PropertyName,
                        error.ErrorMessage
                    );
                }
                return 1;
            }

            if (settings.DryRun)
            {
                _logger.LogInformation("Dry-run mode: Rollback validation completed successfully");
                return 0;
            }

            // Run rollback
            _logger.LogInformation("Starting rollback operation...");
            var result = await rollbackService.RollbackAsync(config);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Rollback completed successfully!");
                _logger.LogInformation("Rollback ID: {RollbackId}", result.RollbackId);
                _logger.LogInformation(
                    "Tables dropped: {TablesDropped}",
                    result.Statistics.TablesDropped
                );
                _logger.LogInformation(
                    "Documents restored: {DocumentsRestored}",
                    result.Statistics.DocumentsRestored
                );
                _logger.LogInformation("Duration: {Duration}", result.Statistics.Duration);
                _logger.LogInformation(
                    "Integrity verified: {IntegrityVerified}",
                    result.IntegrityVerified
                );

                foreach (var operation in result.Operations)
                {
                    var status = operation.IsSuccess ? "SUCCESS" : "FAILED";
                    _logger.LogInformation(
                        "Operation {Type}: {Description} - {Status}",
                        operation.Type,
                        operation.Description,
                        status
                    );

                    if (!operation.IsSuccess && !string.IsNullOrEmpty(operation.ErrorMessage))
                    {
                        _logger.LogWarning("  Error: {Error}", operation.ErrorMessage);
                    }
                }

                return 0;
            }
            else
            {
                _logger.LogError("Rollback failed: {Error}", result.ErrorMessage);
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Rollback command failed: {Error}", ex.Message);
            return 1;
        }
    }
}
