using System.ComponentModel;
using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Constants;
using Nocturne.Tools.Migration.Data;
using Nocturne.Tools.Migration.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Migration.Commands;

/// <summary>
/// Command to run PostgreSQL migration engine
/// </summary>
public class MigrateCommand : AsyncCommand<MigrateCommand.Settings>
{
    public class Settings : CommandSettings
    {
        [CommandOption("--mongo-connection")]
        [Description("MongoDB connection string (uses configuration default if not specified)")]
        public string? MongoConnectionString { get; init; }

        [CommandOption("--mongo-database")]
        [Description("MongoDB database name")]
        public required string MongoDatabaseName { get; init; }

        [CommandOption("--postgres-connection")]
        [Description("PostgreSQL connection string")]
        public string? PostgresConnectionString { get; init; }

        [CommandOption("--collections")]
        [Description("Comma-separated list of collections to migrate (optional)")]
        public string? Collections { get; init; }

        [CommandOption("--batch-size")]
        [Description("Batch size for processing documents")]
        [DefaultValue(1000)]
        public int BatchSize { get; init; } = 1000;

        [CommandOption("--max-memory-mb")]
        [Description("Maximum memory usage in MB")]
        [DefaultValue(512L)]
        public long MaxMemoryMb { get; init; } = 512;

        [CommandOption("--parallelism")]
        [Description("Maximum degree of parallelism")]
        [DefaultValue(0)]
        public int Parallelism { get; init; } = 0;

        [CommandOption("--drop-tables")]
        [Description("Drop existing PostgreSQL tables before migration")]
        [DefaultValue(false)]
        public bool DropTables { get; init; } = false;

        [CommandOption("--skip-duplicates")]
        [Description("Skip documents that violate unique constraints (enabled by default)")]
        [DefaultValue(true)]
        public bool SkipDuplicates { get; init; } = true;

        [CommandOption("--start-date")]
        [Description("Start date for filtering (ISO format, optional)")]
        public string? StartDate { get; init; }

        [CommandOption("--end-date")]
        [Description("End date for filtering (ISO format, optional)")]
        public string? EndDate { get; init; }

        [CommandOption("--skip-index-creation")]
        [Description("Skip index creation entirely")]
        [DefaultValue(false)]
        public bool SkipIndexCreation { get; init; } = false;

        [CommandOption("--defer-index-creation")]
        [Description("Defer index creation to post-migration")]
        [DefaultValue(false)]
        public bool DeferIndexCreation { get; init; } = false;

        [CommandOption("--drop-existing-indexes")]
        [Description("Drop existing indexes before creating new ones")]
        [DefaultValue(false)]
        public bool DropExistingIndexes { get; init; } = false;

        [CommandOption("--disable-concurrent-indexes")]
        [Description("Disable concurrent index creation")]
        [DefaultValue(false)]
        public bool DisableConcurrentIndexes { get; init; } = false;

        [CommandOption("--max-concurrent-indexes")]
        [Description("Maximum number of indexes to create concurrently")]
        [DefaultValue(2)]
        public int MaxConcurrentIndexes { get; init; } = 2;

        [CommandOption("--preserve-null-properties")]
        [Description("Preserve null values in additional_properties fields")]
        [DefaultValue(false)]
        public bool PreserveNullProperties { get; init; } = false;

        [CommandOption("--skip-connection-test")]
        [Description("Skip database connection test before migration")]
        [DefaultValue(false)]
        public bool SkipConnectionTest { get; init; } = false;

        [CommandOption("--skip-validation")]
        [Description("Skip schema validation (useful for re-running migrations)")]
        [DefaultValue(false)]
        public bool SkipValidation { get; init; } = false;
    }

    private readonly ILogger<MigrateCommand> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="MigrateCommand"/> class.
    /// </summary>
    /// <param name="logger">The logger instance.</param>
    /// <param name="serviceProvider">The service provider instance.</param>
    /// <param name="configuration">The configuration instance.</param>
    public MigrateCommand(
        ILogger<MigrateCommand> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider =
            serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <summary>
    /// Migrate data from MongoDB to PostgreSQL using the new migration engine
    /// </summary>
    /// <param name="context">Command context</param>
    /// <param name="settings">Command settings</param>
    /// <param name="cancellationToken">The cancellation token</param>
    /// <returns>Exit code</returns>
    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var postgresConnectionString = settings.PostgresConnectionString;

            // Use configuration value if postgres connection string not provided
            if (postgresConnectionString == null)
            {
                postgresConnectionString = _configuration.GetConnectionString("nocturne-postgres");
                _logger.LogDebug(
                    "Looking for connection string 'nocturne-postgres' in configuration"
                );
                _logger.LogDebug(
                    "Found connection string: {ConnectionString}",
                    postgresConnectionString ?? "NULL"
                );

                if (postgresConnectionString == null)
                {
                    // Debug: List all available connection strings
                    var connectionStringsSection = _configuration.GetSection("ConnectionStrings");
                    _logger.LogDebug("Available connection strings:");
                    foreach (var child in connectionStringsSection.GetChildren())
                    {
                        _logger.LogDebug("  {Key}: {Value}", child.Key, child.Value);
                    }

                    throw new InvalidOperationException(
                        "PostgreSQL connection string not found in configuration or command line arguments"
                    );
                }
            }

            var mongoConnectionString = settings.MongoConnectionString;

            // Use configuration value if MongoDB connection string not provided
            if (mongoConnectionString == null)
            {
                // Try various MongoDB connection string configurations in order of preference
                mongoConnectionString =
                    _configuration.GetConnectionString("mongodb")
                    ?? _configuration.GetConnectionString("mongo")
                    ?? _configuration.GetConnectionString("MongoDB")
                    ?? _configuration[ServiceNames.ConfigKeys.CustomConnStrMongo]
                    ?? _configuration[ServiceNames.ConfigKeys.MongoConnectionString];

                _logger.LogDebug("Looking for MongoDB connection string in configuration");
                _logger.LogDebug(
                    "Found MongoDB connection string: {ConnectionString}",
                    mongoConnectionString ?? "NULL"
                );

                if (mongoConnectionString == null)
                {
                    throw new InvalidOperationException(
                        "MongoDB connection string not found in configuration or command line arguments. "
                            + "Please provide --mongo-connection parameter or add MongoDB configuration to appsettings.json"
                    );
                }
                else
                {
                    _logger.LogInformation("Using MongoDB connection string from configuration");
                }
            }

            // Extract database name from connection string if not provided
            var mongoDatabaseName = settings.MongoDatabaseName;
            if (string.IsNullOrWhiteSpace(mongoDatabaseName))
            {
                try
                {
                    var mongoUrl = new MongoDB.Driver.MongoUrl(mongoConnectionString);
                    mongoDatabaseName = mongoUrl.DatabaseName;
                    _logger.LogInformation(
                        "Extracted MongoDB database name from connection string: {DatabaseName}",
                        mongoDatabaseName
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to extract database name from MongoDB connection string"
                    );
                    throw new InvalidOperationException(
                        "MongoDB database name could not be extracted from connection string and was not provided via --mongo-database parameter"
                    );
                }
            }

            if (string.IsNullOrWhiteSpace(mongoDatabaseName))
            {
                throw new InvalidOperationException(
                    "MongoDB database name is required. Please provide --mongo-database parameter or ensure it's included in the connection string"
                );
            }

            _logger.LogInformation(
                "Starting PostgreSQL migration with MongoDB source: {MongoDatabase}",
                mongoDatabaseName
            );

            _logger.LogInformation(
                "Using MongoDB connection string: {MongoConnectionString}",
                mongoConnectionString
            );
            _logger.LogInformation(
                "Using PostgreSQL connection string: {PostgresConnectionString}",
                postgresConnectionString
            );

            // Test database connections before migration (unless skipped)
            if (!settings.SkipConnectionTest)
            {
                _logger.LogInformation("Testing database connections before starting migration");
                using var connectionTestScope = _serviceProvider.CreateScope();
                var connectionService =
                    connectionTestScope.ServiceProvider.GetRequiredService<IDatabaseConnectionService>();

                var connectionReport = await connectionService.TestAllConnectionsAsync(
                    mongoConnectionString,
                    mongoDatabaseName,
                    postgresConnectionString
                );

                if (!connectionReport.AllConnectionsSuccessful)
                {
                    _logger.LogError("Database connection test failed. Migration cannot proceed.");

                    if (!connectionReport.MongoResult.IsSuccessful)
                    {
                        _logger.LogError(
                            "MongoDB connection failed: {Error}",
                            connectionReport.MongoResult.ErrorMessage
                        );
                    }

                    if (!connectionReport.PostgreSqlResult.IsSuccessful)
                    {
                        _logger.LogError(
                            "PostgreSQL connection failed: {Error}",
                            connectionReport.PostgreSqlResult.ErrorMessage
                        );
                    }

                    return 1;
                }

                _logger.LogInformation(
                    "Database connection test successful. Proceeding with migration."
                );
                _logger.LogDebug("MongoDB: {MongoInfo}", connectionReport.MongoResult.ServerInfo);
                _logger.LogDebug(
                    "PostgreSQL: {PostgresInfo}",
                    connectionReport.PostgreSqlResult.ServerInfo
                );
            }
            else
            {
                _logger.LogWarning("Skipping database connection test as requested");
            }

            // Run interactive mode if no start/end dates provided
            DateTime? startDate = null;
            DateTime? endDate = null;

            if (string.IsNullOrWhiteSpace(settings.StartDate) && string.IsNullOrWhiteSpace(settings.EndDate))
            {
                (startDate, endDate) = await RunInteractiveModeAsync(
                    mongoConnectionString,
                    mongoDatabaseName
                );
            }
            else
            {
                // Parse date filters from command line
                if (
                    !string.IsNullOrWhiteSpace(settings.StartDate)
                    && DateTime.TryParse(settings.StartDate, out var start)
                )
                {
                    startDate = start;
                }

                if (
                    !string.IsNullOrWhiteSpace(settings.EndDate)
                    && DateTime.TryParse(settings.EndDate, out var end)
                )
                {
                    endDate = end;
                }
            }

            // Create configuration
            var config = new MigrationEngineConfiguration
            {
                MongoConnectionString = mongoConnectionString,
                MongoDatabaseName = mongoDatabaseName,
                PostgreSqlConnectionString = postgresConnectionString,
                BatchSize = settings.BatchSize,
                MaxMemoryUsageMB = settings.MaxMemoryMb,
                MaxDegreeOfParallelism =
                    settings.Parallelism > 0 ? settings.Parallelism : Environment.ProcessorCount,
                DropExistingTables = settings.DropTables,
                SkipDuplicates = settings.SkipDuplicates,
                ValidationOptions = new Nocturne.Tools.Abstractions.Services.ValidationOptions(
                    EnableSchemaValidation: !settings.SkipValidation,
                    EnableDataValidation: !settings.SkipValidation
                ),
                IndexOptimizationOptions = new IndexOptimizationOptions
                {
                    SkipIndexCreation = settings.SkipIndexCreation,
                    DeferIndexCreation = settings.DeferIndexCreation,
                    DropExistingIndexes = settings.DropExistingIndexes,
                    CreateConcurrently = !settings.DisableConcurrentIndexes,
                    MaxConcurrentIndexCreation = settings.MaxConcurrentIndexes,
                },
                TransformationOptions = new TransformationOptions
                {
                    PreserveNullProperties = settings.PreserveNullProperties,
                },
            };

            // Parse collections
            if (!string.IsNullOrWhiteSpace(settings.Collections))
            {
                config.CollectionsToMigrate = settings
                    .Collections.Split(',', StringSplitOptions.RemoveEmptyEntries)
                    .Select(c => c.Trim())
                    .ToList();
            }

            // Apply date filters from interactive mode or command line
            if (startDate.HasValue)
            {
                config.StartDate = startDate.Value;
            }

            if (endDate.HasValue)
            {
                config.EndDate = endDate.Value;
            }

            // Set up PostgreSQL DbContext
            using var scope = _serviceProvider.CreateScope();
            var services = scope.ServiceProvider;

            // Add DbContexts with the provided connection string
            var serviceCollection = new ServiceCollection();

            // Migration tracking context - manages only migration-specific tables
            serviceCollection.AddDbContext<MigrationDbContext>(options =>
                options.UseNpgsql(postgresConnectionString)
            );

            // Migration data context - provides data access to main application tables
            serviceCollection.AddDbContext<MigrationDataContext>(options =>
                options.UseNpgsql(postgresConnectionString)
            );

            serviceCollection.AddLogging();
            serviceCollection.AddSingleton<IDataTransformationService, DataTransformationService>();
            serviceCollection.AddSingleton<
                Nocturne.Tools.Migration.Services.IDatabaseSchemaIntrospectionService,
                Nocturne.Tools.Migration.Services.DatabaseSchemaIntrospectionService
            >();
            serviceCollection.AddSingleton<
                Nocturne.Tools.Abstractions.Services.IValidationService,
                Nocturne.Tools.Migration.Services.SchemaValidationService
            >();
            serviceCollection.AddSingleton<IMigrationEngine, MigrationEngine>();
            serviceCollection.AddSingleton<IIndexOptimizationService, IndexOptimizationService>();

            using var migrationServiceProvider = serviceCollection.BuildServiceProvider();

            // Create migration engine
            var migrationEngine = migrationServiceProvider.GetRequiredService<IMigrationEngine>();

            // Validate configuration
            _logger.LogInformation("Validating migration configuration...");
            var validation = await migrationEngine.ValidateAsync(config);

            if (!validation.IsValid)
            {
                _logger.LogError("Migration validation failed:");
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

            if (validation.Conflicts.Any())
            {
                _logger.LogWarning("Migration conflicts detected:");
                foreach (var conflict in validation.Conflicts)
                {
                    _logger.LogWarning(
                        "  - {ConflictType}: {Description}",
                        conflict.ConflictType,
                        conflict.Description
                    );
                }
            }

            // Run migration
            _logger.LogInformation("Starting migration...");
            var result = await migrationEngine.MigrateAsync(config);

            if (result.IsSuccess)
            {
                _logger.LogInformation("Migration completed successfully!");
                _logger.LogInformation("Migration ID: {MigrationId}", result.MigrationId);
                _logger.LogInformation(
                    "Total documents processed: {TotalProcessed}",
                    result.Statistics.TotalDocumentsProcessed
                );
                _logger.LogInformation(
                    "Total documents failed: {TotalFailed}",
                    result.Statistics.TotalDocumentsFailed
                );
                _logger.LogInformation("Duration: {Duration}", result.Statistics.Duration);

                foreach (var collectionStat in result.Statistics.CollectionStats)
                {
                    _logger.LogInformation(
                        "Collection {Collection}: {Migrated} migrated, {Failed} failed in {Duration}",
                        collectionStat.Key,
                        collectionStat.Value.DocumentsMigrated,
                        collectionStat.Value.DocumentsFailed,
                        collectionStat.Value.Duration
                    );
                }

                return 0;
            }
            else
            {
                _logger.LogError("Migration failed: {Error}", result.ErrorMessage);
                return 1;
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Migration command failed: {Error}", ex.Message);
            return 1;
        }
    }

    /// <summary>
    /// Run interactive mode to display collection statistics and prompt for date range
    /// </summary>
    private async Task<(DateTime? startDate, DateTime? endDate)> RunInteractiveModeAsync(
        string mongoConnectionString,
        string mongoDatabaseName
    )
    {
        try
        {
            AnsiConsole.MarkupLine("[bold cyan]Analyzing MongoDB collections...[/]");
            AnsiConsole.WriteLine();

            // Create analysis service with proper logger
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var analysisLogger = loggerFactory.CreateLogger<CollectionAnalysisService>();
            var analysisService = new CollectionAnalysisService(
                analysisLogger,
                mongoConnectionString,
                mongoDatabaseName
            );

            // Analyze all collections
            var statistics = await analysisService.AnalyzeAllCollectionsAsync();

            // Display collection statistics table
            var table = new Table();
            table.Border(TableBorder.Rounded);
            table.AddColumn("[bold]Collection[/]");
            table.AddColumn("[bold]Documents[/]");
            table.AddColumn("[bold]Earliest[/]");
            table.AddColumn("[bold]Latest[/]");

            foreach (var stat in statistics)
            {
                table.AddRow(
                    stat.CollectionName,
                    stat.DocumentCount.ToString("N0"),
                    stat.EarliestDate?.ToString("yyyy-MM-dd") ?? "N/A",
                    stat.LatestDate?.ToString("yyyy-MM-dd") ?? "N/A"
                );
            }

            AnsiConsole.Write(table);
            AnsiConsole.WriteLine();

            // Prompt: Import all data?
            var importAll = AnsiConsole.Confirm("Import all historical data?", defaultValue: false);

            if (importAll)
            {
                return (null, null);
            }

            // Prompt for start date
            var startDateStr = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter start date ([green]YYYY-MM-DD[/]):")
                    .PromptStyle("green")
                    .ValidationErrorMessage("[red]Invalid date format. Please use YYYY-MM-DD[/]")
                    .Validate(input =>
                    {
                        if (
                            DateTime.TryParseExact(
                                input,
                                "yyyy-MM-dd",
                                CultureInfo.InvariantCulture,
                                DateTimeStyles.None,
                                out var date
                            )
                        )
                        {
                            if (date > DateTime.UtcNow)
                            {
                                return ValidationResult.Error(
                                    "[red]Start date cannot be in the future[/]"
                                );
                            }
                            return ValidationResult.Success();
                        }
                        return ValidationResult.Error(
                            "[red]Invalid date format. Please use YYYY-MM-DD[/]"
                        );
                    })
            );

            var startDate = DateTime.ParseExact(
                startDateStr,
                "yyyy-MM-dd",
                CultureInfo.InvariantCulture
            );

            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine(
                "[bold cyan]Calculating estimated document counts for selected range...[/]"
            );
            AnsiConsole.WriteLine();

            // Get estimated counts for selected range
            var estimatedStats = await analysisService.AnalyzeAllCollectionsAsync(
                startDate,
                null
            );

            var totalEstimated = estimatedStats.Sum(s => s.DocumentCount);

            AnsiConsole.MarkupLine(
                $"[bold]Estimated documents to migrate with selected range (from {startDate:yyyy-MM-dd}):[/]"
            );
            foreach (var stat in estimatedStats.Where(s => s.DocumentCount > 0))
            {
                AnsiConsole.MarkupLine($"  - {stat.CollectionName}: [green]~{stat.DocumentCount:N0}[/]");
            }
            AnsiConsole.WriteLine();
            AnsiConsole.MarkupLine($"[bold]Total: [green]~{totalEstimated:N0}[/] documents[/]");
            AnsiConsole.WriteLine();

            // Final confirmation
            var proceed = AnsiConsole.Confirm("Proceed with migration?", defaultValue: true);

            if (!proceed)
            {
                AnsiConsole.MarkupLine("[yellow]Migration cancelled by user[/]");
                Environment.Exit(0);
            }

            return (startDate, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to run interactive mode: {ErrorMessage}",
                ex.Message
            );
            AnsiConsole.MarkupLine(
                "[yellow]Unable to analyze collections. Proceeding with command-line parameters only.[/]"
            );
            return (null, null);
        }
    }
}
