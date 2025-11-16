using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;
using Nocturne.Infrastructure.Cache.Abstractions;

namespace Nocturne.Tools.Connect.Services;

/// <summary>
/// Service for monitoring daemon process status and managing process lifecycle
/// </summary>
public class DaemonStatusService
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<DaemonStatusService> _logger;
    private const string DaemonStatusKeyPrefix = "daemon:status";
    private const string ProcessIdFile = "nocturne-connect.pid";
    private static readonly TimeSpan StatusTtl = TimeSpan.FromMinutes(5); // Status expires after 5 minutes of inactivity

    public DaemonStatusService(ICacheService cacheService, ILogger<DaemonStatusService> logger)
    {
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Gets the current daemon status information
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Daemon status information or null if not running</returns>
    public async Task<DaemonStatusInfo?> GetDaemonStatusAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var key = GetDaemonStatusKey();
            var statusInfo = await _cacheService.GetAsync<DaemonStatusInfo>(key, cancellationToken);

            if (statusInfo != null)
            {
                // Verify the process is actually running
                if (IsProcessRunning(statusInfo.ProcessId))
                {
                    return statusInfo;
                }
                else
                {
                    // Process is not running, clean up stale status
                    await RemoveDaemonStatusAsync(cancellationToken);
                    return null;
                }
            }

            return null;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving daemon status");
            return null;
        }
    }

    /// <summary>
    /// Updates the daemon status information
    /// </summary>
    /// <param name="statusInfo">Status information to update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    public async Task UpdateDaemonStatusAsync(
        DaemonStatusInfo statusInfo,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var key = GetDaemonStatusKey();
            await _cacheService.SetAsync(key, statusInfo, StatusTtl, cancellationToken);

            _logger.LogDebug("Updated daemon status for process {ProcessId}", statusInfo.ProcessId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating daemon status");
        }
    }

    /// <summary>
    /// Registers a new daemon process
    /// </summary>
    /// <param name="connectSource">Data source being monitored</param>
    /// <param name="intervalMinutes">Sync interval in minutes</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    public async Task RegisterDaemonAsync(
        string connectSource,
        int intervalMinutes,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var processId = Environment.ProcessId;
            var statusInfo = new DaemonStatusInfo
            {
                ProcessId = processId,
                ConnectSource = connectSource,
                StartedAt = DateTime.UtcNow,
                LastHeartbeat = DateTime.UtcNow,
                IntervalMinutes = intervalMinutes,
                Status = "running",
                SyncCount = 0,
                LastSyncAt = null,
                Errors = new List<string>(),
            };

            await UpdateDaemonStatusAsync(statusInfo, cancellationToken);

            // Write PID file for external monitoring
            await WritePidFileAsync(processId);

            _logger.LogInformation(
                "Registered daemon process {ProcessId} for source {Source}",
                processId,
                connectSource
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error registering daemon process");
        }
    }

    /// <summary>
    /// Updates the heartbeat for the daemon process
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    public async Task UpdateHeartbeatAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var statusInfo = await GetDaemonStatusAsync(cancellationToken);
            if (statusInfo != null)
            {
                statusInfo.LastHeartbeat = DateTime.UtcNow;
                await UpdateDaemonStatusAsync(statusInfo, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating daemon heartbeat");
        }
    }

    /// <summary>
    /// Records a successful sync operation
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    public async Task RecordSyncSuccessAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var statusInfo = await GetDaemonStatusAsync(cancellationToken);
            if (statusInfo != null)
            {
                statusInfo.SyncCount++;
                statusInfo.LastSyncAt = DateTime.UtcNow;
                statusInfo.LastHeartbeat = DateTime.UtcNow;
                statusInfo.Status = "running";
                await UpdateDaemonStatusAsync(statusInfo, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording sync success");
        }
    }

    /// <summary>
    /// Records a sync error
    /// </summary>
    /// <param name="error">Error message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    public async Task RecordSyncErrorAsync(
        string error,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var statusInfo = await GetDaemonStatusAsync(cancellationToken);
            if (statusInfo != null)
            {
                statusInfo.Errors.Add($"{DateTime.UtcNow:yyyy-MM-dd HH:mm:ss} UTC: {error}");
                statusInfo.LastHeartbeat = DateTime.UtcNow;
                statusInfo.Status = "error";

                // Keep only the last 10 errors
                if (statusInfo.Errors.Count > 10)
                {
                    statusInfo.Errors.RemoveAt(0);
                }

                await UpdateDaemonStatusAsync(statusInfo, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error recording sync error");
        }
    }

    /// <summary>
    /// Removes daemon status (called when daemon stops)
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task</returns>
    public async Task RemoveDaemonStatusAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            var key = GetDaemonStatusKey();
            await _cacheService.RemoveAsync(key, cancellationToken);

            // Remove PID file
            await RemovePidFileAsync();

            _logger.LogInformation("Removed daemon status");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error removing daemon status");
        }
    }

    /// <summary>
    /// Checks if the daemon is healthy (recent heartbeat)
    /// </summary>
    /// <param name="maxAge">Maximum age for heartbeat to consider healthy</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if healthy, false otherwise</returns>
    public async Task<bool> IsDaemonHealthyAsync(
        TimeSpan? maxAge = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var statusInfo = await GetDaemonStatusAsync(cancellationToken);
            if (statusInfo == null)
            {
                return false;
            }

            var maxAgeToUse = maxAge ?? TimeSpan.FromMinutes(10); // Default: consider unhealthy after 10 minutes
            var age = DateTime.UtcNow - statusInfo.LastHeartbeat;

            return age <= maxAgeToUse && IsProcessRunning(statusInfo.ProcessId);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error checking daemon health");
            return false;
        }
    }

    /// <summary>
    /// Gets performance metrics for the daemon
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Performance metrics or null if not available</returns>
    public async Task<DaemonPerformanceMetrics?> GetPerformanceMetricsAsync(
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            var statusInfo = await GetDaemonStatusAsync(cancellationToken);
            if (statusInfo == null)
            {
                return null;
            }

            var process = GetProcessById(statusInfo.ProcessId);
            if (process == null)
            {
                return null;
            }

            var uptime = DateTime.UtcNow - statusInfo.StartedAt;
            var avgSyncInterval =
                statusInfo.SyncCount > 0 && statusInfo.LastSyncAt.HasValue
                    ? (statusInfo.LastSyncAt.Value - statusInfo.StartedAt).TotalMinutes
                        / statusInfo.SyncCount
                    : 0;

            return new DaemonPerformanceMetrics
            {
                Uptime = uptime,
                MemoryUsage = process.WorkingSet64,
                CpuTime = process.TotalProcessorTime,
                SyncCount = statusInfo.SyncCount,
                AverageSyncInterval = avgSyncInterval,
                ErrorCount = statusInfo.Errors.Count,
                LastSyncAge = statusInfo.LastSyncAt.HasValue
                    ? DateTime.UtcNow - statusInfo.LastSyncAt.Value
                    : null,
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error getting performance metrics");
            return null;
        }
    }

    /// <summary>
    /// Checks if a process with the given ID is running
    /// </summary>
    /// <param name="processId">Process ID to check</param>
    /// <returns>True if running, false otherwise</returns>
    private static bool IsProcessRunning(int processId)
    {
        try
        {
            var process = Process.GetProcessById(processId);
            return !process.HasExited;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>
    /// Gets a process by ID, or null if not found
    /// </summary>
    /// <param name="processId">Process ID</param>
    /// <returns>Process or null</returns>
    private static Process? GetProcessById(int processId)
    {
        try
        {
            return Process.GetProcessById(processId);
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Writes the current process ID to a file for external monitoring
    /// </summary>
    /// <param name="processId">Process ID to write</param>
    /// <returns>Task</returns>
    private async Task WritePidFileAsync(int processId)
    {
        try
        {
            await File.WriteAllTextAsync(ProcessIdFile, processId.ToString());
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not write PID file");
        }
    }

    /// <summary>
    /// Removes the PID file
    /// </summary>
    /// <returns>Task</returns>
    private async Task RemovePidFileAsync()
    {
        try
        {
            if (File.Exists(ProcessIdFile))
            {
                File.Delete(ProcessIdFile);
            }
            await Task.CompletedTask;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Could not remove PID file");
        }
    }

    /// <summary>
    /// Gets the cache key for daemon status
    /// </summary>
    /// <returns>Cache key</returns>
    private static string GetDaemonStatusKey() => $"{DaemonStatusKeyPrefix}:main";
}

/// <summary>
/// Information about a running daemon process
/// </summary>
public class DaemonStatusInfo
{
    public int ProcessId { get; set; }
    public string ConnectSource { get; set; } = string.Empty;
    public DateTime StartedAt { get; set; }
    public DateTime LastHeartbeat { get; set; }
    public int IntervalMinutes { get; set; }
    public string Status { get; set; } = "running"; // running, error, stopping
    public int SyncCount { get; set; }
    public DateTime? LastSyncAt { get; set; }
    public List<string> Errors { get; set; } = new();
}

/// <summary>
/// Performance metrics for a daemon process
/// </summary>
public class DaemonPerformanceMetrics
{
    public TimeSpan Uptime { get; set; }
    public long MemoryUsage { get; set; }
    public TimeSpan CpuTime { get; set; }
    public int SyncCount { get; set; }
    public double AverageSyncInterval { get; set; }
    public int ErrorCount { get; set; }
    public TimeSpan? LastSyncAge { get; set; }
}
