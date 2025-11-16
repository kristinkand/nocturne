using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Abstractions;
using Nocturne.Infrastructure.Data.Repositories;

namespace Nocturne.Infrastructure.Data.Services;

/// <summary>
/// PostgreSQL service implementation for database operations
/// Drop-in replacement for MongoDB service
/// </summary>
public class PostgreSqlService : IPostgreSqlService
{
    private readonly NocturneDbContext _context;
    private readonly EntryRepository _entryRepository;
    private readonly TreatmentRepository _treatmentRepository;
    private readonly FoodRepository _foodRepository;
    private readonly SettingsRepository _settingsRepository;
    private readonly DeviceStatusRepository _deviceStatusRepository;
    private readonly ProfileRepository _profileRepository;
    private readonly ActivityRepository _activityRepository;
    private readonly ILogger<PostgreSqlService> _logger;

    /// <summary>
    /// Initializes a new instance of the PostgreSqlService class
    /// </summary>
    /// <param name="context">The database context for PostgreSQL operations</param>
    /// <param name="queryParser">MongoDB query parser for advanced filtering</param>
    /// <param name="logger">Logger instance for this service</param>
    public PostgreSqlService(
        NocturneDbContext context,
        IQueryParser queryParser,
        ILogger<PostgreSqlService> logger
    )
    {
        _context = context;
        _entryRepository = new EntryRepository(context, queryParser);
        _treatmentRepository = new TreatmentRepository(context, queryParser);
        _foodRepository = new FoodRepository(context);
        _settingsRepository = new SettingsRepository(context);
        _deviceStatusRepository = new DeviceStatusRepository(context);
        _profileRepository = new ProfileRepository(context);
        _activityRepository = new ActivityRepository(context);
        _logger = logger;
    }

    #region Entry Operations

    /// <inheritdoc />
    public async Task<Entry?> GetCurrentEntryAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting current entry");
        return await _entryRepository.GetCurrentEntryAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Entry?> GetEntryByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting entry by ID: {Id}", id);
        return await _entryRepository.GetEntryByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Entry>> GetEntriesAsync(
        string? type = null,
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(
            "Getting entries with type: {Type}, count: {Count}, skip: {Skip}",
            type,
            count,
            skip
        );
        return await _entryRepository.GetEntriesAsync(type, count, skip, cancellationToken);
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
        _logger.LogDebug(
            "Getting entries with advanced filter - type: {Type}, count: {Count}, skip: {Skip}, findQuery: {FindQuery}, dateString: {DateString}, reverse: {Reverse}",
            type,
            count,
            skip,
            findQuery,
            dateString,
            reverseResults
        );

        return await _entryRepository.GetEntriesWithAdvancedFilterAsync(
            type,
            count,
            skip,
            findQuery,
            dateString,
            reverseResults,
            cancellationToken
        );
    }

    /// <summary>
    /// Create a single entry
    /// </summary>
    /// <param name="entry">The entry to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created entry, or null if creation failed</returns>
    public async Task<Entry?> CreateEntryAsync(
        Entry entry,
        CancellationToken cancellationToken = default
    )
    {
        var entriesList = new List<Entry> { entry };
        _logger.LogDebug("Creating single entry");
        var createdEntries = await _entryRepository.CreateEntriesAsync(
            entriesList,
            cancellationToken
        );
        return createdEntries.FirstOrDefault();
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
        // Calculate time window in milliseconds
        var windowMs = windowMinutes * 60 * 1000L;
        var windowStart = mills - windowMs;
        var windowEnd = mills + windowMs;

        _logger.LogDebug(
            "Checking for duplicate entry: device={Device}, type={Type}, sgv={Sgv}, mills={Mills}, window={WindowMinutes}min",
            device ?? "null",
            type,
            sgv,
            mills,
            windowMinutes
        );

        // Query database for duplicate using composite index
        var duplicate = await _context
            .Entries.Where(e => e.Device == device)
            .Where(e => e.Type == type)
            .Where(e => e.Sgv == sgv)
            .Where(e => e.Mills >= windowStart && e.Mills <= windowEnd)
            .OrderBy(e => e.Mills) // Use index order
            .Select(e => new Entry
            {
                Id = e.Id.ToString(),
                Mills = e.Mills,
                DateString = e.DateString,
                Mgdl = e.Mgdl,
                Mmol = e.Mmol,
                Sgv = e.Sgv,
                Direction = e.Direction,
                Type = e.Type,
                Device = e.Device,
                Notes = e.Notes,
                Delta = e.Delta,
                SysTime = e.SysTime,
                UtcOffset = e.UtcOffset,
                Noise = e.Noise,
                Filtered = e.Filtered,
                Unfiltered = e.Unfiltered,
                Rssi = e.Rssi,
                Slope = e.Slope,
                Intercept = e.Intercept,
                Scale = e.Scale,
                CreatedAt = e.CreatedAt,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (duplicate != null)
        {
            _logger.LogDebug(
                "Found duplicate entry: id={Id}, mills={Mills}",
                duplicate.Id,
                duplicate.Mills
            );
        }

        return duplicate;
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Entry>> CreateEntriesAsync(
        IEnumerable<Entry> entries,
        CancellationToken cancellationToken = default
    )
    {
        var entriesList = entries.ToList();
        _logger.LogDebug("Creating {Count} entries", entriesList.Count);
        return await _entryRepository.CreateEntriesAsync(entriesList, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Entry?> UpdateEntryAsync(
        string id,
        Entry entry,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Updating entry with ID: {Id}", id);
        return await _entryRepository.UpdateEntryAsync(id, entry, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteEntryAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Deleting entry with ID: {Id}", id);
        return await _entryRepository.DeleteEntryAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> DeleteDemoEntriesAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Deleting all demo entries");
        return await _entryRepository.DeleteDemoEntriesAsync(cancellationToken);
    }

    /// <summary>
    /// Bulk delete entries using MongoDB-style query filters
    /// </summary>
    /// <param name="findQuery">Query filter for entries to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entries deleted</returns>
    public async Task<long> BulkDeleteEntriesAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Bulk deleting entries with query: {FindQuery}", findQuery);
        // For now, treat findQuery as a type filter - this could be expanded later
        return await _entryRepository.DeleteEntriesAsync(findQuery, cancellationToken);
    }

    /// <summary>
    /// Count entries with optional filtering
    /// </summary>
    /// <param name="findQuery">Optional query filter</param>
    /// <param name="type">Optional entry type filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of entries matching the filter</returns>
    public async Task<long> CountEntriesAsync(
        string? findQuery = null,
        string? type = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Counting entries with query: {FindQuery}, type: {Type}", findQuery, type);
        return await _entryRepository.CountEntriesAsync(findQuery, type, cancellationToken);
    }

    /// <summary>
    /// Bulk delete treatments using MongoDB-style query filters
    /// </summary>
    /// <param name="findQuery">Query filter for treatments to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of treatments deleted</returns>
    public async Task<long> BulkDeleteTreatmentsAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Bulk deleting treatments with query: {FindQuery}", findQuery);
        // For now, treat findQuery as an eventType filter - this could be expanded later
        return await _treatmentRepository.DeleteTreatmentsAsync(findQuery, cancellationToken);
    }

    /// <summary>
    /// Count treatments with optional filtering
    /// </summary>
    /// <param name="findQuery">Optional query filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of treatments matching the filter</returns>
    public async Task<long> CountTreatmentsAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Counting treatments with query: {FindQuery}", findQuery);
        return await _treatmentRepository.CountTreatmentsAsync(findQuery, cancellationToken);
    }

    /// <summary>
    /// Get treatments with advanced filtering
    /// </summary>
    /// <param name="count">Maximum number of treatments to return</param>
    /// <param name="skip">Number of treatments to skip</param>
    /// <param name="findQuery">Optional query filter</param>
    /// <param name="reverseResults">Whether to reverse the order of results</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of treatments matching the filter</returns>
    public async Task<IEnumerable<Treatment>> GetTreatmentsWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(
            "Getting treatments with advanced filter - count: {Count}, skip: {Skip}, findQuery: {FindQuery}, reverse: {Reverse}",
            count,
            skip,
            findQuery,
            reverseResults
        );

        return await _treatmentRepository.GetTreatmentsWithAdvancedFilterAsync(
            null,
            count,
            skip,
            findQuery,
            null,
            reverseResults,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<Treatment?> GetTreatmentByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting treatment by ID: {Id}", id);
        return await _treatmentRepository.GetTreatmentByIdAsync(id, cancellationToken);
    }

    /// <summary>
    /// Get treatments with optional filtering and pagination (interface-compatible)
    /// </summary>
    /// <param name="count">Maximum number of treatments to return</param>
    /// <param name="skip">Number of treatments to skip</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of treatments</returns>
    public async Task<IEnumerable<Treatment>> GetTreatmentsAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting treatments with count: {Count}, skip: {Skip}", count, skip);
        return await _treatmentRepository.GetTreatmentsAsync(null, count, skip, cancellationToken);
    }

    /// <summary>
    /// Create a single treatment
    /// </summary>
    /// <param name="treatment">The treatment to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>The created treatment, or null if creation failed</returns>
    public async Task<Treatment?> CreateTreatmentAsync(
        Treatment treatment,
        CancellationToken cancellationToken = default
    )
    {
        var treatmentsList = new List<Treatment> { treatment };
        _logger.LogDebug("Creating single treatment");
        var createdTreatments = await _treatmentRepository.CreateTreatmentsAsync(
            treatmentsList,
            cancellationToken
        );
        return createdTreatments.FirstOrDefault();
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Treatment>> CreateTreatmentsAsync(
        IEnumerable<Treatment> treatments,
        CancellationToken cancellationToken = default
    )
    {
        var treatmentsList = treatments.ToList();
        _logger.LogDebug("Creating {Count} treatments", treatmentsList.Count);
        return await _treatmentRepository.CreateTreatmentsAsync(treatmentsList, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Treatment?> UpdateTreatmentAsync(
        string id,
        Treatment treatment,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Updating treatment with ID: {Id}", id);
        return await _treatmentRepository.UpdateTreatmentAsync(id, treatment, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteTreatmentAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Deleting treatment with ID: {Id}", id);
        return await _treatmentRepository.DeleteTreatmentAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> DeleteTreatmentsAsync(
        string? findFilter = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Deleting treatments with filter: {FindFilter}", findFilter);
        // For now, treat findFilter as an eventType filter - this could be expanded later
        return await _treatmentRepository.DeleteTreatmentsAsync(findFilter, cancellationToken);
    }

    #endregion

    #region Connection Operations

    /// <inheritdoc />
    public async Task<bool> TestConnectionAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogDebug("Testing PostgreSQL database connection");
            await _context.Database.CanConnectAsync(cancellationToken);
            _logger.LogDebug("PostgreSQL database connection successful");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "PostgreSQL database connection failed");
            return false;
        }
    }

    #endregion

    #region Food Operations

    /// <inheritdoc />
    public async Task<IEnumerable<Food>> GetFoodAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug("Getting all food entries");
        return await _foodRepository.GetFoodAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Food?> GetFoodByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting food by ID: {Id}", id);
        return await _foodRepository.GetFoodByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Food>> GetFoodByTypeAsync(
        string type,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting food by type: {Type}", type);
        return await _foodRepository.GetFoodByTypeAsync(type, cancellationToken);
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
        _logger.LogDebug(
            "Getting food with advanced filter - count: {Count}, skip: {Skip}, findQuery: {FindQuery}, reverse: {Reverse}",
            count,
            skip,
            findQuery,
            reverseResults
        );

        return await _foodRepository.GetFoodWithAdvancedFilterAsync(
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
        _logger.LogDebug(
            "Getting food with advanced filter - count: {Count}, skip: {Skip}, findQuery: {FindQuery}, type: {Type}, reverse: {Reverse}",
            count,
            skip,
            findQuery,
            type,
            reverseResults
        );

        return await _foodRepository.GetFoodWithAdvancedFilterAsync(
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
        _logger.LogDebug("Creating {Count} food entries", foods.Count());
        return await _foodRepository.CreateFoodAsync(foods, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Food?> UpdateFoodAsync(
        string id,
        Food food,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Updating food with ID: {Id}", id);
        return await _foodRepository.UpdateFoodAsync(id, food, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteFoodAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Deleting food with ID: {Id}", id);
        return await _foodRepository.DeleteFoodAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> BulkDeleteFoodAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Bulk deleting food with query: {FindQuery}", findQuery);
        return await _foodRepository.BulkDeleteFoodAsync(findQuery, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountFoodAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Counting food entries with query: {FindQuery}", findQuery);
        return await _foodRepository.CountFoodAsync(findQuery, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountFoodAsync(
        string? findQuery = null,
        string? type = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(
            "Counting food entries with query: {FindQuery}, type: {Type}",
            findQuery,
            type
        );
        return await _foodRepository.CountFoodAsync(findQuery, type, cancellationToken);
    }

    #endregion

    #region Activity Operations

    /// <inheritdoc />
    public async Task<IEnumerable<Activity>> GetActivityAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting activities with count: {Count}, skip: {Skip}", count, skip);
        return await _activityRepository.GetActivitiesAsync(null, count, skip, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Activity>> GetActivitiesAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting activities with count: {Count}, skip: {Skip}", count, skip);
        return await _activityRepository.GetActivitiesAsync(null, count, skip, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Activity?> GetActivityByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting activity by ID: {Id}", id);
        return await _activityRepository.GetActivityByIdAsync(id, cancellationToken);
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
        _logger.LogDebug(
            "Getting activities with advanced filter - count: {Count}, skip: {Skip}, query: {Query}, reverse: {Reverse}",
            count,
            skip,
            findQuery,
            reverseResults
        );
        return await _activityRepository.GetActivitiesWithAdvancedFilterAsync(
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
        _logger.LogDebug("Creating activities: {Count}", activities.Count());
        return await _activityRepository.CreateActivitiesAsync(activities, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Activity>> CreateActivitiesAsync(
        IEnumerable<Activity> activities,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Creating activities: {Count}", activities.Count());
        return await _activityRepository.CreateActivitiesAsync(activities, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Activity?> UpdateActivityAsync(
        string id,
        Activity activity,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Updating activity with ID: {Id}", id);
        return await _activityRepository.UpdateActivityAsync(id, activity, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteActivityAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Deleting activity with ID: {Id}", id);
        return await _activityRepository.DeleteActivityAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountActivitiesAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Counting activities with query: {Query}", findQuery);
        return await _activityRepository.CountActivitiesAsync(findQuery, cancellationToken);
    }

    #endregion

    #region Settings Operations

    /// <inheritdoc />
    public async Task<IEnumerable<Settings>> GetSettingsAsync(
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting all settings");
        return await _settingsRepository.GetSettingsAsync(cancellationToken);
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
        _logger.LogDebug(
            "Getting settings with advanced filter: count={Count}, skip={Skip}, query={FindQuery}, reverse={ReverseResults}",
            count,
            skip,
            findQuery,
            reverseResults
        );
        return await _settingsRepository.GetSettingsWithAdvancedFilterAsync(
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
        _logger.LogDebug("Getting settings by ID: {Id}", id);
        return await _settingsRepository.GetSettingsByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Settings?> GetSettingsByKeyAsync(
        string key,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting settings by key: {Key}", key);
        return await _settingsRepository.GetSettingsByKeyAsync(key, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Settings>> CreateSettingsAsync(
        IEnumerable<Settings> settings,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Creating {Count} settings", settings.Count());
        return await _settingsRepository.CreateSettingsAsync(settings, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Settings?> UpdateSettingsAsync(
        string id,
        Settings settings,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Updating settings with ID: {Id}", id);
        return await _settingsRepository.UpdateSettingsAsync(id, settings, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteSettingsAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Deleting settings with ID: {Id}", id);
        return await _settingsRepository.DeleteSettingsAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> BulkDeleteSettingsAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Bulk deleting settings with query: {FindQuery}", findQuery);
        return await _settingsRepository.BulkDeleteSettingsAsync(findQuery, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountSettingsAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Counting settings with query: {FindQuery}", findQuery);
        return await _settingsRepository.CountSettingsAsync(findQuery, cancellationToken);
    }

    #endregion

    #region DeviceStatus Operations

    /// <inheritdoc />
    public async Task<IEnumerable<DeviceStatus>> GetDeviceStatusAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting device status with count: {Count}, skip: {Skip}", count, skip);
        return await _deviceStatusRepository.GetDeviceStatusAsync(count, skip, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<DeviceStatus?> GetDeviceStatusByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting device status by ID: {Id}", id);
        return await _deviceStatusRepository.GetDeviceStatusByIdAsync(id, cancellationToken);
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
        _logger.LogDebug(
            "Getting device status with advanced filter - count: {Count}, skip: {Skip}, findQuery: {FindQuery}, reverse: {Reverse}",
            count,
            skip,
            findQuery,
            reverseResults
        );

        return await _deviceStatusRepository.GetDeviceStatusWithAdvancedFilterAsync(
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
        _logger.LogDebug("Creating {Count} device status entries", deviceStatuses.Count());
        return await _deviceStatusRepository.CreateDeviceStatusAsync(
            deviceStatuses,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<DeviceStatus?> UpdateDeviceStatusAsync(
        string id,
        DeviceStatus deviceStatus,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Updating device status with ID: {Id}", id);
        return await _deviceStatusRepository.UpdateDeviceStatusAsync(
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
        _logger.LogDebug("Deleting device status with ID: {Id}", id);
        return await _deviceStatusRepository.DeleteDeviceStatusAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> BulkDeleteDeviceStatusAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Bulk deleting device status with query: {Query}", findQuery);
        return await _deviceStatusRepository.BulkDeleteDeviceStatusAsync(
            findQuery,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public async Task<long> CountDeviceStatusAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Counting device status with query: {Query}", findQuery);
        return await _deviceStatusRepository.CountDeviceStatusAsync(findQuery, cancellationToken);
    }

    #endregion

    #region Profile Operations

    /// <inheritdoc />
    public async Task<Profile?> GetCurrentProfileAsync(
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting current profile");
        return await _profileRepository.GetCurrentProfileAsync(cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Profile?> GetProfileByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting profile by ID: {Id}", id);
        return await _profileRepository.GetProfileByIdAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<IEnumerable<Profile>> GetProfilesAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Getting profiles with count: {Count}, skip: {Skip}", count, skip);
        return await _profileRepository.GetProfilesAsync(count, skip, cancellationToken);
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
        _logger.LogDebug(
            "Getting profiles with advanced filter - count: {Count}, skip: {Skip}, query: {Query}, reverse: {Reverse}",
            count,
            skip,
            findQuery,
            reverseResults
        );
        return await _profileRepository.GetProfilesWithAdvancedFilterAsync(
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
        _logger.LogDebug("Creating profiles: {Count}", profiles.Count());
        return await _profileRepository.CreateProfilesAsync(profiles, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<Profile?> UpdateProfileAsync(
        string id,
        Profile profile,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Updating profile with ID: {Id}", id);
        return await _profileRepository.UpdateProfileAsync(id, profile, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> DeleteProfileAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Deleting profile with ID: {Id}", id);
        return await _profileRepository.DeleteProfileAsync(id, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<long> CountProfilesAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug("Counting profiles with query: {Query}", findQuery);
        return await _profileRepository.CountProfilesAsync(findQuery, cancellationToken);
    }

    #endregion
}
