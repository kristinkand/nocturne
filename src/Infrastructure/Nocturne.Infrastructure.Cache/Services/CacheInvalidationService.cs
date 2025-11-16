using Microsoft.Extensions.Logging;
using Nocturne.Infrastructure.Cache.Abstractions;
using Nocturne.Infrastructure.Cache.Keys;
using Nocturne.Infrastructure.Cache.Services;

namespace Nocturne.Infrastructure.Cache.Services;

/// <summary>
/// Service for managing complex cache invalidation chains for Phase 3 calculations
/// Implements the invalidation patterns specified in the issue requirements
/// </summary>
public interface ICacheInvalidationService
{
    /// <summary>
    /// Invalidate cache when new insulin treatment is added
    /// Invalidates: treatments:recent:*, calculations:iob:*, stats:*
    /// </summary>
    Task InvalidateForNewInsulinTreatmentAsync(
        string userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Invalidate cache when new carb treatment is added
    /// Invalidates: treatments:recent:*, calculations:cob:*, stats:*
    /// </summary>
    Task InvalidateForNewCarbTreatmentAsync(
        string userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Invalidate cache when new glucose entry is added
    /// Invalidates: entries:current, entries:recent:*, stats:glucose:*, stats:tir:*, stats:hba1c:*
    /// </summary>
    Task InvalidateForNewGlucoseEntryAsync(
        string userId,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Invalidate cache when profile changes
    /// Invalidates: profiles:*, calculations:iob:*, calculations:cob:*
    /// </summary>
    Task InvalidateForProfileChangeAsync(
        string userId,
        string? profileId = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Invalidate all calculation caches for a user (nuclear option)
    /// </summary>
    Task InvalidateAllCalculationsAsync(
        string userId,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Implementation of cache invalidation service for Phase 3 calculation chains
/// </summary>
public class CacheInvalidationService : ICacheInvalidationService
{
    private readonly ICacheService _cacheService;
    private readonly ILogger<CacheInvalidationService> _logger;

    public CacheInvalidationService(
        ICacheService cacheService,
        ILogger<CacheInvalidationService> logger
    )
    {
        _cacheService = cacheService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task InvalidateForNewInsulinTreatmentAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                "Starting cache invalidation for new insulin treatment, user: {UserId}",
                userId
            );

            // Invalidation chain for new insulin treatment:
            // - treatments:recent:* (recent treatments cache)
            // - calculations:iob:* (all IOB calculations)
            // - stats:* (potentially affected statistics)

            var invalidationTasks = new List<Task>
            {
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildRecentTreatmentsPattern(userId),
                    cancellationToken
                ),
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildIobCalculationPattern(userId),
                    cancellationToken
                ),
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildStatsPattern(userId),
                    cancellationToken
                ),
            };

            await Task.WhenAll(invalidationTasks);

            _logger.LogInformation(
                "Completed cache invalidation for new insulin treatment, user: {UserId}",
                userId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error invalidating cache for new insulin treatment, user: {UserId}",
                userId
            );
            throw;
        }
    }

    /// <inheritdoc />
    public async Task InvalidateForNewCarbTreatmentAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                "Starting cache invalidation for new carb treatment, user: {UserId}",
                userId
            );

            // Invalidation chain for new carb treatment:
            // - treatments:recent:* (recent treatments cache)
            // - calculations:cob:* (all COB calculations)
            // - stats:* (potentially affected statistics)

            var invalidationTasks = new List<Task>
            {
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildRecentTreatmentsPattern(userId),
                    cancellationToken
                ),
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildCobCalculationPattern(userId),
                    cancellationToken
                ),
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildStatsPattern(userId),
                    cancellationToken
                ),
            };

            await Task.WhenAll(invalidationTasks);

            _logger.LogInformation(
                "Completed cache invalidation for new carb treatment, user: {UserId}",
                userId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error invalidating cache for new carb treatment, user: {UserId}",
                userId
            );
            throw;
        }
    }

    /// <inheritdoc />
    public async Task InvalidateForNewGlucoseEntryAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                "Starting cache invalidation for new glucose entry, user: {UserId}",
                userId
            );

            // Invalidation chain for new glucose entry:
            // - entries:current (current entries cache)
            // - entries:recent:* (recent entries cache)
            // - stats:glucose:* (glucose statistics)
            // - stats:tir:* (time in range statistics)
            // - stats:hba1c:* (HbA1c estimates)

            var invalidationTasks = new List<Task>
            {
                _cacheService.RemoveAsync(
                    CacheKeyBuilder.BuildCurrentEntriesKey(userId),
                    cancellationToken
                ),
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildRecentEntriesPattern(userId),
                    cancellationToken
                ),
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildPattern("stats", userId, "glucose:*"),
                    cancellationToken
                ),
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildPattern("stats", userId, "tir:*"),
                    cancellationToken
                ),
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildPattern("stats", userId, "hba1c:*"),
                    cancellationToken
                ),
            };

            await Task.WhenAll(invalidationTasks);

            _logger.LogInformation(
                "Completed cache invalidation for new glucose entry, user: {UserId}",
                userId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error invalidating cache for new glucose entry, user: {UserId}",
                userId
            );
            throw;
        }
    }

    /// <inheritdoc />
    public async Task InvalidateForProfileChangeAsync(
        string userId,
        string? profileId = null,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogDebug(
                "Starting cache invalidation for profile change, user: {UserId}, profileId: {ProfileId}",
                userId,
                profileId
            );

            // Invalidation chain for profile change:
            // - profiles:* (all profile caches)
            // - calculations:iob:* (basal rates affect IOB)
            // - calculations:cob:* (carb ratios affect COB)

            var invalidationTasks = new List<Task>
            {
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildPattern("profiles", userId, "*"),
                    cancellationToken
                ),
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildIobCalculationPattern(userId),
                    cancellationToken
                ),
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildCobCalculationPattern(userId),
                    cancellationToken
                ),
            };

            // If specific profile ID is provided, also invalidate profile-specific calculated cache
            if (!string.IsNullOrEmpty(profileId))
            {
                invalidationTasks.Add(
                    _cacheService.RemoveByPatternAsync(
                        CacheKeyBuilder.BuildProfileCalculatedPattern(profileId),
                        cancellationToken
                    )
                );
            }

            await Task.WhenAll(invalidationTasks);

            _logger.LogInformation(
                "Completed cache invalidation for profile change, user: {UserId}, profileId: {ProfileId}",
                userId,
                profileId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error invalidating cache for profile change, user: {UserId}, profileId: {ProfileId}",
                userId,
                profileId
            );
            throw;
        }
    }

    /// <inheritdoc />
    public async Task InvalidateAllCalculationsAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        try
        {
            _logger.LogWarning(
                "Starting nuclear cache invalidation for all calculations, user: {UserId}",
                userId
            );

            // Nuclear option: invalidate all caches for user
            var invalidationTasks = new List<Task>
            {
                // Entries and treatments
                _cacheService.RemoveAsync(
                    CacheKeyBuilder.BuildCurrentEntriesKey(userId),
                    cancellationToken
                ),
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildRecentEntriesPattern(userId),
                    cancellationToken
                ),
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildRecentTreatmentsPattern(userId),
                    cancellationToken
                ),
                // Profiles
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildPattern("profiles", userId, "*"),
                    cancellationToken
                ),
                // Calculations
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildIobCalculationPattern(userId),
                    cancellationToken
                ),
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildCobCalculationPattern(userId),
                    cancellationToken
                ),
                // Statistics
                _cacheService.RemoveByPatternAsync(
                    CacheKeyBuilder.BuildStatsPattern(userId),
                    cancellationToken
                ),
            };

            await Task.WhenAll(invalidationTasks);

            _logger.LogWarning(
                "Completed nuclear cache invalidation for all calculations, user: {UserId}",
                userId
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during nuclear cache invalidation, user: {UserId}", userId);
            throw;
        }
    }
}
