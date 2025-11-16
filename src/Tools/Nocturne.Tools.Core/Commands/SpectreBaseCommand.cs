using Microsoft.Extensions.Logging;
using Nocturne.Tools.Abstractions.Commands;
using Nocturne.Tools.Abstractions.Services;
using Spectre.Console;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Core.Commands;

/// <summary>
/// Base class for all Spectre.Console commands.
/// </summary>
/// <typeparam name="TSettings">The settings type for the command.</typeparam>
public abstract class SpectreBaseCommand<TSettings> : AsyncCommand<TSettings>
    where TSettings : CommandSettings
{
    private readonly ILogger _logger;
    private readonly IProgressReporter _progressReporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="SpectreBaseCommand{TSettings}"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="progressReporter">The progress reporter.</param>
    protected SpectreBaseCommand(ILogger logger, IProgressReporter progressReporter)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progressReporter =
            progressReporter ?? throw new ArgumentNullException(nameof(progressReporter));
    }

    /// <summary>
    /// Gets the logger.
    /// </summary>
    protected ILogger Logger => _logger;

    /// <summary>
    /// Gets the progress reporter.
    /// </summary>
    protected IProgressReporter ProgressReporter => _progressReporter;

    /// <summary>
    /// Gets the name of the command.
    /// </summary>
    public abstract string Name { get; }

    /// <summary>
    /// Gets the description of the command.
    /// </summary>
    public abstract string Description { get; }

    /// <summary>
    /// Executes the command.
    /// </summary>
    public override async Task<int> ExecuteAsync(CommandContext context, TSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Starting command: {CommandName}", Name);

            // Display command start with Spectre.Console styling
            AnsiConsole.MarkupLine($"[bold green]Starting {Name}[/]");

            var result = await ExecuteCommandAsync(context, settings);

            if (result.IsSuccess)
            {
                Logger.LogInformation("Command completed successfully: {Message}", result.Message);
                ProgressReporter.ReportCompletion(Name, true, result.Message);
                AnsiConsole.MarkupLine($"[bold green]✓[/] {result.Message}");
            }
            else
            {
                Logger.LogError("Command failed: {Message}", result.Message);
                ProgressReporter.ReportCompletion(Name, false, result.Message);
                AnsiConsole.MarkupLine($"[bold red]✗[/] {result.Message}");
            }

            return result.ExitCode;
        }
        catch (OperationCanceledException)
        {
            var message = "Operation was cancelled";
            Logger.LogInformation(message);
            ProgressReporter.ReportCompletion(Name, false, message);
            AnsiConsole.MarkupLine($"[bold yellow]⚠[/] {message}");
            return 130; // Standard exit code for cancelled operations
        }
        catch (Exception ex)
        {
            Logger.LogError(
                ex,
                "Unexpected error occurred while executing command: {CommandName}",
                Name
            );
            ProgressReporter.ReportError(Name, ex);
            AnsiConsole.MarkupLine($"[bold red]✗[/] Unexpected error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Executes the command logic.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <param name="settings">The command settings.</param>
    /// <returns>The command result.</returns>
    protected abstract Task<CommandResult> ExecuteCommandAsync(
        CommandContext context,
        TSettings settings
    );
}

/// <summary>
/// Base class for simple commands without settings.
/// </summary>
public abstract class SpectreBaseCommand : SpectreBaseCommand<EmptyCommandSettings>
{
    /// <summary>
    /// Initializes a new instance of the <see cref="SpectreBaseCommand"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="progressReporter">The progress reporter.</param>
    protected SpectreBaseCommand(ILogger logger, IProgressReporter progressReporter)
        : base(logger, progressReporter) { }

    /// <inheritdoc/>
    protected override Task<CommandResult> ExecuteCommandAsync(
        CommandContext context,
        EmptyCommandSettings settings
    )
    {
        return ExecuteCommandAsync(context);
    }

    /// <summary>
    /// Executes the command logic.
    /// </summary>
    /// <param name="context">The command context.</param>
    /// <returns>The command result.</returns>
    protected abstract Task<CommandResult> ExecuteCommandAsync(CommandContext context);
}

/// <summary>
/// Empty command settings for commands that don't need any settings.
/// </summary>
public sealed class EmptyCommandSettings : CommandSettings { }

