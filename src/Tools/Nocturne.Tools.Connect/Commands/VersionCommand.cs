using System.Reflection;
using Microsoft.Extensions.Logging;
using Nocturne.Tools.Abstractions.Commands;
using Nocturne.Tools.Abstractions.Services;
using Nocturne.Tools.Core.Commands;
using Spectre.Console.Cli;

namespace Nocturne.Tools.Connect.Commands;

/// <summary>
/// Command to display version information.
/// </summary>
public class VersionCommand : AsyncCommand
{
    private readonly ILogger<VersionCommand> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="VersionCommand"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    public VersionCommand(ILogger<VersionCommand> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public override Task<int> ExecuteAsync(CommandContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var assembly = Assembly.GetExecutingAssembly();
            var version = assembly.GetName().Version;
            var buildDate = new DateTime(2000, 1, 1)
                .AddDays(version?.Build ?? 0)
                .AddSeconds((version?.Revision ?? 0) * 2);

            Console.WriteLine("🌙 Nocturne Connect");
            Console.WriteLine($"   Version: {version?.ToString() ?? "Unknown"}");
            Console.WriteLine($"   Built: {buildDate:yyyy-MM-dd}");
            Console.WriteLine($"   Runtime: {Environment.Version}");
            Console.WriteLine($"   Platform: {Environment.OSVersion}");
            Console.WriteLine();
            Console.WriteLine("A modern C# rewrite of nightscout-connect");
            Console.WriteLine("Repository: https://github.com/your-repo/nocturne");

            return Task.FromResult(0);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to display version information");
            Console.WriteLine($"❌ Error: {ex.Message}");
            return Task.FromResult(1);
        }
    }
}
