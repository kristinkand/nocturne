namespace Nocturne.Core.Constants;

/// <summary>
/// Defines all the standard ports used throughout the Nocturne application.
/// This centralizes port configuration to avoid conflicts and make management easier.
/// </summary>
public static class PortConstants
{
    // Core Application Ports
    /// <summary>Legacy Nightscout default port</summary>
    public const int LegacyNightscout = 1337;

    /// <summary>Nocturne API HTTP port (uses legacy port for compatibility)</summary>
    public const int NocturneApiHttp = 1612;

    /// <summary>Nocturne API HTTPS port</summary>
    public const int NocturneApiHttps = 1612;

    // Frontend Ports
    /// <summary>Vite development server port</summary>
    public const int ViteDev = 5173;

    /// <summary>Vite preview server port</summary>
    public const int VitePreview = 4173;

    // WebSocket Bridge Ports
    /// <summary>Socket.IO server port</summary>
    public const int SocketIo = 1613;

    /// <summary>WebSocket Bridge health check port</summary>
    public const int WebSocketHealth = 1614;

    // Database Ports
    /// <summary>MongoDB consistent port for all environments</summary>
    public const int MongoDb = 27017;

    // Aspire Ports
    /// <summary>Aspire Dashboard HTTP port</summary>
    public const int AspireDashboardHttp = 15888;

    /// <summary>Aspire Dashboard HTTPS port</summary>
    public const int AspireDashboardHttps = 18888;

    // Test Ports (used for integration tests to avoid conflicts)
    /// <summary>MongoDB test port (alternative port for testing when 27017 is occupied)</summary>
    public const int MongoDbTest = 27018;

    /// <summary>API test port</summary>
    public const int ApiTest = 5002;

    // External Service Ports (for reference)
    /// <summary>Typical HTTP port</summary>
    public const int Http = 80;

    /// <summary>Typical HTTPS port</summary>
    public const int Https = 443;
}
