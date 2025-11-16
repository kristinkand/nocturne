using Microsoft.Extensions.Logging;
using Nocturne.Tools.Abstractions.Commands;
using Nocturne.Tools.Abstractions.Services;

namespace Nocturne.Tools.Core.Commands;

/// <summary>
/// Base class for all CLI commands.
/// </summary>
public abstract class BaseCommand : IToolCommand
{
    private readonly ILogger _logger;
    private readonly IProgressReporter _progressReporter;

    /// <summary>
    /// Initializes a new instance of the <see cref="BaseCommand"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="progressReporter">The progress reporter.</param>
    protected BaseCommand(ILogger logger, IProgressReporter progressReporter)
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

    /// <inheritdoc/>
    public abstract string Name { get; }

    /// <inheritdoc/>
    public abstract string Description { get; }

    /// <inheritdoc/>
    public async Task<int> ExecuteAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            Logger.LogInformation("Starting command: {CommandName}", Name);

            var result = await ExecuteCommandAsync(cancellationToken);

            if (result.IsSuccess)
            {
                Logger.LogInformation("Command completed successfully: {Message}", result.Message);
                ProgressReporter.ReportCompletion(Name, true, result.Message);
            }
            else
            {
                Logger.LogError("Command failed: {Message}", result.Message);
                ProgressReporter.ReportCompletion(Name, false, result.Message);
            }

            return result.ExitCode;
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("Command was cancelled");
            ProgressReporter.ReportCompletion(Name, false, "Operation was cancelled");
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
            return 1;
        }
    }

    /// <summary>
    /// Executes the command logic.
    /// </summary>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>The command result.</returns>
    protected abstract Task<CommandResult> ExecuteCommandAsync(CancellationToken cancellationToken);
}

