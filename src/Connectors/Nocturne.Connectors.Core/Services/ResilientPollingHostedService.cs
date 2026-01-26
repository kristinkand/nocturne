using System;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nocturne.Connectors.Core.Interfaces;

namespace Nocturne.Connectors.Core.Services;

/// <summary>
/// Base class for connector hosted services with resilient polling behavior.
/// Features:
/// - Adaptive polling: Normal interval when healthy, fast interval (10s) when disconnected
/// - Tracks last successful sync timestamp for backfill on reconnection
/// - Automatic backfill when connection is restored after disruption
/// - Exponential backoff after extended failures to avoid overwhelming APIs
/// - Graceful self-disabling when Enabled flag is set to false
/// </summary>
/// <typeparam name="TConnector">The connector service type</typeparam>
/// <typeparam name="TConfig">The connector configuration type</typeparam>
public abstract class ResilientPollingHostedService<TConnector, TConfig> : BackgroundService
    where TConnector : class
    where TConfig : IConnectorConfiguration
{
    protected readonly IServiceProvider ServiceProvider;
    protected readonly ILogger Logger;
    protected TConfig Config;

    /// <summary>
    /// Normal polling interval from configuration (e.g., 5 minutes)
    /// </summary>
    protected abstract TimeSpan NormalPollingInterval { get; }

    /// <summary>
    /// Fast polling interval when disconnected (default: 10 seconds)
    /// </summary>
    protected virtual TimeSpan DisconnectedPollingInterval => TimeSpan.FromSeconds(10);

    /// <summary>
    /// Maximum number of fast poll attempts before backing off
    /// After this many failures, we switch to exponential backoff
    /// </summary>
    protected virtual int MaxFastPollAttempts => 30; // 30 * 10s = 5 minutes of fast polling

    /// <summary>
    /// Maximum backoff interval during extended outages
    /// </summary>
    protected virtual TimeSpan MaxBackoffInterval => TimeSpan.FromMinutes(5);

    /// <summary>
    /// Interval to check for configuration changes while in standby mode.
    /// Default: 30 seconds.
    /// </summary>
    protected virtual TimeSpan StandbyCheckInterval => TimeSpan.FromSeconds(30);

    /// <summary>
    /// Connector name for logging
    /// </summary>
    protected abstract string ConnectorName { get; }

    // Connection state tracking
    private DateTime? _lastSuccessfulSync;
    private bool _wasDisconnected;
    private int _consecutiveFailures;

    // Standby state
    private bool _isInStandby;
    private readonly SemaphoreSlim _configChangeLock = new(1, 1);

    protected ResilientPollingHostedService(
        IServiceProvider serviceProvider,
        ILogger logger,
        TConfig config)
    {
        ServiceProvider = serviceProvider ?? throw new ArgumentNullException(nameof(serviceProvider));
        Logger = logger ?? throw new ArgumentNullException(nameof(logger));
        Config = config ?? throw new ArgumentNullException(nameof(config));
    }

    /// <summary>
    /// Called when configuration is updated externally.
    /// Override to handle runtime configuration changes.
    /// </summary>
    /// <param name="newConfig">The new configuration</param>
    public virtual void OnConfigurationChanged(TConfig newConfig)
    {
        _configChangeLock.Wait();
        try
        {
            Config = newConfig;
            Logger.LogInformation("{ConnectorName} Configuration updated at runtime", ConnectorName);
        }
        finally
        {
            _configChangeLock.Release();
        }
    }

    /// <summary>
    /// Check if the connector is currently enabled.
    /// </summary>
    protected virtual bool IsEnabled()
    {
        _configChangeLock.Wait();
        try
        {
            return Config.Enabled;
        }
        finally
        {
            _configChangeLock.Release();
        }
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        Logger.LogInformation("{ConnectorName} Resilient Polling Service started", ConnectorName);
        Logger.LogInformation(
            "{ConnectorName} Normal polling interval: {Interval}",
            ConnectorName,
            NormalPollingInterval);

        try
        {
            // Check initial enabled state
            if (!IsEnabled())
            {
                Logger.LogInformation("{ConnectorName} Connector is disabled, entering standby mode", ConnectorName);
                _isInStandby = true;
                await WaitForEnableAsync(stoppingToken);
            }

            // Initial sync
            await PerformSyncCycleAsync(stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                // Check if we've been disabled
                if (!IsEnabled())
                {
                    Logger.LogInformation("{ConnectorName} Connector has been disabled, entering standby mode", ConnectorName);
                    _isInStandby = true;
                    await WaitForEnableAsync(stoppingToken);
                    Logger.LogInformation("{ConnectorName} Connector re-enabled, resuming polling", ConnectorName);
                    _isInStandby = false;
                    continue;
                }

                var delay = CalculateNextPollInterval();

                Logger.LogDebug(
                    "{ConnectorName} Next poll in {Delay} (failures: {Failures})",
                    ConnectorName,
                    delay,
                    _consecutiveFailures);

                try
                {
                    await Task.Delay(delay, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    break;
                }

                await PerformSyncCycleAsync(stoppingToken);
            }
        }
        catch (OperationCanceledException)
        {
            Logger.LogInformation("{ConnectorName} Resilient Polling Service cancellation requested", ConnectorName);
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{ConnectorName} Unexpected error in Resilient Polling Service", ConnectorName);
            throw;
        }
        finally
        {
            Logger.LogInformation("{ConnectorName} Resilient Polling Service stopped", ConnectorName);
        }
    }

    /// <summary>
    /// Wait for the connector to be enabled.
    /// Polls the configuration at the standby check interval.
    /// </summary>
    private async Task WaitForEnableAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested && !IsEnabled())
        {
            try
            {
                await Task.Delay(StandbyCheckInterval, stoppingToken);
            }
            catch (OperationCanceledException)
            {
                break;
            }

            // Log periodically to show we're still alive
            Logger.LogDebug("{ConnectorName} Standby mode - waiting for enable signal", ConnectorName);
        }
    }

    /// <summary>
    /// Gets whether the connector is currently in standby mode (disabled).
    /// </summary>
    public bool IsInStandby => _isInStandby;

    /// <summary>
    /// Performs a single sync cycle with the connector.
    /// Override this to customize the sync behavior.
    /// </summary>
    protected virtual async Task PerformSyncCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = ServiceProvider.CreateScope();
            var connector = scope.ServiceProvider.GetRequiredService<TConnector>();

            Logger.LogDebug("{ConnectorName} Starting data sync cycle", ConnectorName);

            // Check if we're recovering from a disconnection
            var isBackfill = _wasDisconnected && _lastSuccessfulSync.HasValue;
            var backfillFrom = isBackfill ? _lastSuccessfulSync : null;

            if (isBackfill)
            {
                Logger.LogInformation(
                    "{ConnectorName} Connection restored! Performing backfill from {BackfillFrom:yyyy-MM-dd HH:mm:ss} UTC",
                    ConnectorName,
                    backfillFrom);
            }

            var success = await ExecuteSyncAsync(connector, backfillFrom, cancellationToken);

            if (success)
            {
                HandleSyncSuccess();
            }
            else
            {
                HandleSyncFailure();
            }
        }
        catch (Exception ex)
        {
            Logger.LogError(ex, "{ConnectorName} Error during data sync cycle", ConnectorName);
            HandleSyncFailure();
        }
    }

    /// <summary>
    /// Execute the actual sync operation with the connector.
    /// Override this in derived classes to implement connector-specific sync logic.
    /// </summary>
    /// <param name="connector">The connector service instance</param>
    /// <param name="backfillFrom">If set, perform a backfill from this timestamp</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if sync was successful, false otherwise</returns>
    protected abstract Task<bool> ExecuteSyncAsync(
        TConnector connector,
        DateTime? backfillFrom,
        CancellationToken cancellationToken);

    /// <summary>
    /// Calculate the next poll interval based on connection state
    /// </summary>
    private TimeSpan CalculateNextPollInterval()
    {
        if (_consecutiveFailures == 0)
        {
            // Healthy state: use normal polling interval
            return NormalPollingInterval;
        }

        if (_consecutiveFailures <= MaxFastPollAttempts)
        {
            // Disconnected but still in fast-poll phase
            return DisconnectedPollingInterval;
        }

        // Extended outage: use exponential backoff
        var backoffMultiplier = Math.Min(_consecutiveFailures - MaxFastPollAttempts, 10);
        var backoffSeconds = DisconnectedPollingInterval.TotalSeconds * Math.Pow(1.5, backoffMultiplier);
        var backoff = TimeSpan.FromSeconds(Math.Min(backoffSeconds, MaxBackoffInterval.TotalSeconds));

        Logger.LogDebug(
            "{ConnectorName} Extended outage detected, using backoff interval: {Backoff}",
            ConnectorName,
            backoff);

        return backoff;
    }

    /// <summary>
    /// Handle a successful sync
    /// </summary>
    private void HandleSyncSuccess()
    {
        var now = DateTime.UtcNow;

        if (_consecutiveFailures > 0)
        {
            Logger.LogInformation(
                "{ConnectorName} Connection restored after {Failures} failed attempts. Backfill complete.",
                ConnectorName,
                _consecutiveFailures);
        }

        _lastSuccessfulSync = now;
        _wasDisconnected = false;
        _consecutiveFailures = 0;

        Logger.LogInformation("{ConnectorName} Data sync completed successfully", ConnectorName);
    }

    /// <summary>
    /// Handle a failed sync
    /// </summary>
    private void HandleSyncFailure()
    {
        _consecutiveFailures++;

        if (_consecutiveFailures == 1)
        {
            // First failure - we just became disconnected
            _wasDisconnected = true;
            Logger.LogWarning(
                "{ConnectorName} Connection lost, switching to fast polling ({Interval}s)",
                ConnectorName,
                DisconnectedPollingInterval.TotalSeconds);
        }
        else if (_consecutiveFailures == MaxFastPollAttempts)
        {
            Logger.LogWarning(
                "{ConnectorName} Extended outage detected ({Failures} failures), switching to backoff mode",
                ConnectorName,
                _consecutiveFailures);
        }
        else
        {
            Logger.LogWarning(
                "{ConnectorName} Data sync failed (attempt {Failures})",
                ConnectorName,
                _consecutiveFailures);
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        Logger.LogInformation("{ConnectorName} Resilient Polling Service is stopping...", ConnectorName);
        await base.StopAsync(cancellationToken);
    }
}
