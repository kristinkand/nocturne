namespace Nocturne.Connectors.Nightscout.Constants;

/// <summary>
/// Constants specific to Nightscout connector
/// </summary>
public static class NightscoutConstants
{
    /// <summary>
    /// API endpoints for Nightscout
    /// </summary>
    public static class ApiPaths
    {
        public const string Entries = "/api/v1/entries";
        public const string EntriesSgv = "/api/v1/entries/sgv.json";
        public const string Treatments = "/api/v1/treatments";
        public const string DeviceStatus = "/api/v1/devicestatus";
        public const string Status = "/api/v1/status";
        public const string Profile = "/api/v1/profile";
    }

    /// <summary>
    /// Configuration specific to Nightscout
    /// </summary>
    public static class Configuration
    {
        public const int DefaultCount = 100000; // High limit to get more data per request
        public const string DeviceIdentifier = "nightscout-connect-nightscout";
        public const int DefaultLookbackHours = 24;
    }
}
