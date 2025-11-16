using Nocturne.Core.Models;

namespace Nocturne.Connectors.Core.Constants;

/// <summary>
/// Shared constants used across all connector implementations
/// </summary>
public static class SharedConnectorConstants
{
    /// <summary>
    /// Common HTTP client configuration
    /// </summary>
    public static class HttpClient
    {
        public static readonly TimeSpan DefaultTimeout = TimeSpan.FromMinutes(2);
        public const string DefaultUserAgent = "Nocturne-Connect/1.0";
    }

    /// <summary>
    /// Common retry configuration
    /// </summary>
    public static class Retry
    {
        public const int MaxRetries = 3;
        public static readonly TimeSpan[] RetryDelays =
        {
            TimeSpan.FromSeconds(5),
            TimeSpan.FromSeconds(15),
            TimeSpan.FromSeconds(30),
        };
    }

    /// <summary>
    /// Common health monitoring configuration
    /// </summary>
    public static class Health
    {
        public const int MaxFailedRequests = 5;
        public const int DefaultLookbackDays = 7;
        public const int DefaultLookbackHours = 24;
        public const int DefaultOverlapMinutes = 30;
    }

    /// <summary>
    /// Common data processing configuration
    /// </summary>
    public static class Data
    {
        public const string DefaultEntryType = "sgv";
        public static readonly string DefaultDirection = Direction.NotComputable.ToString();
    }

    /// <summary>
    /// Common HTTP headers
    /// </summary>
    public static class Headers
    {
        public const string Accept = "application/json";
        public const string ContentType = "application/json";
    }
}
