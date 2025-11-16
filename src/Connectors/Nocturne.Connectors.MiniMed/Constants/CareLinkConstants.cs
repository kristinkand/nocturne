using Nocturne.Core.Models;

namespace Nocturne.Connectors.MiniMed.Constants;

/// <summary>
/// Constants specific to MiniMed CareLink connector
/// </summary>
public static class CareLinkConstants
{
    /// <summary>
    /// Known CareLink servers
    /// </summary>
    public static class Servers
    {
        public const string US = "carelink.minimed.com";
        public const string EU = "carelink.minimed.eu";
    }

    /// <summary>
    /// API endpoints for CareLink
    /// </summary>
    public static class ApiPaths
    {
        public const string Login = "/patient/sso/login";
        public const string LoginData = "/patient/sso/login/data";
        public const string RecentData = "/patient/connect/data/recent";
        public const string PatientData = "/patient/connect/data";
    }

    /// <summary>
    /// HTTP headers for CareLink API
    /// </summary>
    public static class Headers
    {
        public const string Accept =
            "text/html,application/xhtml+xml,application/xml;q=0.9,image/webp,*/*;q=0.8";
        public const string AcceptEncoding = "gzip, deflate, br";
        public const string AcceptLanguage = "en;q=0.9, *;q=0.8";
        public const string UserAgent =
            "Mozilla/5.0 (Macintosh; Intel Mac OS X 10_15_7) AppleWebKit/605.1.15 (KHTML, like Gecko) Version/16.5 Safari/605.1.15";
        public const string SecChUa =
            "\"Chromium\";v=\"112\", \"Google Chrome\";v=\"112\", \"Not:A-Brand\";v=\"99\"";
    }

    /// <summary>
    /// Trend direction mappings from CareLink-specific terms to standard Direction enum
    /// </summary>
    public static class TrendDirections
    {
        public static readonly Direction RisingQuickly = Direction.DoubleUp;
        public static readonly Direction Rising = Direction.SingleUp;
        public static readonly Direction RisingSlightly = Direction.FortyFiveUp;
        public static readonly Direction Steady = Direction.Flat;
        public static readonly Direction FallingSlightly = Direction.FortyFiveDown;
        public static readonly Direction Falling = Direction.SingleDown;
        public static readonly Direction FallingQuickly = Direction.DoubleDown;
        public static readonly Direction Unknown = Direction.NotComputable;
    }

    /// <summary>
    /// Configuration specific to CareLink
    /// </summary>
    public static class Configuration
    {
        public const string DefaultRegion = "us";
        public const string DeviceIdentifier = "nightscout-connect-carelink";
    }
}
