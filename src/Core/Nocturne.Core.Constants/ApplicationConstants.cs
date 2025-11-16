namespace Nocturne.Core.Constants;

/// <summary>
/// Application configuration constants
/// </summary>
public static class ApplicationConstants
{
    /// <summary>
    /// Cache configuration
    /// </summary>
    public static class Cache
    {
        public const string InstanceName = "nocturne";
    }

    /// <summary>
    /// Proxy configuration
    /// </summary>
    public static class Proxy
    {
        public const string SectionName = "Proxy";
        public const int DefaultTimeoutSeconds = 30;
        public const int DefaultRetryAttempts = 3;
        public const bool DefaultForwardAuthHeaders = true;
    }

    /// <summary>
    /// Health check endpoints
    /// </summary>
    public static class HealthCheck
    {
        public const string HealthEndpoint = "/health";
        public const string AliveEndpoint = "/alive";
        public const string StatsEndpoint = "/stats";
        public const string LiveTag = "live";
    }

    /// <summary>
    /// OpenTelemetry and monitoring
    /// </summary>
    public static class Monitoring
    {
        public const string ServiceName = "Nocturne";
        public const string ServiceVersion = "1.0.0";
    }

    /// <summary>
    /// CORS configuration
    /// </summary>
    public static class Cors
    {
        public const string DefaultPolicy = "DefaultCorsPolicy";
        public static readonly string[] DefaultMethods =
        {
            "GET",
            "POST",
            "PUT",
            "DELETE",
            "OPTIONS",
        };
        public static readonly string[] DefaultHeaders =
        {
            "Content-Type",
            "Authorization",
            "X-Requested-With",
        };
    }

    /// <summary>
    /// File system paths
    /// </summary>
    public static class Paths
    {
        public const string DataDirectory = "./data";
        public const string LogDirectory = "./logs";
        public const string ConfigDirectory = "./config";
        public const string TempDirectory = "./temp";
    }

    /// <summary>
    /// Database configuration
    /// </summary>
    public static class Database
    {
        public const string DefaultDatabaseName = "nocturne-database";
        public const string DefaultUsername = "nocturne-user";
        public const string DefaultPassword = "nocturne_password";
        public const string RootPassword = "nocturne_root_password";
    }

    /// <summary>
    /// Web application settings
    /// </summary>
    public static class Web
    {
        /// <summary>
        /// Client settings defaults
        /// </summary>
        public static class ClientDefaults
        {
            public const string Units = "mg/dl";
            public const int TimeFormat = 12;
            public const bool NightMode = false;
            public const bool ShowBGON = true;
            public const bool ShowIOB = true;
            public const bool ShowCOB = true;
            public const bool ShowBasal = true;
            public const string Language = "en";
            public const string Theme = "default";
            public const bool AlarmUrgentHigh = true;
            public const bool AlarmHigh = true;
            public const bool AlarmLow = true;
            public const bool AlarmUrgentLow = true;
            public const bool AlarmTimeagoWarn = true;
            public const int AlarmTimeagoWarnMins = 15;
            public const bool AlarmTimeagoUrgent = true;
            public const int AlarmTimeagoUrgentMins = 30;
            public const bool ShowForecast = true;
            public const int FocusHours = 3;
            public const int Heartbeat = 60;
            public const string AuthDefaultRoles = "readable";
        }

        /// <summary>
        /// Threshold defaults
        /// </summary>
        public static class Thresholds
        {
            public const int High = 260;
            public const int TargetTop = 180;
            public const int TargetBottom = 80;
            public const int Low = 55;
        }

        /// <summary>
        /// Clock configuration defaults
        /// </summary>
        public static class Clock
        {
            public const int MaxStaleMinutes = 60;
            public static readonly string[] AllowedElements = { "sg", "dt", "ar", "ag", "time" };

            /// <summary>
            /// Size constraints for clock elements
            /// </summary>
            public static class SizeConstraints
            {
                public const int SgMin = 20;
                public const int SgMax = 80;
                public const int DtMin = 10;
                public const int DtMax = 40;
                public const int ArMin = 15;
                public const int ArMax = 50;
                public const int AgMin = 8;
                public const int AgMax = 24;
                public const int TimeMin = 16;
                public const int TimeMax = 48;
            }
        }
    }

    /// <summary>
    /// Plugin names and identifiers
    /// </summary>
    public static class Plugins
    {
        public const string Delta = "delta";
        public const string Direction = "direction";
        public const string TimeAgo = "timeago";
        public const string DeviceStatus = "devicestatus";
        public const string Basal = "basal";
        public const string Bridge = "bridge";
        public const string CannulaAge = "cage";
        public const string SensorAge = "sage";
        public const string InsulinAge = "iage";
        public const string BatteryAge = "bage";
        public const string BasalProfile = "basal";
        public const string Share2Nightscout = "bridge";
        public const string MiniMedConnect = "mmconnect";
        public const string Pump = "pump";
        public const string OpenAPS = "openaps";
        public const string Loop = "loop";
        public const string Override = "override";

        public static readonly string[] DefaultShowPlugins =
        {
            Delta,
            Direction,
            TimeAgo,
            DeviceStatus,
        };
    }

    /// <summary>
    /// API route patterns
    /// </summary>
    public static class ApiRoutes
    {
        /// <summary>
        /// Native API routes (handled by Nocturne)
        /// </summary>
        public static class Native
        {
            public const string Status = "status";
            public const string Entries = "entries";
            public const string Treatments = "treatments";
            public const string DeviceStatus = "devicestatus";
            public const string Profile = "profile";
            public const string Food = "food";
            public const string Activity = "activity";
            public const string Count = "count";
            public const string Echo = "echo";
            public const string Times = "times";
            public const string Slice = "slice";
            public const string VerifyAuth = "verifyauth";
            public const string Notifications = "notifications";
            public const string AdminNotifies = "adminnotifies";
        }

        /// <summary>
        /// V2 API native routes
        /// </summary>
        public static class V2Native
        {
            public const string DData = "ddata";
            public const string Properties = "properties";
            public const string Summary = "summary";
            public const string Notifications = "notifications";
            public const string Authorization = "authorization";
        }

        /// <summary>
        /// Legacy proxy route patterns (regex patterns for YARP)
        /// </summary>
        public static class Patterns
        {
            public const string LegacyApiV1 =
                "^(?!status|entries|treatments|devicestatus|profile|food|activity|count|echo|times|slice|verifyauth|notifications|adminnotifies).*$";
            public const string LegacyApiV2 =
                "^(?!ddata|properties|summary|notifications|authorization).*$";
            public const string LegacyApiV3 = "{**catch-all}";
        }
    }
}
