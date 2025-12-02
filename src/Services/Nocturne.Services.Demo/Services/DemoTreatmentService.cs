using Nocturne.Core.Constants;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Abstractions;

namespace Nocturne.Services.Demo.Services;

/// <summary>
/// Service for managing demo treatments in the database.
/// </summary>
public interface IDemoTreatmentService
{
    Task CreateTreatmentsAsync(
        IEnumerable<Treatment> treatments,
        CancellationToken cancellationToken = default
    );
    Task<long> DeleteAllDemoTreatmentsAsync(CancellationToken cancellationToken = default);
}

public class DemoTreatmentService : IDemoTreatmentService
{
    private readonly IPostgreSqlService _postgreSqlService;
    private readonly ILogger<DemoTreatmentService> _logger;

    public DemoTreatmentService(
        IPostgreSqlService postgreSqlService,
        ILogger<DemoTreatmentService> logger
    )
    {
        _postgreSqlService = postgreSqlService;
        _logger = logger;
    }

    public async Task CreateTreatmentsAsync(
        IEnumerable<Treatment> treatments,
        CancellationToken cancellationToken = default
    )
    {
        var treatmentList = treatments.ToList();
        if (!treatmentList.Any())
            return;

        await _postgreSqlService.CreateTreatmentsAsync(treatmentList, cancellationToken);
        _logger.LogDebug("Created {Count} demo treatments", treatmentList.Count);
    }

    public async Task<long> DeleteAllDemoTreatmentsAsync(
        CancellationToken cancellationToken = default
    )
    {
        var count = await _postgreSqlService.DeleteTreatmentsByDataSourceAsync(
            DataSources.DemoService,
            cancellationToken
        );
        _logger.LogInformation("Deleted {Count} demo treatments", count);
        return count;
    }
}
