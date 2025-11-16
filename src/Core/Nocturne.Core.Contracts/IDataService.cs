using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts;

/// <summary>
/// Interface for database operations - abstracted from specific database implementation
/// </summary>
public interface IDataService
{
    /// <param name="cancellationToken">Cancellation token</param>
    /// <summary>
    /// Count activity records matching specific criteria
    /// </summary>
    /// <returns>The most recent entry, or null if no entries exist</returns>
    Task<Entry?> GetCurrentEntryAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific entry by ID
    /// </summary>
    /// <param name="id">The entry ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The entry with the specified ID, or null if not found</returns>
    Task<Entry?> GetEntryByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get entries with optional filtering and pagination
    /// </summary>
    /// <param name="type">Entry type filter (e.g., "sgv", "mbg", "cal")</param>
    /// <param name="count">Maximum number of entries to return</param>
    /// <param name="skip">Number of entries to skip</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of entries matching the criteria</returns>
    Task<IEnumerable<Entry>> GetEntriesAsync(
        string? type = null,
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get entries with advanced filtering support including find queries, date filtering, and reverse ordering
    /// </summary>
    /// <param name="type">Entry type filter (e.g., "sgv", "mbg", "cal")</param>
    /// <param name="count">Maximum number of entries to return</param>
    /// <param name="skip">Number of entries to skip</param>
    /// <param name="findQuery">MongoDB-style find query filters (e.g., "find[sgv][$gte]=100")</param>
    /// <param name="dateString">ISO date string for date filtering</param>
    /// <param name="reverseResults">If true, return results in reverse chronological order (oldest first)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of entries matching the criteria</returns>
    Task<IEnumerable<Entry>> GetEntriesWithAdvancedFilterAsync(
        string? type = null,
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        string? dateString = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Test the database connection
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if connection is successful</returns>
    Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current active profile
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The current active profile, or null if no profiles exist</returns>
    Task<Profile?> GetCurrentProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a specific profile by ID
    /// </summary>
    /// <param name="id">The profile ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The profile with the specified ID, or null if not found</returns>
    Task<Profile?> GetProfileByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get profiles with optional pagination
    /// </summary>
    /// <param name="count">Maximum number of profiles to return</param>
    /// <param name="skip">Number of profiles to skip</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of profiles matching the criteria</returns>
    Task<IEnumerable<Profile>> GetProfilesAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get profiles with advanced filtering support including find queries and reverse ordering
    /// </summary>
    /// <param name="count">Maximum number of profiles to return</param>
    /// <param name="skip">Number of profiles to skip</param>
    /// <param name="findQuery">MongoDB-style find query filters</param>
    /// <param name="reverseResults">If true, return results in reverse chronological order (oldest first)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of profiles matching the criteria</returns>
    Task<IEnumerable<Profile>> GetProfilesWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Create new profiles
    /// </summary>
    /// <param name="profiles">Profiles to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created profiles with assigned IDs</returns>
    Task<IEnumerable<Profile>> CreateProfilesAsync(
        IEnumerable<Profile> profiles,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update an existing profile by ID
    /// </summary>
    /// <param name="id">Profile ID to update</param>
    /// <param name="profile">Updated profile data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated profile, or null if not found</returns>
    Task<Profile?> UpdateProfileAsync(
        string id,
        Profile profile,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete a profile by ID
    /// </summary>
    /// <param name="id">Profile ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if profile was deleted, false if not found</returns>
    Task<bool> DeleteProfileAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new entries
    /// </summary>
    /// <param name="entries">Entries to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created entries with assigned IDs</returns>
    Task<IEnumerable<Entry>> CreateEntriesAsync(
        IEnumerable<Entry> entries,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update an existing entry by ID
    /// </summary>
    /// <param name="id">Entry ID to update</param>
    /// <param name="entry">Updated entry data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated entry, or null if not found</returns>
    Task<Entry?> UpdateEntryAsync(
        string id,
        Entry entry,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete an entry by ID
    /// </summary>
    /// <param name="id">Entry ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if entry was deleted, false if not found</returns>
    Task<bool> DeleteEntryAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk delete entries using MongoDB-style query filters
    /// </summary>
    /// <param name="findQuery">JSON query string for filtering entries to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entries deleted</returns>
    Task<long> BulkDeleteEntriesAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    );

    // Treatment operations

    /// <summary>
    /// Get a specific treatment by ID
    /// </summary>
    /// <param name="id">The treatment ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The treatment with the specified ID, or null if not found</returns>
    Task<Treatment?> GetTreatmentByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get treatments with optional filtering and pagination
    /// </summary>
    /// <param name="count">Maximum number of treatments to return</param>
    /// <param name="skip">Number of treatments to skip</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of treatments matching the criteria</returns>
    Task<IEnumerable<Treatment>> GetTreatmentsAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get treatments with advanced filtering support
    /// </summary>
    /// <param name="count">Maximum number of treatments to return</param>
    /// <param name="skip">Number of treatments to skip</param>
    /// <param name="findQuery">MongoDB-style find query filters</param>
    /// <param name="reverseResults">If true, return results in reverse chronological order</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of treatments matching the criteria</returns>
    Task<IEnumerable<Treatment>> GetTreatmentsWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Create a single treatment
    /// </summary>
    /// <param name="treatment">Treatment to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created treatment with assigned ID</returns>
    Task<Treatment?> CreateTreatmentAsync(
        Treatment treatment,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Create new treatments
    /// </summary>
    /// <param name="treatments">Treatments to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created treatments with assigned IDs</returns>
    Task<IEnumerable<Treatment>> CreateTreatmentsAsync(
        IEnumerable<Treatment> treatments,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update an existing treatment by ID
    /// </summary>
    /// <param name="id">Treatment ID to update</param>
    /// <param name="treatment">Updated treatment data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated treatment, or null if not found</returns>
    Task<Treatment?> UpdateTreatmentAsync(
        string id,
        Treatment treatment,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete a treatment by ID
    /// </summary>
    /// <param name="id">Treatment ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if treatment was deleted, false if not found</returns>
    Task<bool> DeleteTreatmentAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk delete treatments using MongoDB-style query filters
    /// </summary>
    /// <param name="findQuery">JSON query string for filtering treatments to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of treatments deleted</returns>
    Task<long> BulkDeleteTreatmentsAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    );

    // DeviceStatus operations

    /// <summary>
    /// Get device status entries with optional filtering and pagination
    /// </summary>
    /// <param name="count">Maximum number of device status entries to return</param>
    /// <param name="skip">Number of device status entries to skip</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of device status entries matching the criteria</returns>
    Task<IEnumerable<DeviceStatus>> GetDeviceStatusAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Create new device status entries
    /// </summary>
    /// <param name="deviceStatusEntries">Device status entries to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created device status entries with assigned IDs</returns>
    Task<IEnumerable<DeviceStatus>> CreateDeviceStatusAsync(
        IEnumerable<DeviceStatus> deviceStatusEntries,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete a device status entry by ID
    /// </summary>
    /// <param name="id">Device status ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if device status was deleted, false if not found</returns>
    Task<bool> DeleteDeviceStatusAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk delete device status entries using MongoDB-style query filters
    /// </summary>
    /// <param name="findQuery">JSON query string for filtering device status entries to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of device status entries deleted</returns>
    Task<long> BulkDeleteDeviceStatusAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get device status entries with advanced filtering support including find queries, date filtering, and reverse ordering
    /// </summary>
    /// <param name="count">Maximum number of device status entries to return</param>
    /// <param name="skip">Number of device status entries to skip</param>
    /// <param name="findQuery">MongoDB-style find query filters (e.g., "find[device]=pump")</param>
    /// <param name="reverseResults">If true, return results in reverse chronological order (oldest first)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of device status entries matching the criteria</returns>
    Task<IEnumerable<DeviceStatus>> GetDeviceStatusWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get a specific device status entry by ID
    /// </summary>
    /// <param name="id">The device status entry ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The device status entry with the specified ID, or null if not found</returns>
    Task<DeviceStatus?> GetDeviceStatusByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update an existing device status entry by ID
    /// </summary>
    /// <param name="id">Device status entry ID to update</param>
    /// <param name="deviceStatus">Updated device status data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated device status entry, or null if not found</returns>
    Task<DeviceStatus?> UpdateDeviceStatusAsync(
        string id,
        DeviceStatus deviceStatus,
        CancellationToken cancellationToken = default
    );

    // Food operations

    /// <summary>
    /// Get all food records with optional filtering
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all food records (both food and quickpick types)</returns>
    Task<IEnumerable<Food>> GetFoodAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get food records of specific type with optional filtering
    /// </summary>
    /// <param name="type">Type filter ("food" or "quickpick")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of food records matching the type filter</returns>
    Task<IEnumerable<Food>> GetFoodByTypeAsync(
        string type,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get a specific food record by ID
    /// </summary>
    /// <param name="id">The food record ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The food record with the specified ID, or null if not found</returns>
    Task<Food?> GetFoodByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new food records
    /// </summary>
    /// <param name="foods">Food records to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created food records with assigned IDs</returns>
    Task<IEnumerable<Food>> CreateFoodAsync(
        IEnumerable<Food> foods,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update an existing food record by ID
    /// </summary>
    /// <param name="id">Food record ID to update</param>
    /// <param name="food">Updated food data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated food record, or null if not found</returns>
    Task<Food?> UpdateFoodAsync(
        string id,
        Food food,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete a food record by ID
    /// </summary>
    /// <param name="id">Food record ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if food record was deleted, false if not found</returns>
    Task<bool> DeleteFoodAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get food records with advanced filtering support including find queries and reverse ordering
    /// </summary>
    /// <param name="count">Maximum number of food records to return</param>
    /// <param name="skip">Number of food records to skip</param>
    /// <param name="findQuery">MongoDB-style find query filters</param>
    /// <param name="type">Type filter ("food" or "quickpick")</param>
    /// <param name="reverseResults">If true, return results in reverse chronological order (oldest first)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of food records matching the criteria</returns>
    Task<IEnumerable<Food>> GetFoodWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        string? type = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Bulk delete food records using MongoDB-style query filters
    /// </summary>
    /// <param name="findQuery">JSON query string for filtering food records to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of food records deleted</returns>
    Task<long> BulkDeleteFoodAsync(string findQuery, CancellationToken cancellationToken = default);

    // Count operations

    /// <summary>
    /// Count entries matching specific criteria
    /// </summary>
    /// <param name="findQuery">MongoDB-style find query filters</param>
    /// <param name="type">Entry type filter (sgv, mbg, cal)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of entries matching the criteria</returns>
    Task<long> CountEntriesAsync(
        string? findQuery = null,
        string? type = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Count treatments matching specific criteria
    /// </summary>
    /// <param name="findQuery">MongoDB-style find query filters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of treatments matching the criteria</returns>
    Task<long> CountTreatmentsAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Count device status entries matching specific criteria
    /// </summary>
    /// <param name="findQuery">MongoDB-style find query filters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of device status entries matching the criteria</returns>
    Task<long> CountDeviceStatusAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Count profile records matching specific criteria
    /// </summary>
    /// <param name="findQuery">MongoDB-style find query filters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of profile records matching the criteria</returns>
    Task<long> CountProfilesAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Count food records matching specific criteria
    /// </summary>
    /// <param name="findQuery">MongoDB-style find query filters</param>
    /// <param name="type">Food type filter ("food" or "quickpick")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of food records matching the criteria</returns>
    Task<long> CountFoodAsync(
        string? findQuery = null,
        string? type = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    // Activity operations

    /// <summary>
    /// Get a specific activity by ID
    /// </summary>
    /// <param name="id">The activity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The activity with the specified ID, or null if not found</returns>
    Task<Activity?> GetActivityByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get activities with optional filtering and pagination
    /// </summary>
    /// <param name="count">Maximum number of activities to return</param>
    /// <param name="skip">Number of activities to skip</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of activities matching the criteria</returns>
    Task<IEnumerable<Activity>> GetActivitiesAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Create new activities
    /// </summary>
    /// <param name="activities">Activities to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created activities with assigned IDs</returns>
    Task<IEnumerable<Activity>> CreateActivitiesAsync(
        IEnumerable<Activity> activities,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update an existing activity by ID
    /// </summary>
    /// <param name="id">Activity ID to update</param>
    /// <param name="activity">Updated activity data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated activity, or null if not found</returns>
    Task<Activity?> UpdateActivityAsync(
        string id,
        Activity activity,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete an activity by ID
    /// </summary>
    /// <param name="id">Activity ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if activity was deleted, false if not found</returns>
    Task<bool> DeleteActivityAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Count activity records matching specific criteria
    /// </summary>
    /// <param name="findQuery">MongoDB-style find query filters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of activity records matching the criteria</returns>
    Task<long> CountActivitiesAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    );

    // Settings operations

    /// <summary>
    /// Get all settings records with optional filtering
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of all settings records</returns>
    Task<IEnumerable<Settings>> GetSettingsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get settings records with advanced filtering support
    /// </summary>
    /// <param name="count">Maximum number of settings to return</param>
    /// <param name="skip">Number of settings to skip</param>
    /// <param name="findQuery">MongoDB-style find query filters</param>
    /// <param name="reverseResults">If true, return results in reverse chronological order (oldest first)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of settings matching the criteria</returns>
    Task<IEnumerable<Settings>> GetSettingsWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get a specific settings record by ID
    /// </summary>
    /// <param name="id">The settings record ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The settings record with the specified ID, or null if not found</returns>
    Task<Settings?> GetSettingsByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get a settings record by key
    /// </summary>
    /// <param name="key">The settings key</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The settings record with the specified key, or null if not found</returns>
    Task<Settings?> GetSettingsByKeyAsync(
        string key,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Create new settings records
    /// </summary>
    /// <param name="settings">Settings records to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created settings records with assigned IDs</returns>
    Task<IEnumerable<Settings>> CreateSettingsAsync(
        IEnumerable<Settings> settings,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update an existing settings record by ID
    /// </summary>
    /// <param name="id">Settings record ID to update</param>
    /// <param name="settings">Updated settings data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated settings record, or null if not found</returns>
    Task<Settings?> UpdateSettingsAsync(
        string id,
        Settings settings,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete a settings record by ID
    /// </summary>
    /// <param name="id">Settings record ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if settings record was deleted, false if not found</returns>
    Task<bool> DeleteSettingsAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Bulk delete settings records using MongoDB-style query filters
    /// </summary>
    /// <param name="findQuery">JSON query string for filtering settings records to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of settings records deleted</returns>
    Task<long> BulkDeleteSettingsAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Count settings records matching specific criteria
    /// </summary>
    /// <param name="findQuery">MongoDB-style find query filters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Count of settings records matching the criteria</returns>
    Task<long> CountSettingsAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    );
}
