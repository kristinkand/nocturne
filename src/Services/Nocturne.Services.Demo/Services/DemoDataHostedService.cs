using Microsoft.Extensions.Options;
using Nocturne.Core.Constants;
using Nocturne.Core.Models;
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
    /// Clears all demo data and regenerates historical data using streaming pattern.
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

        // Generate and save data using streaming pattern to minimize memory usage
        var startTime = DateTime.UtcNow;
        const int batchSize = 1000;

        // Stream and save entries in batches
        var entryCount = 0;
        var entryBatch = new List<Entry>(batchSize);

        foreach (var entry in _generator.GenerateHistoricalEntries())
        {
            cancellationToken.ThrowIfCancellationRequested();
            entryBatch.Add(entry);

            if (entryBatch.Count >= batchSize)
            {
                await entryService.CreateEntriesAsync(entryBatch, cancellationToken);
                entryCount += entryBatch.Count;
                entryBatch.Clear();
            }
        }

        // Save remaining entries
        if (entryBatch.Count > 0)
        {
            await entryService.CreateEntriesAsync(entryBatch, cancellationToken);
            entryCount += entryBatch.Count;
            entryBatch.Clear();
        }

        _logger.LogInformation("Saved {Count} entries using streaming pattern", entryCount);

        // Stream and save treatments in batches
        var treatmentCount = 0;
        var treatmentBatch = new List<Treatment>(batchSize);

        foreach (var treatment in _generator.GenerateHistoricalTreatments())
        {
            cancellationToken.ThrowIfCancellationRequested();
            treatmentBatch.Add(treatment);

            if (treatmentBatch.Count >= batchSize)
            {
                await treatmentService.CreateTreatmentsAsync(treatmentBatch, cancellationToken);
                treatmentCount += treatmentBatch.Count;
                treatmentBatch.Clear();
            }
        }

        // Save remaining treatments
        if (treatmentBatch.Count > 0)
        {
            await treatmentService.CreateTreatmentsAsync(treatmentBatch, cancellationToken);
            treatmentCount += treatmentBatch.Count;
            treatmentBatch.Clear();
        }

        _logger.LogInformation("Saved {Count} treatments using streaming pattern", treatmentCount);

        var duration = DateTime.UtcNow - startTime;
        _logger.LogInformation(
            "Completed demo data regeneration: {Entries} entries, {Treatments} treatments in {Duration}",
            entryCount,
            treatmentCount,
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
