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
/// Command settings for the status command.
/// </summary>
public sealed class StatusSettings : CommandSettings
{
    [CommandOption("-w|--watch")]
    [Description("Monitor status continuously")]
    public bool Watch { get; init; }

    [CommandOption("-f|--file <FILE>")]
    [Description("Environment file to use (.env file path)")]
    public string? File { get; init; }
}

/// <summary>
/// Command to show current sync status and health.
/// </summary>
public class StatusCommand : AsyncCommand<StatusSettings>
{
    private readonly ILogger<StatusCommand> _logger;
    private readonly IConfigurationManager _configurationManager;
    private readonly DaemonStatusService _daemonStatusService;

    /// <summary>
    /// Initializes a new instance of the <see cref="StatusCommand"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="configurationManager">The configuration manager.</param>
    /// <param name="daemonStatusService">The daemon status service.</param>
    public StatusCommand(
        ILogger<StatusCommand> logger,
        IConfigurationManager configurationManager,
        DaemonStatusService daemonStatusService
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configurationManager =
            configurationManager ?? throw new ArgumentNullException(nameof(configurationManager));
        _daemonStatusService =
            daemonStatusService ?? throw new ArgumentNullException(nameof(daemonStatusService));
    }

    /// <inheritdoc/>
    public override async Task<int> ExecuteAsync(CommandContext context, StatusSettings settings, CancellationToken cancellationToken = default)
    {
        try
        {
            var config = _configurationManager.LoadConfiguration<ConnectConfiguration>();

            Console.WriteLine("🔍 Nocturne Connect Status");
            Console.WriteLine($"   Time: {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            Console.WriteLine($"   Source: {config.ConnectSource}");
            Console.WriteLine($"   Target: {config.NightscoutUrl}");

            // Check if configuration is valid
            if (_configurationManager.ValidateConfiguration(config))
            {
                Console.WriteLine("   Config: ✅ Valid");
            }
            else
            {
                Console.WriteLine("   Config: ❌ Invalid");
                return 1;
            }

            // Check daemon status using the new service
            var daemonStatus = await _daemonStatusService.GetDaemonStatusAsync(
                CancellationToken.None
            );
            var isHealthy = await _daemonStatusService.IsDaemonHealthyAsync(
                cancellationToken: CancellationToken.None
            );

            if (daemonStatus != null && isHealthy)
            {
                Console.WriteLine($"   Status: ✅ Running (PID: {daemonStatus.ProcessId})");
                Console.WriteLine($"   Started: {daemonStatus.StartedAt:yyyy-MM-dd HH:mm:ss} UTC");
                Console.WriteLine(
                    $"   Uptime: {DateTime.UtcNow - daemonStatus.StartedAt:dd\\:hh\\:mm\\:ss}"
                );
                Console.WriteLine($"   Interval: {daemonStatus.IntervalMinutes} minutes");
                Console.WriteLine($"   Syncs: {daemonStatus.SyncCount}");

                if (daemonStatus.LastSyncAt.HasValue)
                {
                    var lastSyncAge = DateTime.UtcNow - daemonStatus.LastSyncAt.Value;
                    Console.WriteLine($"   Last Sync: {lastSyncAge.TotalMinutes:F1} minutes ago");
                }
                else
                {
                    Console.WriteLine("   Last Sync: Never");
                }

                if (daemonStatus.Errors.Count > 0)
                {
                    Console.WriteLine($"   Recent Errors: {daemonStatus.Errors.Count}");
                }

                // Show performance metrics
                var metrics = await _daemonStatusService.GetPerformanceMetricsAsync(
                    CancellationToken.None
                );
                if (metrics != null)
                {
                    Console.WriteLine($"   Memory: {metrics.MemoryUsage / 1024 / 1024:F1} MB");
                    Console.WriteLine($"   CPU Time: {metrics.CpuTime.TotalSeconds:F1} seconds");

                    if (metrics.AverageSyncInterval > 0)
                    {
                        Console.WriteLine(
                            $"   Avg Sync Interval: {metrics.AverageSyncInterval:F1} minutes"
                        );
                    }
                }
            }
            else if (daemonStatus != null && !isHealthy)
            {
                Console.WriteLine($"   Status: ⚠️  Unhealthy (PID: {daemonStatus.ProcessId})");
                Console.WriteLine(
                    $"   Last Heartbeat: {DateTime.UtcNow - daemonStatus.LastHeartbeat:dd\\:hh\\:mm\\:ss} ago"
                );
            }
            else
            {
                Console.WriteLine("   Status: 💤 Not running");
            }

            if (settings.Watch)
            {
                Console.WriteLine("\n👀 Watching status (Press Ctrl+C to stop)...");
                await WatchStatusAsync(CancellationToken.None);
            }

            return 0;
        }
        catch (OperationCanceledException)
        {
            Console.WriteLine("\n   Status monitoring stopped.");
            return 0;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to check status");
            Console.WriteLine($"❌ Error: {ex.Message}");
            return 1;
        }
    }

    /// <summary>
    /// Continuously watches daemon status
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    private async Task WatchStatusAsync(CancellationToken cancellationToken)
    {
        var lastStatus = string.Empty;
        var lastSyncCount = -1;

        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var daemonStatus = await _daemonStatusService.GetDaemonStatusAsync(
                    cancellationToken
                );
                var isHealthy = await _daemonStatusService.IsDaemonHealthyAsync(
                    cancellationToken: cancellationToken
                );

                var currentTime = DateTime.Now.ToString("HH:mm:ss");

                if (daemonStatus != null && isHealthy)
                {
                    var statusIcon = daemonStatus.Status switch
                    {
                        "running" => "✅",
                        "error" => "❌",
                        _ => "🔄",
                    };

                    var statusText = $"{statusIcon} Running - Syncs: {daemonStatus.SyncCount}";

                    if (daemonStatus.SyncCount != lastSyncCount)
                    {
                        statusText += " (NEW SYNC)";
                        lastSyncCount = daemonStatus.SyncCount;
                    }

                    if (daemonStatus.LastSyncAt.HasValue)
                    {
                        var lastSyncAge = DateTime.UtcNow - daemonStatus.LastSyncAt.Value;
                        statusText += $" - Last: {lastSyncAge.TotalMinutes:F1}m ago";
                    }

                    if (statusText != lastStatus)
                    {
                        Console.WriteLine($"   {currentTime} - {statusText}");
                        lastStatus = statusText;
                    }
                }
                else if (daemonStatus != null && !isHealthy)
                {
                    var statusText =
                        $"⚠️  Unhealthy - Last heartbeat: {DateTime.UtcNow - daemonStatus.LastHeartbeat:hh\\:mm\\:ss} ago";
                    if (statusText != lastStatus)
                    {
                        Console.WriteLine($"   {currentTime} - {statusText}");
                        lastStatus = statusText;
                    }
                }
                else
                {
                    var statusText = "💤 Not running";
                    if (statusText != lastStatus)
                    {
                        Console.WriteLine($"   {currentTime} - {statusText}");
                        lastStatus = statusText;
                    }
                }

                await Task.Delay(5000, cancellationToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error during status watch");
                await Task.Delay(5000, cancellationToken);
            }
        }
    }
}
