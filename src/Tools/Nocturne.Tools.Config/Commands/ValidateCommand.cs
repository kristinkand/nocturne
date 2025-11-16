using System.Text.Json;
using Microsoft.Extensions.Logging;
using Nocturne.Tools.Abstractions.Commands;
using Nocturne.Tools.Abstractions.Services;
using Nocturne.Tools.Core.Commands;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Config.Commands;

/// <summary>
/// Command to validate configuration files for Nocturne.
/// </summary>
public class ValidateCommand : SpectreBaseCommand<ValidateSettings>
{
    private readonly ILogger<ValidateCommand> _logger;
    private readonly IProgressReporter _progressReporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="ValidateCommand"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="progressReporter">The progress reporter.</param>
    public ValidateCommand(ILogger<ValidateCommand> logger, IProgressReporter progressReporter)
        : base(logger, progressReporter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progressReporter =
            progressReporter ?? throw new ArgumentNullException(nameof(progressReporter));
    }

    /// <inheritdoc/>
    public override string Name => "validate";

    /// <inheritdoc/>
    public override string Description =>
        "Validate configuration files for correctness and completeness";

    /// <inheritdoc/>
    protected override async Task<CommandResult> ExecuteCommandAsync(
        CommandContext context,
        ValidateSettings settings
    )
    {
        try
        {
            _logger.LogInformation(
                "Starting configuration validation for: {ConfigPath}",
                settings.ConfigPath
            );

            // Check if file exists
            if (!File.Exists(settings.ConfigPath))
            {
                var errorMessage = $"Configuration file not found: {settings.ConfigPath}";
                _logger.LogError(errorMessage);
                AnsiConsole.MarkupLine($"[red]✗[/] {errorMessage}");
                return CommandResult.Failure(errorMessage);
            }

            AnsiConsole.MarkupLine(
                $"[blue]📋[/] Validating configuration file: [yellow]{settings.ConfigPath}[/]"
            );

            var configContent = await File.ReadAllTextAsync(
                settings.ConfigPath,
                CancellationToken.None
            );

            if (string.IsNullOrWhiteSpace(configContent))
            {
                var errorMessage = "Configuration file is empty";
                _logger.LogError(errorMessage);
                AnsiConsole.MarkupLine($"[red]✗[/] {errorMessage}");
                return CommandResult.Failure(errorMessage);
            }

            // Simple JSON validation
            try
            {
                JsonDocument.Parse(configContent);
                AnsiConsole.MarkupLine($"[green]✓[/] JSON structure is valid");
            }
            catch (JsonException ex)
            {
                var errorMessage = $"Invalid JSON format: {ex.Message}";
                _logger.LogError(errorMessage);
                AnsiConsole.MarkupLine($"[red]✗[/] {errorMessage}");
                return CommandResult.Failure(errorMessage);
            }

            if (settings.Verbose)
            {
                AnsiConsole.MarkupLine($"[cyan]ℹ[/] File size: {configContent.Length} characters");
                AnsiConsole.MarkupLine(
                    $"[cyan]ℹ[/] Last modified: {File.GetLastWriteTime(settings.ConfigPath)}"
                );
            }

            _logger.LogInformation("Configuration validation completed successfully");
            return CommandResult.Success("Configuration validation passed");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during configuration validation: {Message}", ex.Message);
            AnsiConsole.MarkupLine($"[red]✗[/] Unexpected error: {ex.Message}");
            return CommandResult.Failure($"Unexpected error: {ex.Message}");
        }
    }
}

