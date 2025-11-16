using System.ComponentModel;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nocturne.Tools.Migration.Data;
using Nocturne.Tools.Migration.Services;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Migration.Commands;

/// <summary>
/// Command to perform recovery operations
/// </summary>
public class RecoveryCommand : AsyncCommand<RecoveryCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--migration-id")]
        [Description("Migration ID to recover")]
        public required string MigrationId { get; init; }

        [CommandOption("--mongo-connection")]
        [Description("MongoDB connection string")]
        public required string MongoConnectionString { get; init; }

        [CommandOption("--mongo-database")]
        [Description("MongoDB database name")]
        public required string MongoDatabaseName { get; init; }

        [CommandOption("--postgres-connection")]
        [Description("PostgreSQL connection string")]
        public required string PostgresConnectionString { get; init; }

        [CommandOption("--recovery-strategy")]
        [Description("Specific recovery strategy to use (optional)")]
        public string? RecoveryStrategy { get; init; }

        [CommandOption("--max-retry-attempts")]
        [Description("Maximum number of retry attempts")]
        [DefaultValue(3)]
        public int MaxRetryAttempts { get; init; } = 3;

        [CommandOption("--create-backup")]
        [Description("Whether to create a backup before recovery")]
        [DefaultValue(true)]
        public bool CreateBackup { get; init; } = true;

        [CommandOption("--skip-problematic-data")]
        [Description("Whether to skip problematic data during recovery")]
        [DefaultValue(false)]
        public bool SkipProblematicData { get; init; } = false;
    }

    private readonly ILogger<RecoveryCommand> _logger;
    private readonly IServiceProvider _serviceProvider;

    public RecoveryCommand(ILogger<RecoveryCommand> logger, IServiceProvider serviceProvider)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider =
            serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
    }

    /// <summary>
    /// Perform automatic recovery of a failed migration
    /// </summary>
    /// <param name="context">Command context</param>
    /// <param name="settings">Command settings</param>
    /// <returns>Exit code</returns>
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                "Starting recovery for migration {MigrationId}",
                settings.MigrationId
            );

            // Create recovery configuration
            var config = new RecoveryConfiguration
            {
                MigrationId = settings.MigrationId,
                MongoConnectionString = settings.MongoConnectionString,
                MongoDatabaseName = settings.MongoDatabaseName,
                PostgreSqlConnectionString = settings.PostgresConnectionString,
                RecoveryType = string.IsNullOrEmpty(settings.RecoveryStrategy)
                    ? RecoveryType.Auto
                    : RecoveryType.Manual,
                RecoveryStrategy = settings.RecoveryStrategy,
                MaxRetryAttempts = settings.MaxRetryAttempts,
                CreateBackupBeforeRecovery = settings.CreateBackup,
                SkipProblematicData = settings.SkipProblematicData,
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
            serviceCollection.AddSingleton<IRecoveryService, RecoveryService>();

            var recoveryServiceProvider = serviceCollection.BuildServiceProvider();

            // Create recovery service
            var recoveryService = recoveryServiceProvider.GetRequiredService<IRecoveryService>();

            // Validate recovery is possible
            _logger.LogInformation("Validating recovery configuration...");
            var validation = await recoveryService.ValidateRecoveryAsync(settings.MigrationId);

            if (!validation.IsValid)
            {
                _logger.LogError("Recovery validation failed:");
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

            // Analyze failure
            _logger.LogInformation("Analyzing migration failure...");
            var analysis = await recoveryService.AnalyzeFailureAsync(settings.MigrationId);

            _logger.LogInformation("Failure analysis complete:");
            _logger.LogInformation("  Failure Type: {FailureType}", analysis.FailureType);
            _logger.LogInformation("  Description: {Description}", analysis.FailureDescription);
            _logger.LogInformation("  Root Cause: {RootCause}", analysis.RootCause);
            _logger.LogInformation(
                "  Recovery Likelihood: {Likelihood}%",
                analysis.RecoveryLikelihood
            );
            _logger.LogInformation(
                "  Requires Immediate Action: {Immediate}",
                analysis.RequiresImmediateAction
            );

            if (analysis.RecommendedStrategies.Any())
            {
                _logger.LogInformation("  Recommended Strategies:");
                foreach (var strategy in analysis.RecommendedStrategies.Take(3))
                {
                    _logger.LogInformation(
                        "    - {Name} (Success Rate: {Rate}%, Time: {Time})",
                        strategy.Name,
                        strategy.SuccessRate,
                        strategy.EstimatedTime
                    );
                }
            }

            // Run recovery
            _logger.LogInformation("Starting recovery operation...");
            var result = await recoveryService.RecoverAsync(config);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Recovery completed successfully!");
                _logger.LogInformation("Recovery ID: {RecoveryId}", result.RecoveryId);
                _logger.LogInformation("Strategy Used: {Strategy}", result.RecoveryStrategy);
                _logger.LogInformation("Duration: {Duration}", result.Statistics.Duration);
                _logger.LogInformation(
                    "Retry Attempts: {Attempts}",
                    result.Statistics.RetryAttempts
                );
                _logger.LogInformation(
                    "Documents Recovered: {Recovered}",
                    result.Statistics.DocumentsRecovered
                );
                _logger.LogInformation(
                    "Documents Skipped: {Skipped}",
                    result.Statistics.DocumentsSkipped
                );
                _logger.LogInformation(
                    "Connections Restored: {Connections}",
                    result.Statistics.ConnectionsRestored
                );
                _logger.LogInformation(
                    "Can Resume Migration: {CanResume}",
                    result.CanResumeMigration
                );

                if (!string.IsNullOrEmpty(result.ResumeCheckpointId))
                {
                    _logger.LogInformation(
                        "Resume Checkpoint ID: {CheckpointId}",
                        result.ResumeCheckpointId
                    );
                }

                _logger.LogInformation("Recovery operations performed:");
                foreach (var operation in result.Operations)
                {
                    var status = operation.IsSuccess ? "SUCCESS" : "FAILED";
                    _logger.LogInformation(
                        "  {Type}: {Description} - {Status}",
                        operation.Type,
                        operation.Description,
                        status
                    );

                    if (!operation.IsSuccess && !string.IsNullOrEmpty(operation.ErrorMessage))
                    {
                        _logger.LogWarning("    Error: {Error}", operation.ErrorMessage);
                    }
                }

                return 0;
            }
            else
            {
                _logger.LogError("Recovery failed: {Error}", result.ErrorMessage);
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Recovery command failed: {Error}", ex.Message);
            return 1;
        }
    }
}
