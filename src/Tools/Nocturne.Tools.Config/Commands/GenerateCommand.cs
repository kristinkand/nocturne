using Microsoft.Extensions.Logging;
using Nocturne.Tools.Abstractions.Commands;
using Nocturne.Tools.Abstractions.Services;
using Nocturne.Tools.Config.Configuration;
using Nocturne.Tools.Config.Services;
using Nocturne.Tools.Core.Commands;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Config.Commands;

/// <summary>
/// Command to generate configuration files for Nocturne.
/// </summary>
public class GenerateCommand : SpectreBaseCommand<GenerateSettings>
{
    private readonly ILogger<GenerateCommand> _logger;
    private readonly ConfigurationGeneratorService _generatorService;
    private readonly IProgressReporter _progressReporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="GenerateCommand"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="generatorService">The configuration generator service.</param>
    /// <param name="progressReporter">The progress reporter.</param>
    public GenerateCommand(
        ILogger<GenerateCommand> logger,
        ConfigurationGeneratorService generatorService,
        IProgressReporter progressReporter
    )
        : base(logger, progressReporter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _generatorService =
            generatorService ?? throw new ArgumentNullException(nameof(generatorService));
        _progressReporter =
            progressReporter ?? throw new ArgumentNullException(nameof(progressReporter));
    }

    /// <inheritdoc/>
    public override string Name => "generate";

    /// <inheritdoc/>
    public override string Description => "Generate configuration files with example values";

    /// <inheritdoc/>
    protected override async Task<CommandResult> ExecuteCommandAsync(
        CommandContext context,
        GenerateSettings settings
    )
    {
        try
        {
            _logger.LogInformation("Starting configuration generation...");

            var config = new ConfigConfiguration
            {
                OutputPath = settings.OutputPath,
                Format = settings.Format,
                OverwriteExisting = settings.Overwrite,
                IncludeComments = settings.Comments,
                Environment = settings.Environment,
            };

            // Validate configuration
            var validationResult = config.ValidateConfiguration();
            if (validationResult != System.ComponentModel.DataAnnotations.ValidationResult.Success)
            {
                _logger.LogError(
                    "Configuration validation failed: {ErrorMessage}",
                    validationResult.ErrorMessage
                );
                return CommandResult.Failure(
                    validationResult.ErrorMessage ?? "Configuration validation failed"
                );
            }

            await _generatorService.GenerateConfigurationAsync(config, CancellationToken.None);

            _logger.LogInformation("Configuration generation completed successfully");
            return CommandResult.Success("Configuration file generated successfully");
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Configuration generation failed: {Message}", ex.Message);
            return CommandResult.Failure(ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Unexpected error during configuration generation: {Message}",
                ex.Message
            );
            return CommandResult.Failure($"Unexpected error: {ex.Message}");
        }
    }
}

