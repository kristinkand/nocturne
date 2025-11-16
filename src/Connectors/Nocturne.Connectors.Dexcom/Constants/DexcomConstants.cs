namespace Nocturne.Connectors.Dexcom.Constants;

/// <summary>
/// Constants specific to Dexcom Share connector
/// </summary>
public static class DexcomConstants
{
    /// <summary>
    /// Known Dexcom Share servers
    /// </summary>
    public static class Servers
    {
        public const string US = "share2.dexcom.com";
        public const string OUS = "shareous1.dexcom.com";
    }

    /// <summary>
    /// API endpoints for Dexcom Share
    /// </summary>
    public static class ApiPaths
    {
        public const string Login =
            "/ShareWebServices/Services/General/LoginPublisherAccountByName";
        public const string GlucoseReadings =
            "/ShareWebServices/Services/Publisher/ReadPublisherLatestGlucoseValues";
        public const string ApplicationId = "d89443d2-327c-4a6f-89e5-496bbb0317db";
    }

    /// <summary>
    /// Configuration specific to Dexcom
    /// </summary>
    public static class Configuration
    {
        public const string DefaultRegion = "us";
        public const string DeviceIdentifier = "nightscout-connect-dexcom";
        public const int MaxCount = 1440; // 24 hours worth of 1-minute readings
    }
}
