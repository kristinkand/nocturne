namespace Nocturne.Core.Constants;

/// <summary>
/// Defines all the standard URLs and endpoints used throughout the Nocturne application.
/// This centralizes URL configuration to avoid hardcoded strings and ensure consistency.
/// </summary>
public static class UrlConstants
{
    // External/Public URLs
    public static class External
    {
        /// <summary>Nocturne website URL (temporary until custom domain is purchased)</summary>
        public const string NocturneWebsite = "https://nightscoutfoundation.org/nocturne";

        /// <summary>Nocturne documentation URL</summary>
        public const string NocturneDocsBase = NocturneWebsite + "/docs";

        /// <summary>Dexcom connector documentation</summary>
        public const string DocsDexcom = NocturneDocsBase + "/connectors/dexcom";

        /// <summary>Libre connector documentation</summary>
        public const string DocsLibre = NocturneDocsBase + "/connectors/libre";

        /// <summary>CareLink connector documentation</summary>
        public const string DocsCareLink = NocturneDocsBase + "/connectors/carelink";

        /// <summary>Nightscout connector documentation</summary>
        public const string DocsNightscout = NocturneDocsBase + "/connectors/nightscout";

        /// <summary>Glooko connector documentation</summary>
        public const string DocsGlooko = NocturneDocsBase + "/connectors/glooko";
    }

    // Base URLs
    public static class Base
    {
        /// <summary>Legacy Nightscout localhost URL</summary>
        public const string LegacyNightscout = "http://localhost:1337";

        /// <summary>Nocturne API localhost URL (uses legacy port for compatibility)</summary>
        public const string NocturneApiHttp = "http://localhost:1612";

        /// <summary>Nocturne API HTTPS localhost URL</summary>
        public const string NocturneApiHttps = "https://localhost:1612";

        /// <summary>Frontend development server URL</summary>
        public const string FrontendDev = "http://localhost:5173";

        /// <summary>Frontend preview server URL</summary>
        public const string FrontendPreview = "http://localhost:4173";

        /// <summary>WebSocket Bridge URL</summary>
        public const string WebSocketBridge = "http://localhost:1613";

        /// <summary>WebSocket Health URL</summary>
        public const string WebSocketHealth = "http://localhost:1614";
    }

    // Database URLs
    public static class Database
    {
        /// <summary>MongoDB default connection string</summary>
        public const string MongoDbDefault = "mongodb://localhost:27017";

        /// <summary>MongoDB with authentication</summary>
        public const string MongoDbWithAuth = "mongodb://admin:mongoRootPassword02@localhost:27017";

        /// <summary>MongoDB test connection (different port to avoid conflicts)</summary>
        public const string MongoDbTest = "mongodb://localhost:27018";
    }

    // SignalR Hub Endpoints
    public static class SignalR
    {
        /// <summary>Data hub endpoint</summary>
        public const string DataHub = "/hubs/data";

        /// <summary>Notification hub endpoint</summary>
        public const string NotificationHub = "/hubs/notification";

        /// <summary>Full data hub URL</summary>
        public const string DataHubUrl = Base.NocturneApiHttp + DataHub;

        /// <summary>Full notification hub URL</summary>
        public const string NotificationHubUrl = Base.NocturneApiHttp + NotificationHub;
    }

    // API Endpoints
    public static class Api
    {
        // V1 Endpoints (Legacy compatibility)
        public const string V1Base = "/api/v1";
        public const string V1Status = "/api/v1/status";
        public const string V1Entries = "/api/v1/entries";
        public const string V1Treatments = "/api/v1/treatments";
        public const string V1Profile = "/api/v1/profile";

        // V2 Endpoints
        public const string V2Base = "/api/v2";
        public const string V2Properties = "/api/v2/properties";
        public const string V2Summary = "/api/v2/summary";
        public const string V2DData = "/api/v2/ddata";

        // V3 Endpoints
        public const string V3Base = "/api/v3";
        public const string V3Version = "/api/v3/version";
        public const string V3Status = "/api/v3/status";
        public const string V3Entries = "/api/v3/entries";
        public const string V3Settings = "/api/v3/settings";

        // Special Endpoints
        public const string Alexa = "/api/alexa";
        public const string Versions = "/api/versions";
        public const string OpenApi = "/openapi";
        public const string Scalar = "/scalar";
    }

    // Health Check Endpoints
    public static class Health
    {
        public const string Check = "/health";
        public const string Stats = "/stats";
        public const string Ready = "/ready";
        public const string Live = "/live";
    }
}
