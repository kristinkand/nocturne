using System.ComponentModel;
using Microsoft.Extensions.Logging;
using Nocturne.Tools.Abstractions.Commands;
using Nocturne.Tools.Abstractions.Configuration;
using Nocturne.Tools.Abstractions.Services;
using Nocturne.Tools.Connect.Configuration;
using Nocturne.Tools.Connect.Services;
using Nocturne.Tools.Core.Commands;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Connect.Commands;

/// <summary>
/// Command settings for the init command.
/// </summary>
public sealed class InitSettings : CommandSettings
{
    [CommandOption("-i|--interactive")]
    [Description("Run in interactive configuration mode")]
    public bool Interactive { get; init; }

    [CommandOption("-f|--file <FILE>")]
    [Description("Environment file to use (.env file path)")]
    public string? File { get; init; }
}

/// <summary>
/// Command to initialize Nocturne Connect configuration.
/// </summary>
public class InitCommand : AsyncCommand<InitSettings>
{
    private readonly ILogger<InitCommand> _logger;
    private readonly IConfigurationManager _configurationManager;

    /// <summary>
    /// Initializes a new instance of the <see cref="InitCommand"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configurationManager">The configuration manager.</param>
    public InitCommand(ILogger<InitCommand> logger, IConfigurationManager configurationManager)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configurationManager =
            configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
    }

    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, InitSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            Console.WriteLine("🔧 Initializing Nocturne Connect configuration...");

            // For now, we'll create a basic configuration template
            // In a full implementation, this would include the interactive setup logic
            var outputPath = "appsettings.Connect.json";
            await _configurationManager.CreateConfigurationTemplateAsync(
                outputPath,
                "Nocturne Connect"
            );

            Console.WriteLine($"✅ Configuration template created at {outputPath}");
            Console.WriteLine(
                "   Edit this file with your specific settings and run 'nocturne-connect config --validate' to verify."
            );

            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to initialize configuration");
            Console.WriteLine($"❌ Error: {ex.Message}");
            return 1;
        }
    }
}
