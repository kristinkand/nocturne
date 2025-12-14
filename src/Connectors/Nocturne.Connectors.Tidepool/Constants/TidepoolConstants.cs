namespace Nocturne.Connectors.Tidepool.Constants;

/// <summary>
/// Constants for Tidepool API integration
/// </summary>
public static class TidepoolConstants
{
    /// <summary>
    /// Default Tidepool API servers
    /// </summary>
    public static class Servers
    {
        /// <summary>
        /// Production API server
        /// </summary>
        public const string Production = "https://api.tidepool.org";

        /// <summary>
        /// Integration/testing API server
        /// </summary>
        public const string Integration = "https://int-api.tidepool.org";
    }

    /// <summary>
    /// API endpoints
    /// </summary>
    public static class Endpoints
    {
        /// <summary>
        /// Authentication endpoint
        /// </summary>
        public const string Login = "/auth/login";

        /// <summary>
        /// Data endpoint format (requires userId)
        /// </summary>
        public const string DataFormat = "/data/{0}";
    }

    /// <summary>
    /// Tidepool data types for API queries
    /// </summary>
    public static class DataTypes
    {
        /// <summary>
        /// Continuous blood glucose readings
        /// </summary>
        public const string Cbg = "cbg";

        /// <summary>
        /// Self-monitored blood glucose (finger sticks)
        /// </summary>
        public const string Smbg = "smbg";

        /// <summary>
        /// Insulin bolus deliveries
        /// </summary>
        public const string Bolus = "bolus";

        /// <summary>
        /// Food/carbohydrate entries
        /// </summary>
        public const string Food = "food";

        /// <summary>
        /// Physical activity/exercise
        /// </summary>
        public const string PhysicalActivity = "physicalActivity";

        /// <summary>
        /// Pump settings (profiles)
        /// </summary>
        public const string PumpSettings = "pumpSettings";
    }

    /// <summary>
    /// HTTP header names
    /// </summary>
    public static class Headers
    {
        /// <summary>
        /// Session token header returned after authentication
        /// </summary>
        public const string SessionToken = "x-tidepool-session-token";
    }
}
