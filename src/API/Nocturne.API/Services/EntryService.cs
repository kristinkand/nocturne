using Nocturne.Infrastructure.Cache.Constants;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Cache.Abstractions;
using Nocturne.Infrastructure.Cache.Configuration;
using Nocturne.Infrastructure.Cache.Keys;
using Nocturne.Infrastructure.Data.Abstractions;

namespace Nocturne.API.Services;

/// <summary>
/// Domain service implementation for entry operations with WebSocket broadcasting
/// </summary>
public class EntryService : IEntryService
{
    private readonly IPostgreSqlService _postgreSqlService;
    private readonly ISignalRBroadcastService _broadcastService;
    private readonly ICacheService _cacheService;
    private readonly CacheConfiguration _cacheConfig;
    private readonly ILogger<EntryService> _logger;
    private const string CollectionName = "entries";
    private const string DefaultTenantId = "default"; // TODO: Replace with actual tenant context

    public EntryService(
        IPostgreSqlService postgreSqlService,
        ISignalRBroadcastService broadcastService,
        ICacheService cacheService,
        IOptions<CacheConfiguration> cacheConfig,
        ILogger<EntryService> logger
    )
    {
        _postgreSqlService = postgreSqlService;
        _broadcastService = broadcastService;
        _cacheService = cacheService;
        _cacheConfig = cacheConfig.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Entry>> GetEntriesAsync(
        string? find = null,
        int? count = null,
        int? skip = null,
        CancellationToken cancellationToken = default
    )
    {
        var actualCount = count ?? 10;
        var actualSkip = skip ?? 0;

        // Cache recent entries for common queries (skip = 0 and common counts)
        if (actualSkip == 0 && IsCommonEntryCount(actualCount))
        {
            var cacheKey = CacheKeyBuilder.BuildRecentEntriesKey(
                DefaultTenantId,
                actualCount,
                find
            );
            var cacheTtl = TimeSpan.FromSeconds(CacheConstants.Defaults.RecentEntriesExpirationSeconds);

            return await _cacheService.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    _logger.LogDebug(
                        "Cache MISS for recent entries (count: {Count}, type: {Type}), fetching from database",
                        actualCount,
                        find ?? "all"
                    );
                    var entries = await _postgreSqlService.GetEntriesWithAdvancedFilterAsync(
                        type: "sgv", // Default to SGV entries
                        count: actualCount,
                        skip: actualSkip,
                        findQuery: find,
                        cancellationToken: cancellationToken
                    );
                    return entries.ToList(); // Materialize to avoid multiple enumerations
                },
                cacheTtl,
                cancellationToken
            );
        }

        // Non-cached path for non-standard queries
        return await _postgreSqlService.GetEntriesWithAdvancedFilterAsync(
            type: "sgv", // Default to SGV entries
            count: actualCount,
            skip: actualSkip,
            findQuery: find,
            cancellationToken: cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Entry>> GetEntriesAsync(
        string? type,
        int count,
        int skip,
        CancellationToken cancellationToken
    )
    {
        // Cache recent entries for common queries (skip = 0 and common counts)
        if (skip == 0 && IsCommonEntryCount(count))
        {
            var cacheKey = CacheKeyBuilder.BuildRecentEntriesKey(DefaultTenantId, count, type);
            var cacheTtl = TimeSpan.FromSeconds(CacheConstants.Defaults.RecentEntriesExpirationSeconds);

            return await _cacheService.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    _logger.LogDebug(
                        "Cache MISS for recent entries (count: {Count}, type: {Type}), fetching from database",
                        count,
                        type ?? "all"
                    );
                    var entries = await _postgreSqlService.GetEntriesAsync(
                        type,
                        count,
                        skip,
                        cancellationToken
                    );
                    return entries.ToList(); // Materialize to avoid multiple enumerations
                },
                cacheTtl,
                cancellationToken
            );
        }

        // Non-cached path for non-standard queries
        return await _postgreSqlService.GetEntriesAsync(type, count, skip, cancellationToken);
    }

    /// <summary>
    /// Determines if the entry count is common enough to cache
    /// </summary>
    /// <param name="count">The count to check</param>
    /// <returns>True if the count is common (10, 50, 100), false otherwise</returns>
    private static bool IsCommonEntryCount(int count)
    {
        return count is 10 or 50 or 100;
    }

    /// <inheritdoc />
    public async Task<Entry?> GetEntryByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetEntryByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Entry?> CheckForDuplicateEntryAsync(
        string? device,
        string type,
        double? sgv,
        long mills,
        int windowMinutes = 5,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CheckForDuplicateEntryAsync(
            device,
            type,
            sgv,
            mills,
            windowMinutes,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Entry>> CreateEntriesAsync(
        IEnumerable<Entry> entries,
        CancellationToken cancellationToken = default
    )
    {
        var createdEntries = await _postgreSqlService.CreateEntriesAsync(
            entries,
            cancellationToken
        );

        // Invalidate current entry cache since new entries were created
        try
        {
            await _cacheService.RemoveAsync("entries:current", cancellationToken);
            _logger.LogInformation(
                "Cache INVALIDATION: entries:current after creating {Count} entries",
                createdEntries.Count()
            );

            // Invalidate all recent entries caches using pattern matching
            var recentEntriesPattern = CacheKeyBuilder.BuildRecentEntriesPattern(DefaultTenantId);
            await _cacheService.RemoveByPatternAsync(recentEntriesPattern, cancellationToken);
            _logger.LogInformation(
                "Cache INVALIDATION: recent entries pattern '{Pattern}' after creating {Count} entries",
                recentEntriesPattern,
                createdEntries.Count()
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate entry caches");
        }

        // Broadcast create events for each entry (replaces legacy ctx.bus.emit('storage-socket-create'))
        foreach (var entry in createdEntries)
        {
            try
            {
                await _broadcastService.BroadcastStorageCreateAsync(
                    CollectionName,
                    new { colName = CollectionName, doc = entry }
                );
                _logger.LogDebug("Broadcasted storage create event for entry {EntryId}", entry.Id);
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to broadcast storage create event for entry {EntryId}",
                    entry.Id
                );
            }
        }

        // Broadcast data update for real-time glucose updates (replaces legacy ctx.bus.emit('data-update'))
        try
        {
            await _broadcastService.BroadcastDataUpdateAsync(createdEntries.ToArray());
            _logger.LogDebug("Broadcasted data update for {Count} entries", createdEntries.Count());
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to broadcast data update for {Count} entries",
                createdEntries.Count()
            );
        }

        return createdEntries;
    }

    /// <inheritdoc />
    public async Task<Entry?> UpdateEntryAsync(
        string id,
        Entry entry,
        CancellationToken cancellationToken = default
    )
    {
        var updatedEntry = await _postgreSqlService.UpdateEntryAsync(id, entry, cancellationToken);

        if (updatedEntry != null)
        {
            // Invalidate current entry cache since an entry was updated
            try
            {
                await _cacheService.RemoveAsync("entries:current", cancellationToken);
                _logger.LogInformation(
                    "Cache INVALIDATION: entries:current after updating entry {EntryId}",
                    id
                );

                // Invalidate all recent entries caches using pattern matching
                var recentEntriesPattern = CacheKeyBuilder.BuildRecentEntriesPattern(
                    DefaultTenantId
                );
                await _cacheService.RemoveByPatternAsync(recentEntriesPattern, cancellationToken);
                _logger.LogInformation(
                    "Cache INVALIDATION: recent entries pattern '{Pattern}' after updating entry {EntryId}",
                    recentEntriesPattern,
                    id
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate entry caches");
            }

            try
            {
                await _broadcastService.BroadcastStorageUpdateAsync(
                    CollectionName,
                    new { colName = CollectionName, doc = updatedEntry }
                );
                _logger.LogDebug(
                    "Broadcasted storage update event for entry {EntryId}",
                    updatedEntry.Id
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to broadcast storage update event for entry {EntryId}",
                    updatedEntry.Id
                );
            }
        }

        return updatedEntry;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteEntryAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        // Get the entry before deleting for broadcasting
        var entryToDelete = await _postgreSqlService.GetEntryByIdAsync(id, cancellationToken);

        var deleted = await _postgreSqlService.DeleteEntryAsync(id, cancellationToken);

        if (deleted)
        {
            // Invalidate current entry cache since an entry was deleted
            try
            {
                await _cacheService.RemoveAsync("entries:current", cancellationToken);
                _logger.LogInformation(
                    "Cache INVALIDATION: entries:current after deleting entry {EntryId}",
                    id
                );

                // Invalidate all recent entries caches using pattern matching
                var recentEntriesPattern = CacheKeyBuilder.BuildRecentEntriesPattern(
                    DefaultTenantId
                );
                await _cacheService.RemoveByPatternAsync(recentEntriesPattern, cancellationToken);
                _logger.LogInformation(
                    "Cache INVALIDATION: recent entries pattern '{Pattern}' after deleting entry {EntryId}",
                    recentEntriesPattern,
                    id
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate entry caches");
            }

            if (entryToDelete != null)
            {
                try
                {
                    await _broadcastService.BroadcastStorageDeleteAsync(
                        CollectionName,
                        new { colName = CollectionName, doc = entryToDelete }
                    );
                    _logger.LogDebug(
                        "Broadcasted storage delete event for entry {EntryId}",
                        entryToDelete.Id
                    );
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Failed to broadcast storage delete event for entry {EntryId}",
                        entryToDelete.Id
                    );
                }
            }
        }

        return deleted;
    }

    /// <inheritdoc />
    public async Task<long> DeleteEntriesAsync(
        string? find = null,
        CancellationToken cancellationToken = default
    )
    {
        // For bulk operations, we'd need to get the entries first if we want to broadcast individual delete events
        // For now, just delete without individual broadcasting (matches current controller behavior)
        var deletedCount = await _postgreSqlService.BulkDeleteEntriesAsync(
            find ?? "{}",
            cancellationToken
        );

        if (deletedCount > 0)
        {
            // Invalidate current entry cache since entries were deleted
            try
            {
                await _cacheService.RemoveAsync("entries:current", cancellationToken);
                _logger.LogDebug(
                    "Invalidated current entry cache after bulk deleting {Count} entries",
                    deletedCount
                );

                // Invalidate all recent entries caches using pattern matching
                var recentEntriesPattern = CacheKeyBuilder.BuildRecentEntriesPattern(
                    DefaultTenantId
                );
                await _cacheService.RemoveByPatternAsync(recentEntriesPattern, cancellationToken);
                _logger.LogDebug(
                    "Invalidated recent entries pattern '{Pattern}' after bulk deleting {Count} entries",
                    recentEntriesPattern,
                    deletedCount
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate entry caches");
            }
        }

        return deletedCount;
    }

    /// <inheritdoc />
    public async Task<Entry?> GetCurrentEntryAsync(CancellationToken cancellationToken = default)
    {
        const string cacheKey = "entries:current";
        var cacheTtl = TimeSpan.FromSeconds(CacheConstants.Defaults.CurrentEntryExpirationSeconds);

        var cachedEntry = await _cacheService.GetAsync<Entry>(cacheKey, cancellationToken);
        if (cachedEntry != null)
        {
            _logger.LogDebug("Cache HIT for current entry");
            return cachedEntry;
        }

        _logger.LogDebug("Cache MISS for current entry, fetching from database");
        var entry = await _postgreSqlService.GetCurrentEntryAsync(cancellationToken);

        if (entry != null)
        {
            await _cacheService.SetAsync(cacheKey, entry, cacheTtl, cancellationToken);
            _logger.LogDebug("Cached current entry with {TTL}s TTL", cacheTtl.TotalSeconds);
        }

        return entry;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Entry>> GetEntriesWithAdvancedFilterAsync(
        string find,
        int count,
        int skip,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetEntriesWithAdvancedFilterAsync(
            null,
            count,
            skip,
            find,
            null,
            false,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Entry>> GetEntriesWithAdvancedFilterAsync(
        string? type,
        int count,
        int skip,
        string? findQuery,
        string? dateString,
        bool reverseResults,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetEntriesWithAdvancedFilterAsync(
            type,
            count,
            skip,
            findQuery,
            dateString,
            reverseResults,
            cancellationToken
        );
    }
}
