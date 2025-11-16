using System.Reflection;
using Microsoft.Extensions.Logging;
using Nocturne.Tools.Abstractions.Commands;
using Nocturne.Tools.Abstractions.Services;
using Nocturne.Tools.Config.Configuration;
using Nocturne.Tools.Core.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Config.Commands;

/// <summary>
/// Command to display version information for the Config tool.
/// </summary>
public class VersionCommand : SpectreBaseCommand<VersionSettings>
{
    private readonly ILogger<VersionCommand> _logger;
    private readonly IProgressReporter _progressReporter;
    private readonly ConfigConfiguration _configuration;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionCommand"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="progressReporter">The progress reporter.</param>
    /// <param name="configuration">The configuration.</param>
    public VersionCommand(
        ILogger<VersionCommand> logger,
        IProgressReporter progressReporter,
        ConfigConfiguration configuration
    )
        : base(logger, progressReporter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progressReporter =
            progressReporter ?? throw new ArgumentNullException(nameof(progressReporter));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    /// <inheritdoc/>
    public override string Name => "version";

    /// <inheritdoc/>
    public override string Description => "Display version information for the Config tool";

    /// <inheritdoc/>
    protected override Task<CommandResult> ExecuteCommandAsync(
        CommandContext context,
        VersionSettings settings
    )
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version?.ToString() ?? "Unknown";
            var buildDate = GetBuildDate(assembly);

            _logger.LogInformation("{ToolName} v{Version}", _configuration.ToolName, version);

            // Use Spectre.Console for better formatting
            var table = new Table()
                .Border(TableBorder.Rounded)
                .BorderColor(Color.Blue)
                .AddColumn("[bold]Property[/]")
                .AddColumn("[bold]Value[/]");

            table.AddRow("Tool", $"[green]{_configuration.ToolName}[/]");
            table.AddRow("Version", $"[yellow]{version}[/]");

            if (settings.Detailed)
            {
                table.AddRow("Build Date", $"{buildDate:yyyy-MM-dd HH:mm:ss} UTC");
                table.AddRow("Assembly", assembly.GetName().Name ?? "Unknown");
                table.AddRow("Location", assembly.Location);
                table.AddRow("Runtime", Environment.Version.ToString());
                table.AddRow("Platform", Environment.OSVersion.ToString());
                table.AddRow(
                    "Architecture",
                    $"{Environment.ProcessorCount} cores, {(Environment.Is64BitProcess ? "64-bit" : "32-bit")}"
                );

                // Show supported formats
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Supported Configuration Formats:[/]");
                var formats = Enum.GetValues<ConfigFormat>();
                foreach (var format in formats)
                {
                    AnsiConsole.MarkupLine($"  [cyan]•[/] {format}");
                }

                // Show dependencies
                AnsiConsole.WriteLine();
                AnsiConsole.MarkupLine("[bold]Key Dependencies:[/]");
                ShowDependencyVersion("Spectre.Console.Cli");
                ShowDependencyVersion("Microsoft.Extensions.Configuration");
                ShowDependencyVersion("Microsoft.Extensions.Logging");
                ShowDependencyVersion("System.Text.Json");
            }

            AnsiConsole.Write(table);

            return Task.FromResult(CommandResult.Success($"Version: {version}"));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving version information: {Message}", ex.Message);
            return Task.FromResult(
                CommandResult.Failure($"Error retrieving version information: {ex.Message}")
            );
        }
    }

    private static DateTime GetBuildDate(Assembly assembly)
    {
        try
        {
            var location = assembly.Location;
            if (!string.IsNullOrEmpty(location) && File.Exists(location))
            {
                return File.GetCreationTimeUtc(location);
            }
        }
        catch
        {
            // Fallback to embedded build timestamp if available
        }

        // Fallback to a reasonable default
        return DateTime.UtcNow;
    }

    private static void ShowDependencyVersion(string packageName)
    {
        try
        {
            var assemblies = AppDomain.CurrentDomain.GetAssemblies();
            var assembly = assemblies.FirstOrDefault(a =>
                a.GetName().Name?.StartsWith(packageName, StringComparison.OrdinalIgnoreCase)
                == true
            );

            if (assembly != null)
            {
                var version = assembly.GetName().Version?.ToString() ?? "Unknown";
                AnsiConsole.MarkupLine($"  [cyan]•[/] {packageName}: [yellow]{version}[/]");
            }
        }
        catch
        {
            AnsiConsole.MarkupLine($"  [cyan]•[/] {packageName}: [red]Version unavailable[/]");
        }
    }
}

