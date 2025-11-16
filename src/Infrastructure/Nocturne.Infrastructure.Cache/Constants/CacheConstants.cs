namespace Nocturne.Infrastructure.Cache.Constants;

/// <summary>
/// Constants for cache-related magic strings and values
/// </summary>
public static class CacheConstants
{
    /// <summary>
    /// Processing status values
    /// </summary>
    public static class ProcessingStatus
    {
        public const string Pending = "pending";
        public const string Processing = "processing";
        public const string Completed = "completed";
        public const string Failed = "failed";
    }

    /// <summary>
    /// Default configuration values
    /// </summary>
    public static class Defaults
    {
        public const int DefaultExpirationSeconds = 300; // 5 minutes
        public const int CurrentEntryExpirationSeconds = 60; // 1 minute
        public const int RecentEntriesExpirationSeconds = 120; // 2 minutes
        public const int RecentTreatmentsExpirationSeconds = 300; // 5 minutes
        public const int ProfileTimestampExpirationSeconds = 1800; // 30 minutes
        public const int IobCalculationExpirationSeconds = 900; // 15 minutes
        public const int CobCalculationExpirationSeconds = 900; // 15 minutes
        public const int ProfileCalculationExpirationSeconds = 3600; // 1 hour
        public const int StatisticsExpirationSeconds = 1800; // 30 minutes
        public const string KeyPrefix = "nocturne";
        public const bool EnableBackgroundCacheRefresh = false;
        public const bool EnableCalculationCacheCompression = false;
    }

    /// <summary>
    /// Cache key prefixes and patterns
    /// </summary>
    public static class KeyPrefixes
    {
        public const string Entries = "entries";
        public const string Treatments = "treatments";
        public const string DeviceStatus = "devicestatus";
        public const string Profiles = "profiles";
        public const string Food = "food";
        public const string Settings = "settings";
        public const string Status = "status";
        public const string Version = "version";
        public const string Loop = "loop";
        public const string Iob = "iob";
        public const string Cob = "cob";
        public const string Current = "current";
        public const string Recent = "recent";
        public const string Calculations = "calculations";
        public const string Stats = "stats";
        public const string System = "system";
        public const string Response = "response";
        public const string ProcessingStatus = "processing:status";
    }

    /// <summary>
    /// Cache invalidation patterns
    /// </summary>
    public static class InvalidationPatterns
    {
        public const string AllPattern = "*";
        public const string GlucosePattern = "glucose:*";
        public const string TirPattern = "tir:*";
        public const string HbA1cPattern = "hba1c:*";
    }

    /// <summary>
    /// Response cache route patterns
    /// </summary>
    public static class CacheableRoutes
    {
        public const string EntriesCurrent = "/api/v1/entries/current";
        public const string Entries = "/api/v1/entries";
        public const string Treatments = "/api/v1/treatments";
        public const string Profile = "/api/v1/profile";
        public const string Status = "/api/v1/status";
        public const string EntriesCurrentV2 = "/api/v2/entries/current";
        public const string EntriesV2 = "/api/v2/entries";
        public const string TreatmentsV2 = "/api/v2/treatments";
        public const string ProfileV2 = "/api/v2/profile";
    }

    /// <summary>
    /// Common entry types
    /// </summary>
    public static class EntryTypes
    {
        public const string Sgv = "sgv";
        public const string Mbg = "mbg";
        public const string Cal = "cal";
    }

    /// <summary>
    /// Statistics periods
    /// </summary>
    public static class StatsPeriods
    {
        public const string TwentyFourHours = "24h";
        public const string SevenDays = "7d";
        public const string ThirtyDays = "30d";
    }

    /// <summary>
    /// Cleanup intervals
    /// </summary>
    public static class CleanupIntervals
    {
        public static readonly TimeSpan StatusCleanup = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan CacheCleanup = TimeSpan.FromMinutes(5);
    }

    /// <summary>
    /// Default TTL values
    /// </summary>
    public static class DefaultTtl
    {
        public static readonly TimeSpan ProcessingStatus = TimeSpan.FromHours(1);
        public static readonly TimeSpan SystemLookups = TimeSpan.FromHours(4);
        public static readonly TimeSpan SystemStatus = TimeSpan.FromMinutes(5);
        public static readonly TimeSpan StatusResponse = TimeSpan.FromMinutes(2);
    }
}
