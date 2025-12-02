using Microsoft.Extensions.Options;
using Nocturne.Core.Constants;
using Nocturne.Infrastructure.Data.Abstractions;
using Nocturne.Services.Demo.Configuration;

namespace Nocturne.Services.Demo.Services;

/// <summary>
/// Background service that generates demo data on startup and continues
/// generating real-time entries at configured intervals.
/// </summary>
public class DemoDataHostedService : BackgroundService
{
    private readonly IServiceProvider _serviceProvider;
    private readonly ILogger<DemoDataHostedService> _logger;
    private readonly DemoModeConfiguration _config;
    private readonly IDemoDataGenerator _generator;
    private readonly DemoServiceHealthCheck _healthCheck;

    public DemoDataHostedService(
        IServiceProvider serviceProvider,
        IOptions<DemoModeConfiguration> config,
        IDemoDataGenerator generator,
        DemoServiceHealthCheck healthCheck,
        ILogger<DemoDataHostedService> logger
    )
    {
        _serviceProvider = serviceProvider;
        _logger = logger;
        _config = config.Value;
        _generator = generator;
        _healthCheck = healthCheck;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!_config.Enabled)
        {
            _logger.LogInformation("Demo mode is disabled, service will not run");
            return;
        }

        // Mark the service as running
        ((DemoDataGenerator)_generator).IsRunning = true;

        try
        {
            // Clear and regenerate on startup if configured
            if (_config.ClearOnStartup || _config.RegenerateOnStartup)
            {
                await RegenerateDataAsync(stoppingToken);
            }

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
        finally
        {
            // Mark as unhealthy when stopping - this signals the API to clean up
            _healthCheck.IsHealthy = false;
            ((DemoDataGenerator)_generator).IsRunning = false;
        }
    }

    /// <summary>
    /// Clears all demo data and regenerates historical data.
    /// </summary>
    public async Task RegenerateDataAsync(CancellationToken cancellationToken)
    {
        _logger.LogInformation("Regenerating demo data - clearing existing data first");

        using var scope = _serviceProvider.CreateScope();
        var postgreSqlService = scope.ServiceProvider.GetRequiredService<IPostgreSqlService>();
        var entryService = scope.ServiceProvider.GetRequiredService<IDemoEntryService>();
        var treatmentService = scope.ServiceProvider.GetRequiredService<IDemoTreatmentService>();

        // Clear existing demo data
        var entriesDeleted = await postgreSqlService.DeleteEntriesByDataSourceAsync(
            DataSources.DemoService,
            cancellationToken
        );
        var treatmentsDeleted = await postgreSqlService.DeleteTreatmentsByDataSourceAsync(
            DataSources.DemoService,
            cancellationToken
        );

        _logger.LogInformation(
            "Cleared {Entries} demo entries and {Treatments} demo treatments",
            entriesDeleted,
            treatmentsDeleted
        );

        // Generate new historical data
        var startTime = DateTime.UtcNow;
        var (entries, treatments) = _generator.GenerateHistoricalData();

        _logger.LogInformation(
            "Generated {Entries} entries and {Treatments} treatments, saving to database...",
            entries.Count,
            treatments.Count
        );

        // Save in batches to avoid memory issues
        const int batchSize = 5000;

        for (var i = 0; i < entries.Count; i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = entries.Skip(i).Take(batchSize).ToList();
            await entryService.CreateEntriesAsync(batch, cancellationToken);
        }

        for (var i = 0; i < treatments.Count; i += batchSize)
        {
            cancellationToken.ThrowIfCancellationRequested();
            var batch = treatments.Skip(i).Take(batchSize).ToList();
            await treatmentService.CreateTreatmentsAsync(batch, cancellationToken);
        }

        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation(
            "Completed demo data regeneration: {Entries} entries, {Treatments} treatments in {Duration}",
            entries.Count,
            treatments.Count,
            duration
        );
    }

    private async Task GenerateAndSaveEntryAsync(CancellationToken cancellationToken)
    {
        using var scope = _serviceProvider.CreateScope();
        var entryService = scope.ServiceProvider.GetRequiredService<IDemoEntryService>();

        try
        {
            var entry = _generator.GenerateCurrentEntry();

            _logger.LogInformation(
                "Demo data: Generated entry SGV={Sgv}, Direction={Direction}",
                entry.Sgv,
                entry.Direction
            );

            await entryService.CreateEntriesAsync(new[] { entry }, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate and save demo entry");
            throw;
        }
    }
}
