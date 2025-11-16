using System.ComponentModel;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Constants;
using Nocturne.Tools.Migration.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Migration.Commands;

/// <summary>
/// Command to test database connections before migration
/// </summary>
public class TestConnectionsCommand : AsyncCommand<TestConnectionsCommand.Settings>
{
    private readonly ILogger<TestConnectionsCommand> _logger;
    private readonly IServiceProvider _serviceProvider;
    private readonly IConfiguration _configuration;

    public TestConnectionsCommand(
        ILogger<TestConnectionsCommand> logger,
        IServiceProvider serviceProvider,
        IConfiguration configuration
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _serviceProvider =
            serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public class Settings : CommandSettings
    {
        [Description("MongoDB connection string (overrides configuration)")]
        [CommandOption("--mongo-connection")]
        public string? MongoConnectionString { get; init; }

        [Description("MongoDB database name (overrides configuration)")]
        [CommandOption("--mongo-database")]
        public string? MongoDatabaseName { get; init; }

        [Description("PostgreSQL connection string (overrides configuration)")]
        [CommandOption("--postgres-connection")]
        public string? PostgreSqlConnectionString { get; init; }

        [Description("Show detailed connection information")]
        [CommandOption("--verbose")]
        [DefaultValue(false)]
        public bool Verbose { get; init; }

        [Description("Timeout for connection tests in seconds")]
        [CommandOption("--timeout")]
        [DefaultValue(30)]
        public int TimeoutSeconds { get; init; }
    }

    public override async Task<int> ExecuteAsync(CommandContext context, Settings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            AnsiConsole.Write(
                new FigletText("Database Connection Test").LeftJustified().Color(Color.Cyan1)
            );

            AnsiConsole.WriteLine();

            // Get connection strings from configuration or command line
            var mongoConnectionString =
                settings.MongoConnectionString
                ?? _configuration.GetConnectionString("mongodb")
                ?? throw new InvalidOperationException(
                    "MongoDB connection string not found in configuration or command line"
                );

            var mongoDatabaseName =
                settings.MongoDatabaseName
                ?? GetDatabaseNameFromConnectionString(mongoConnectionString)
                ?? "nightscout02"; // Default fallback

            var postgreConnectionString =
                settings.PostgreSqlConnectionString
                ?? _configuration.GetConnectionString("nocturne-postgres")
                ?? _configuration[ServiceNames.ConfigKeys.PostgreSqlConnectionString]
                ?? throw new InvalidOperationException(
                    "PostgreSQL connection string not found in configuration or command line"
                );

            // Display configuration being tested
            var configTable = new Table().AddColumn("Database").AddColumn("Configuration");

            configTable.AddRow(
                "MongoDB",
                $"Connection: [yellow]{MaskConnectionString(mongoConnectionString)}[/]"
            );
            configTable.AddRow("", $"Database: [yellow]{mongoDatabaseName}[/]");
            configTable.AddRow(
                "PostgreSQL",
                $"Connection: [yellow]{MaskConnectionString(postgreConnectionString)}[/]"
            );

            AnsiConsole.Write(
                new Panel(configTable).Header("Connection Configuration").BorderColor(Color.Grey)
            );

            AnsiConsole.WriteLine();

            // Create connection service
            using var scope = _serviceProvider.CreateScope();
            var connectionService =
                scope.ServiceProvider.GetRequiredService<IDatabaseConnectionService>();

            // Test connections with timeout
            using var cts = new CancellationTokenSource(
                TimeSpan.FromSeconds(settings.TimeoutSeconds)
            );

            DatabaseConnectionReport report;

            await AnsiConsole
                .Status()
                .Spinner(Spinner.Known.Dots2)
                .StartAsync(
                    "Testing database connections...",
                    async ctx =>
                    {
                        ctx.Status("Testing MongoDB connection...");
                        ctx.Refresh();

                        report = await connectionService.TestAllConnectionsAsync(
                            mongoConnectionString,
                            mongoDatabaseName,
                            postgreConnectionString,
                            cts.Token
                        );
                    }
                );

            // Get the report from the async operation
            report = await connectionService.TestAllConnectionsAsync(
                mongoConnectionString,
                mongoDatabaseName,
                postgreConnectionString,
                cts.Token
            );

            // Display results
            DisplayConnectionResults(report, settings.Verbose);

            // Summary
            AnsiConsole.WriteLine();
            if (report.AllConnectionsSuccessful)
            {
                AnsiConsole.Write(
                    new Panel(
                        $"[green]✓ All database connections successful![/]\n\nTotal test duration: {report.TotalDuration.TotalMilliseconds:F0}ms"
                    )
                        .BorderColor(Color.Green)
                        .Header("Connection Test Summary")
                );
                return 0;
            }
            else
            {
                var failedDatabases = new List<string>();
                if (!report.MongoResult.IsSuccessful)
                    failedDatabases.Add("MongoDB");
                if (!report.PostgreSqlResult.IsSuccessful)
                    failedDatabases.Add("PostgreSQL");

                AnsiConsole.Write(
                    new Panel(
                        $"[red]✗ Connection test failed![/]\n\nFailed databases: {string.Join(", ", failedDatabases)}\nTotal test duration: {report.TotalDuration.TotalMilliseconds:F0}ms"
                    )
                        .BorderColor(Color.Red)
                        .Header("Connection Test Summary")
                );
                return 1;
            }
        }
        catch (OperationCanceledException)
        {
            AnsiConsole.WriteLine();
            AnsiConsole.Write(
                new Panel("[yellow]Connection test was cancelled due to timeout[/]")
                    .BorderColor(Color.Yellow)
                    .Header("Timeout")
            );
            return 2;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Database connection test failed");
            AnsiConsole.WriteLine();
            AnsiConsole.Write(
                new Panel($"[red]Connection test failed with error:[/]\n\n{ex.Message}")
                    .BorderColor(Color.Red)
                    .Header("Error")
            );
            return 3;
        }
    }

    private void DisplayConnectionResults(DatabaseConnectionReport report, bool verbose)
    {
        var resultsTable = new Table()
            .AddColumn("Database")
            .AddColumn("Status")
            .AddColumn("Duration")
            .AddColumn("Details");

        // MongoDB result
        var mongoStatus = report.MongoResult.IsSuccessful
            ? "[green]✓ Connected[/]"
            : "[red]✗ Failed[/]";

        var mongoDetails = report.MongoResult.IsSuccessful
            ? (verbose ? report.MongoResult.Details ?? "Connected successfully" : "OK")
            : report.MongoResult.ErrorMessage ?? "Connection failed";

        resultsTable.AddRow(
            "MongoDB",
            mongoStatus,
            $"{report.MongoResult.Duration.TotalMilliseconds:F0}ms",
            Markup.Escape(mongoDetails)
        );

        if (!report.MongoResult.IsSuccessful && verbose)
        {
            resultsTable.AddRow(
                "",
                "",
                "",
                $"[dim]{Markup.Escape(report.MongoResult.Details ?? "No additional details")}[/]"
            );
        }

        // PostgreSQL result
        var pgStatus = report.PostgreSqlResult.IsSuccessful
            ? "[green]✓ Connected[/]"
            : "[red]✗ Failed[/]";

        var pgDetails = report.PostgreSqlResult.IsSuccessful
            ? (verbose ? report.PostgreSqlResult.Details ?? "Connected successfully" : "OK")
            : report.PostgreSqlResult.ErrorMessage ?? "Connection failed";

        resultsTable.AddRow(
            "PostgreSQL",
            pgStatus,
            $"{report.PostgreSqlResult.Duration.TotalMilliseconds:F0}ms",
            Markup.Escape(pgDetails)
        );

        if (!report.PostgreSqlResult.IsSuccessful && verbose)
        {
            resultsTable.AddRow(
                "",
                "",
                "",
                $"[dim]{Markup.Escape(report.PostgreSqlResult.Details ?? "No additional details")}[/]"
            );
        }

        AnsiConsole.Write(
            new Panel(resultsTable)
                .Header("Connection Test Results")
                .BorderColor(report.AllConnectionsSuccessful ? Color.Green : Color.Red)
        );

        // Show server information if verbose and successful
        if (verbose && (report.MongoResult.IsSuccessful || report.PostgreSqlResult.IsSuccessful))
        {
            AnsiConsole.WriteLine();
            var serverInfoTable = new Table().AddColumn("Database").AddColumn("Server Information");

            if (
                report.MongoResult.IsSuccessful
                && !string.IsNullOrEmpty(report.MongoResult.ServerInfo)
            )
            {
                serverInfoTable.AddRow("MongoDB", report.MongoResult.ServerInfo);
            }

            if (
                report.PostgreSqlResult.IsSuccessful
                && !string.IsNullOrEmpty(report.PostgreSqlResult.ServerInfo)
            )
            {
                serverInfoTable.AddRow("PostgreSQL", report.PostgreSqlResult.ServerInfo);
            }

            if (serverInfoTable.Rows.Count > 0)
            {
                AnsiConsole.Write(
                    new Panel(serverInfoTable).Header("Server Information").BorderColor(Color.Grey)
                );
            }
        }
    }

    private static string MaskConnectionString(string connectionString)
    {
        // Basic masking of sensitive information in connection strings
        var masked = connectionString;

        // MongoDB password masking
        if (masked.Contains(":") && masked.Contains("@"))
        {
            var patterns = new[]
            {
                @"(:)([^:@]+)(@)", // :password@
                @"(password=)([^;]+)", // password=value
                @"(pwd=)([^;]+)", // pwd=value
            };

            foreach (var pattern in patterns)
            {
                masked = System.Text.RegularExpressions.Regex.Replace(
                    masked,
                    pattern,
                    "$1***$3",
                    System.Text.RegularExpressions.RegexOptions.IgnoreCase
                );
            }
        }

        return masked;
    }

    private static string? GetDatabaseNameFromConnectionString(string connectionString)
    {
        try
        {
            var uri = new Uri(connectionString);
            return uri.AbsolutePath.TrimStart('/');
        }
        catch
        {
            return null;
        }
    }
}
