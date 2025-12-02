using Nocturne.Core.Constants;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Abstractions;

namespace Nocturne.Services.Demo.Services;

/// <summary>
/// Service for managing demo entries in the database.
/// </summary>
public interface IDemoEntryService
{
    Task CreateEntriesAsync(
        IEnumerable<Entry> entries,
        CancellationToken cancellationToken = default
    );
    Task<long> DeleteAllDemoEntriesAsync(CancellationToken cancellationToken = default);
    Task<bool> HasDemoEntriesAsync(CancellationToken cancellationToken = default);
}

public class DemoEntryService : IDemoEntryService
{
    private readonly IPostgreSqlService _postgreSqlService;
    private readonly ILogger<DemoEntryService> _logger;

    public DemoEntryService(IPostgreSqlService postgreSqlService, ILogger<DemoEntryService> logger)
    {
        _postgreSqlService = postgreSqlService;
        _logger = logger;
    }

    public async Task CreateEntriesAsync(
        IEnumerable<Entry> entries,
        CancellationToken cancellationToken = default
    )
    {
        var entryList = entries.ToList();
        if (!entryList.Any())
            return;

        await _postgreSqlService.CreateEntriesAsync(entryList, cancellationToken);
        _logger.LogDebug("Created {Count} demo entries", entryList.Count);
    }

    public async Task<long> DeleteAllDemoEntriesAsync(CancellationToken cancellationToken = default)
    {
        var count = await _postgreSqlService.DeleteEntriesByDataSourceAsync(
            DataSources.DemoService,
            cancellationToken
        );
        _logger.LogInformation("Deleted {Count} demo entries", count);
        return count;
    }

    public async Task<bool> HasDemoEntriesAsync(CancellationToken cancellationToken = default)
    {
        // Use a simple query to check for demo entries
        var count = await _postgreSqlService.CountEntriesAsync(
            findQuery: "{\"data_source\":\"" + DataSources.DemoService + "\"}",
            cancellationToken: cancellationToken
        );
        return count > 0;
    }
}
