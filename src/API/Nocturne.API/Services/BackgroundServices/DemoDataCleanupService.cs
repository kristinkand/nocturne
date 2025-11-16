using Nocturne.Infrastructure.Data.Abstractions;

namespace Nocturne.API.Services.BackgroundServices;

/// <summary>
/// Cleanup service that deletes all demo entries when demo mode is disabled.
/// Runs once on application startup.
/// </summary>
public class DemoDataCleanupService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DemoDataCleanupService> _logger;
    private readonly DemoModeConfiguration _config;
    private readonly bool _shouldCleanup;

    public DemoDataCleanupService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<DemoDataCleanupService> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config =
            configuration.GetSection("DemoMode").Get<DemoModeConfiguration>()
            ?? new DemoModeConfiguration();

        // Only cleanup if demo mode is disabled (to clean up leftover data)
        _shouldCleanup = !_config.Enabled;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_shouldCleanup)
        {
            _logger.LogInformation("Demo mode is enabled, cleanup service will not run");
            return;
        }

        _logger.LogInformation(
            "Demo data cleanup service is starting - deleting any existing demo entries"
        );

        try
        {
            await CleanupDemoDataAsync(stoppingToken);
            _logger.LogInformation("Demo data cleanup completed successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during demo data cleanup");
            // Don't throw - allow application to continue even if cleanup fails
        }
    }

    private async Task CleanupDemoDataAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var postgreSqlService = scope.ServiceProvider.GetRequiredService<IPostgreSqlService>();

        var deletedCount = await postgreSqlService.DeleteDemoEntriesAsync(cancellationToken);

        if (deletedCount > 0)
        {
            _logger.LogInformation("Deleted {Count} demo entries from database", deletedCount);
        }
        else
        {
            _logger.LogInformation("No demo entries found to delete");
        }
    }
}
