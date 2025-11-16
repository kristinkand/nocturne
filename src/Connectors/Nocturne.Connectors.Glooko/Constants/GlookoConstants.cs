namespace Nocturne.Connectors.Glooko.Constants;

/// <summary>
/// Constants specific to Glooko connector
/// </summary>
public static class GlookoConstants
{
    /// <summary>
    /// Known Glooko servers
    /// </summary>
    public static class Servers
    {
        public const string EU = "eu.api.glooko.com";
        public const string US = "api.glooko.com";
    }

    /// <summary>
    /// API endpoints for Glooko
    /// </summary>
    public static class ApiPaths
    {
        public const string Login = "/oauth2/token";
        public const string Patients = "/v1/users/me/patients";
        public const string Entries = "/v1/patients/{0}/entries";
        public const string Medications = "/v1/patients/{0}/medications";
        public const string Devices = "/v1/patients/{0}/devices";
        public const string Activities = "/v1/patients/{0}/activities";
        public const string Foods = "/v1/patients/{0}/foods";
    }

    /// <summary>
    /// Configuration specific to Glooko
    /// </summary>
    public static class Configuration
    {
        public const string DefaultServer = "eu.api.glooko.com";
        public const string DeviceIdentifier = "nightscout-connect-glooko";
        public const int DefaultTimezoneOffset = 0;
        public const int RateLimitDelayMs = 1000; // 1 second between requests to avoid 422 errors
    }
}
