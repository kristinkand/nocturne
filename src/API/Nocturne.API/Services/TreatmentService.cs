using Nocturne.Infrastructure.Cache.Constants;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Cache.Abstractions;
using Nocturne.Infrastructure.Cache.Configuration;
using Nocturne.Infrastructure.Cache.Keys;
using Nocturne.Infrastructure.Data.Abstractions;

namespace Nocturne.API.Services;

/// <summary>
/// Domain service implementation for treatment operations with WebSocket broadcasting
/// </summary>
public class TreatmentService : ITreatmentService
{
    private readonly IPostgreSqlService _postgreSqlService;
    private readonly ISignalRBroadcastService _broadcastService;
    private readonly ICacheService _cacheService;
    private readonly CacheConfiguration _cacheConfig;
    private readonly ILogger<TreatmentService> _logger;
    private const string CollectionName = "treatments";
    private const string DefaultTenantId = "default"; // TODO: Replace with actual tenant context

    public TreatmentService(
        IPostgreSqlService postgreSqlService,
        ISignalRBroadcastService broadcastService,
        ICacheService cacheService,
        IOptions<CacheConfiguration> cacheConfig,
        ILogger<TreatmentService> logger
    )
    {
        _postgreSqlService = postgreSqlService;
        _broadcastService = broadcastService;
        _cacheService = cacheService;
        _cacheConfig = cacheConfig.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Treatment>> GetTreatmentsAsync(
        string? find = null,
        int? count = null,
        int? skip = null,
        CancellationToken cancellationToken = default
    )
    {
        var actualCount = count ?? 10;
        var actualSkip = skip ?? 0;

        // If find query is provided, use advanced filtering (no caching for filtered queries)
        if (!string.IsNullOrEmpty(find))
        {
            _logger.LogDebug(
                "Using advanced filter for treatments with findQuery: {FindQuery}, count: {Count}, skip: {Skip}",
                find,
                actualCount,
                actualSkip
            );
            return await _postgreSqlService.GetTreatmentsWithAdvancedFilterAsync(
                count: actualCount,
                skip: actualSkip,
                findQuery: find,
                reverseResults: false,
                cancellationToken: cancellationToken
            );
        }

        // Cache recent treatments for common queries (skip = 0 and common counts)
        if (actualSkip == 0 && IsCommonTreatmentCount(actualCount))
        {
            // Determine time range based on common patterns (default to 24 hours for treatments)
            var hours = DetermineTimeRangeHours(actualCount);
            var cacheKey = CacheKeyBuilder.BuildRecentTreatmentsKey(
                DefaultTenantId,
                hours,
                actualCount
            );
            var cacheTtl = TimeSpan.FromSeconds(CacheConstants.Defaults.RecentTreatmentsExpirationSeconds);

            return await _cacheService.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    _logger.LogDebug(
                        "Cache MISS for recent treatments (count: {Count}, hours: {Hours}), fetching from database",
                        actualCount,
                        hours
                    );
                    var treatments = await _postgreSqlService.GetTreatmentsAsync(
                        actualCount,
                        actualSkip,
                        cancellationToken
                    );
                    return treatments.ToList(); // Materialize to avoid multiple enumerations
                },
                cacheTtl,
                cancellationToken
            );
        }

        // Non-cached path for non-standard queries
        return await _postgreSqlService.GetTreatmentsAsync(
            actualCount,
            actualSkip,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Treatment>> GetTreatmentsAsync(
        int count,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        // Cache recent treatments for common queries (skip = 0 and common counts)
        if (skip == 0 && IsCommonTreatmentCount(count))
        {
            // Determine time range based on common patterns (default to 24 hours for treatments)
            var hours = DetermineTimeRangeHours(count);
            var cacheKey = CacheKeyBuilder.BuildRecentTreatmentsKey(DefaultTenantId, hours, count);
            var cacheTtl = TimeSpan.FromSeconds(CacheConstants.Defaults.RecentTreatmentsExpirationSeconds);

            return await _cacheService.GetOrSetAsync(
                cacheKey,
                async () =>
                {
                    _logger.LogDebug(
                        "Cache MISS for recent treatments (count: {Count}, hours: {Hours}), fetching from database",
                        count,
                        hours
                    );
                    var treatments = await _postgreSqlService.GetTreatmentsAsync(
                        count,
                        skip,
                        cancellationToken
                    );
                    return treatments.ToList(); // Materialize to avoid multiple enumerations
                },
                cacheTtl,
                cancellationToken
            );
        }

        // Non-cached path for non-standard queries
        return await _postgreSqlService.GetTreatmentsAsync(count, skip, cancellationToken);
    }

    /// <summary>
    /// Determines if the treatment count is common enough to cache
    /// </summary>
    /// <param name="count">The count to check</param>
    /// <returns>True if the count is common (10, 50, 100), false otherwise</returns>
    private static bool IsCommonTreatmentCount(int count)
    {
        return count is 10 or 50 or 100;
    }

    /// <summary>
    /// Determines the appropriate time range hours based on treatment count
    /// </summary>
    /// <param name="count">The treatment count</param>
    /// <returns>Time range in hours (12, 24, or 48)</returns>
    private static int DetermineTimeRangeHours(int count)
    {
        return count switch
        {
            <= 10 => 12, // 12 hours for small counts
            <= 50 => 24, // 24 hours for medium counts
            _ => 48, // 48 hours for large counts
        };
    }

    /// <inheritdoc />
    public async Task<Treatment?> GetTreatmentByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetTreatmentByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Treatment>> CreateTreatmentsAsync(
        IEnumerable<Treatment> treatments,
        CancellationToken cancellationToken = default
    )
    {
        var createdTreatments = await _postgreSqlService.CreateTreatmentsAsync(
            treatments,
            cancellationToken
        );

        // Invalidate all recent treatments caches since new treatments were created
        try
        {
            var recentTreatmentsPattern = CacheKeyBuilder.BuildRecentTreatmentsPattern(
                DefaultTenantId
            );
            await _cacheService.RemoveByPatternAsync(recentTreatmentsPattern, cancellationToken);
            _logger.LogInformation(
                "Cache INVALIDATION: recent treatments pattern '{Pattern}' after creating {Count} treatments",
                recentTreatmentsPattern,
                createdTreatments.Count()
            );
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to invalidate treatment caches");
        }

        // Broadcast create events for each treatment (replaces legacy ctx.bus.emit('storage-socket-create'))
        foreach (var treatment in createdTreatments)
        {
            try
            {
                await _broadcastService.BroadcastStorageCreateAsync(
                    CollectionName,
                    new { colName = CollectionName, doc = treatment }
                );
                _logger.LogDebug(
                    "Broadcasted storage create event for treatment {TreatmentId}",
                    treatment.Id
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to broadcast storage create event for treatment {TreatmentId}",
                    treatment.Id
                );
            }
        }

        return createdTreatments;
    }

    /// <inheritdoc />
    public async Task<Treatment?> UpdateTreatmentAsync(
        string id,
        Treatment treatment,
        CancellationToken cancellationToken = default
    )
    {
        var updatedTreatment = await _postgreSqlService.UpdateTreatmentAsync(
            id,
            treatment,
            cancellationToken
        );

        if (updatedTreatment != null)
        {
            // Invalidate all recent treatments caches since a treatment was updated
            try
            {
                var recentTreatmentsPattern = CacheKeyBuilder.BuildRecentTreatmentsPattern(
                    DefaultTenantId
                );
                await _cacheService.RemoveByPatternAsync(
                    recentTreatmentsPattern,
                    cancellationToken
                );
                _logger.LogInformation(
                    "Cache INVALIDATION: recent treatments pattern '{Pattern}' after updating treatment {TreatmentId}",
                    recentTreatmentsPattern,
                    id
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate treatment caches");
            }

            try
            {
                await _broadcastService.BroadcastStorageUpdateAsync(
                    CollectionName,
                    new { colName = CollectionName, doc = updatedTreatment }
                );
                _logger.LogDebug(
                    "Broadcasted storage update event for treatment {TreatmentId}",
                    updatedTreatment.Id
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to broadcast storage update event for treatment {TreatmentId}",
                    updatedTreatment.Id
                );
            }
        }

        return updatedTreatment;
    }

    /// <inheritdoc />
    public async Task<bool> DeleteTreatmentAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        // Get the treatment before deleting for broadcasting
        var treatmentToDelete = await _postgreSqlService.GetTreatmentByIdAsync(
            id,
            cancellationToken
        );

        var deleted = await _postgreSqlService.DeleteTreatmentAsync(id, cancellationToken);

        if (deleted && treatmentToDelete != null)
        {
            // Invalidate all recent treatments caches since a treatment was deleted
            try
            {
                var recentTreatmentsPattern = CacheKeyBuilder.BuildRecentTreatmentsPattern(
                    DefaultTenantId
                );
                await _cacheService.RemoveByPatternAsync(
                    recentTreatmentsPattern,
                    cancellationToken
                );
                _logger.LogInformation(
                    "Cache INVALIDATION: recent treatments pattern '{Pattern}' after deleting treatment {TreatmentId}",
                    recentTreatmentsPattern,
                    id
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate treatment caches");
            }

            try
            {
                await _broadcastService.BroadcastStorageDeleteAsync(
                    CollectionName,
                    new { colName = CollectionName, doc = treatmentToDelete }
                );
                _logger.LogDebug(
                    "Broadcasted storage delete event for treatment {TreatmentId}",
                    treatmentToDelete.Id
                );
            }
            catch (Exception ex)
            {
                _logger.LogError(
                    ex,
                    "Failed to broadcast storage delete event for treatment {TreatmentId}",
                    treatmentToDelete.Id
                );
            }
        }

        return deleted;
    }

    /// <inheritdoc />
    public async Task<long> DeleteTreatmentsAsync(
        string? find = null,
        CancellationToken cancellationToken = default
    )
    {
        // For bulk operations, we'd need to get the treatments first if we want to broadcast individual delete events
        // For now, just delete without individual broadcasting (matches current controller behavior)
        var deletedCount = await _postgreSqlService.BulkDeleteTreatmentsAsync(
            find ?? "{}",
            cancellationToken
        );

        if (deletedCount > 0)
        {
            // Invalidate all recent treatments caches since treatments were deleted
            try
            {
                var recentTreatmentsPattern = CacheKeyBuilder.BuildRecentTreatmentsPattern(
                    DefaultTenantId
                );
                await _cacheService.RemoveByPatternAsync(
                    recentTreatmentsPattern,
                    cancellationToken
                );
                _logger.LogDebug(
                    "Invalidated recent treatments pattern '{Pattern}' after bulk deleting {Count} treatments",
                    recentTreatmentsPattern,
                    deletedCount
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to invalidate treatment caches");
            }
        }

        return deletedCount;
    }
}
