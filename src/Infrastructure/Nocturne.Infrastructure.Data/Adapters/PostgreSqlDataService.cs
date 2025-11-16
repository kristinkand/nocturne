using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Abstractions;

namespace Nocturne.Infrastructure.Data.Adapters;

/// <summary>
/// Minimal PostgreSQL-based implementation of IDataService
/// Implements core Entry and Treatment operations, with other operations marked as not implemented
/// This allows gradual migration from MongoDB to PostgreSQL
/// </summary>
public class PostgreSqlDataService : IDataService
{
    private readonly IPostgreSqlService _postgreSqlService;
    private readonly ILogger<PostgreSqlDataService> _logger;

    /// <summary>
    /// Initializes a new instance of the PostgreSqlDataService class
    /// </summary>
    /// <param name="postgreSqlService">The PostgreSQL service to wrap</param>
    /// <param name="logger">Logger instance for this adapter</param>
    public PostgreSqlDataService(
        IPostgreSqlService postgreSqlService,
        ILogger<PostgreSqlDataService> logger
    )
    {
        _postgreSqlService = postgreSqlService;
        _logger = logger;
    }

    #region Entry Operations - IMPLEMENTED ✅

    /// <inheritdoc />
    public async Task<Entry?> GetCurrentEntryAsync(CancellationToken cancellationToken = default)
    {
        return await _postgreSqlService.GetCurrentEntryAsync(cancellationToken);
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
    public async Task<IEnumerable<Entry>> GetEntriesAsync(
        string? type = null,
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetEntriesAsync(type, count, skip, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Entry>> GetEntriesWithAdvancedFilterAsync(
        string? type = null,
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        string? dateString = null,
        bool reverseResults = false,
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

    /// <inheritdoc />
    public async Task<IEnumerable<Entry>> CreateEntriesAsync(
        IEnumerable<Entry> entries,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CreateEntriesAsync(entries, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Entry?> UpdateEntryAsync(
        string id,
        Entry entry,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.UpdateEntryAsync(id, entry, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteEntryAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.DeleteEntryAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> BulkDeleteEntriesAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.BulkDeleteEntriesAsync(findQuery, cancellationToken);
    }

    #endregion

    #region Treatment Operations - IMPLEMENTED ✅

    /// <inheritdoc />
    public async Task<Treatment?> GetTreatmentByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetTreatmentByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Treatment>> GetTreatmentsAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetTreatmentsAsync(count, skip, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Treatment>> GetTreatmentsWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetTreatmentsWithAdvancedFilterAsync(
            count,
            skip,
            findQuery,
            reverseResults,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<Treatment?> CreateTreatmentAsync(
        Treatment treatment,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CreateTreatmentAsync(treatment, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Treatment>> CreateTreatmentsAsync(
        IEnumerable<Treatment> treatments,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CreateTreatmentsAsync(treatments, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Treatment?> UpdateTreatmentAsync(
        string id,
        Treatment treatment,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.UpdateTreatmentAsync(id, treatment, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteTreatmentAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.DeleteTreatmentAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> BulkDeleteTreatmentsAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.BulkDeleteTreatmentsAsync(findQuery, cancellationToken);
    }

    #endregion

    #region Connection Testing - IMPLEMENTED ✅

    /// <inheritdoc />
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        return await _postgreSqlService.TestConnectionAsync(cancellationToken);
    }

    #endregion

    #region Profile Operations - IMPLEMENTED ✅

    /// <inheritdoc />
    public async Task<Profile?> GetCurrentProfileAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetCurrentProfileAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Profile?> GetProfileByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetProfileByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Profile>> GetProfilesAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetProfilesAsync(count, skip, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Profile>> GetProfilesWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetProfilesWithAdvancedFilterAsync(
            count,
            skip,
            findQuery,
            reverseResults,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Profile>> CreateProfilesAsync(
        IEnumerable<Profile> profiles,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CreateProfilesAsync(profiles, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Profile?> UpdateProfileAsync(
        string id,
        Profile profile,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.UpdateProfileAsync(id, profile, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteProfileAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.DeleteProfileAsync(id, cancellationToken);
    }

    #endregion

    #region DeviceStatus Operations - NOT YET IMPLEMENTED ⚠️

    /// <inheritdoc />
    public async Task<IEnumerable<DeviceStatus>> GetDeviceStatusAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetDeviceStatusAsync(count, skip, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DeviceStatus?> GetDeviceStatusByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetDeviceStatusByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DeviceStatus>> GetDeviceStatusWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetDeviceStatusWithAdvancedFilterAsync(
            count,
            skip,
            findQuery,
            reverseResults,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<DeviceStatus>> CreateDeviceStatusAsync(
        IEnumerable<DeviceStatus> deviceStatuses,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CreateDeviceStatusAsync(deviceStatuses, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DeviceStatus?> UpdateDeviceStatusAsync(
        string id,
        DeviceStatus deviceStatus,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.UpdateDeviceStatusAsync(
            id,
            deviceStatus,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<bool> DeleteDeviceStatusAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.DeleteDeviceStatusAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> BulkDeleteDeviceStatusAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.BulkDeleteDeviceStatusAsync(findQuery, cancellationToken);
    }

    #endregion

    #region Food, Activity, Settings Operations

    // Food and Activity operations delegate to PostgreSQL service
    // Settings operations are fully implemented ✅

    /// <inheritdoc />
    public async Task<IEnumerable<Food>> GetFoodAsync(CancellationToken cancellationToken = default)
    {
        return await _postgreSqlService.GetFoodAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Food?> GetFoodByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetFoodByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Food>> GetFoodByTypeAsync(
        string type,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetFoodByTypeAsync(type, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Food>> GetFoodWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetFoodWithAdvancedFilterAsync(
            count,
            skip,
            findQuery,
            reverseResults,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Food>> GetFoodWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        string? type = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetFoodWithAdvancedFilterAsync(
            count,
            skip,
            findQuery,
            type,
            reverseResults,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Food>> CreateFoodAsync(
        IEnumerable<Food> foods,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CreateFoodAsync(foods, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Food?> UpdateFoodAsync(
        string id,
        Food food,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.UpdateFoodAsync(id, food, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFoodAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.DeleteFoodAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> BulkDeleteFoodAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.BulkDeleteFoodAsync(findQuery, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountFoodAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CountFoodAsync(findQuery, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountFoodAsync(
        string? findQuery = null,
        string? type = null,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CountFoodAsync(findQuery, type, cancellationToken);
    }

    // Activity operations
    /// <inheritdoc />
    public async Task<IEnumerable<Activity>> GetActivityAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetActivityAsync(count, skip, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Activity>> GetActivitiesAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetActivitiesAsync(count, skip, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Activity?> GetActivityByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetActivityByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Activity>> GetActivityWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetActivityWithAdvancedFilterAsync(
            count,
            skip,
            findQuery,
            reverseResults,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Activity>> CreateActivityAsync(
        IEnumerable<Activity> activities,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CreateActivityAsync(activities, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Activity>> CreateActivitiesAsync(
        IEnumerable<Activity> activities,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CreateActivitiesAsync(activities, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Activity?> UpdateActivityAsync(
        string id,
        Activity activity,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.UpdateActivityAsync(id, activity, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteActivityAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.DeleteActivityAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountActivitiesAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CountActivitiesAsync(findQuery, cancellationToken);
    }

    // Settings operations
    /// <inheritdoc />
    public async Task<IEnumerable<Settings>> GetSettingsAsync(
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetSettingsAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Settings>> GetSettingsWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetSettingsWithAdvancedFilterAsync(
            count,
            skip,
            findQuery,
            reverseResults,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<Settings?> GetSettingsByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetSettingsByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Settings?> GetSettingsByKeyAsync(
        string key,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.GetSettingsByKeyAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Settings>> CreateSettingsAsync(
        IEnumerable<Settings> settings,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CreateSettingsAsync(settings, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Settings?> UpdateSettingsAsync(
        string id,
        Settings settings,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.UpdateSettingsAsync(id, settings, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSettingsAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.DeleteSettingsAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> BulkDeleteSettingsAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.BulkDeleteSettingsAsync(findQuery, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountSettingsAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CountSettingsAsync(findQuery, cancellationToken);
    }

    // Count operations
    /// <inheritdoc />
    public async Task<long> CountEntriesAsync(
        string? findQuery = null,
        string? type = null,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CountEntriesAsync(findQuery, type, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountTreatmentsAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CountTreatmentsAsync(findQuery, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountDeviceStatusAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CountDeviceStatusAsync(findQuery, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountProfilesAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        return await _postgreSqlService.CountProfilesAsync(findQuery, cancellationToken);
    }

    #endregion
}
