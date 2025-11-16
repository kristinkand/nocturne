using Nocturne.Core.Contracts;

namespace Nocturne.API.Services;

public class DemoDataBackgroundService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DemoDataBackgroundService> _logger;
    private readonly DemoModeConfiguration _config;

    public DemoDataBackgroundService(
        IServiceProvider serviceProvider,
        IConfiguration configuration,
        ILogger<DemoDataBackgroundService> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config =
            configuration.GetSection("DemoMode").Get<DemoModeConfiguration>()
            ?? new DemoModeConfiguration();
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("Demo mode is disabled, background service will not run");
            return;
        }

        _logger.LogInformation(
            "Starting demo data generation with {IntervalMinutes} minute intervals",
            _config.IntervalMinutes
        );

        // Generate initial entry immediately
        await GenerateAndSaveEntryAsync(stoppingToken);

        // Set up timer for regular generation
        var interval = TimeSpan.FromMinutes(_config.IntervalMinutes);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await Task.Delay(interval, stoppingToken);
                await GenerateAndSaveEntryAsync(stoppingToken);
            }
            catch (OperationCanceledException)
            {
                _logger.LogInformation("Demo data generation service is stopping");
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error generating demo data");
                // Continue running even if one generation fails
            }
        }
    }

    private async Task GenerateAndSaveEntryAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var demoDataService = scope.ServiceProvider.GetRequiredService<IDemoDataService>();
        var entryService = scope.ServiceProvider.GetRequiredService<IEntryService>();
        var broadcastService = scope.ServiceProvider.GetRequiredService<ISignalRBroadcastService>();

        try
        {
            var entry = await demoDataService.GenerateEntryAsync(cancellationToken);

            _logger.LogInformation(
                "Demo data: Generated entry SGV={Sgv}, Direction={Direction}, IsDemo={IsDemo}",
                entry.Sgv,
                entry.Direction,
                entry.IsDemo
            );

            await entryService.CreateEntriesAsync(new[] { entry }, cancellationToken);

            _logger.LogInformation(
                "Demo data: Entry saved to database and should trigger WebSocket broadcasts"
            );

            // Additional diagnostic: Try direct broadcast to verify SignalR is working
            try
            {
                await broadcastService.BroadcastDataUpdateAsync(new[] { entry });
                _logger.LogInformation("Demo data: Direct dataUpdate broadcast sent successfully");
            }
            catch (Exception broadcastEx)
            {
                _logger.LogError(
                    broadcastEx,
                    "Demo data: Direct dataUpdate broadcast failed - WebSocket bridge may not be receiving events"
                );
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate and save demo entry");
            throw;
        }
    }
}
