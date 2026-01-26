namespace Nocturne.Portal.API.Models;

/// <summary>
/// Request for generating docker-compose files
/// </summary>
public class GenerateRequest
{
    /// <summary>
    /// Setup type: "fresh", "migrate", or "compatibility-proxy"
    /// </summary>
    public string SetupType { get; set; } = "fresh";

    /// <summary>
    /// Migration configuration (required if SetupType is "migrate")
    /// </summary>
    public MigrationConfig? Migration { get; set; }

    /// <summary>
    /// Compatibility proxy configuration (required if SetupType is "compatibility-proxy")
    /// </summary>
    public CompatibilityProxyConfig? CompatibilityProxy { get; set; }

    /// <summary>
    /// PostgreSQL configuration
    /// </summary>
    public PostgresConfig Postgres { get; set; } = new();

    /// <summary>
    /// Optional services configuration
    /// </summary>
    public OptionalServicesConfig OptionalServices { get; set; } = new();

    /// <summary>
    /// Selected connectors with their configuration
    /// </summary>
    public List<ConnectorConfig> Connectors { get; set; } = [];
}

public class MigrationConfig
{
    /// <summary>
    /// Migration mode: "Api" (default) or "MongoDb"
    /// </summary>
    public string Mode { get; set; } = "Api";

    /// <summary>
    /// Nightscout URL for API mode
    /// </summary>
    public string? NightscoutUrl { get; set; }

    /// <summary>
    /// Nightscout API secret for API mode
    /// </summary>
    public string? NightscoutApiSecret { get; set; }

    /// <summary>
    /// MongoDB connection string for MongoDB mode
    /// </summary>
    public string? MongoConnectionString { get; set; }

    /// <summary>
    /// MongoDB database name for MongoDB mode
    /// </summary>
    public string? MongoDatabaseName { get; set; }
}


public class CompatibilityProxyConfig
{
    public string NightscoutUrl { get; set; } = string.Empty;
    public string NightscoutApiSecret { get; set; } = string.Empty;
    public bool EnableDetailedLogging { get; set; } = false;
}

public class PostgresConfig
{
    /// <summary>
    /// Whether to use the included PostgreSQL container
    /// </summary>
    public bool UseContainer { get; set; } = true;

    /// <summary>
    /// External connection string (required if UseContainer is false)
    /// </summary>
    public string? ConnectionString { get; set; }
}

public class OptionalServicesConfig
{
    /// <summary>
    /// Aspire Dashboard for telemetry visualization
    /// </summary>
    public OptionalServiceConfig AspireDashboard { get; set; } = new() { Enabled = true };

    /// <summary>
    /// Scalar API reference documentation
    /// </summary>
    public OptionalServiceConfig Scalar { get; set; } = new() { Enabled = true };

    /// <summary>
    /// Watchtower for automatic container updates
    /// </summary>
    public OptionalServiceConfig Watchtower { get; set; } = new();
}

public class OptionalServiceConfig
{
    /// <summary>
    /// Whether the service is enabled
    /// </summary>
    public bool Enabled { get; set; }
}

public class ConnectorConfig
{
    /// <summary>
    /// Connector type (e.g., "Dexcom")
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Configuration values keyed by environment variable name.
    /// The frontend should use the EnvVar values from /api/connectors as keys.
    /// Example: { "CONNECT_DEXCOM_USERNAME": "user@example.com", "CONNECT_DEXCOM_PASSWORD": "secret" }
    /// </summary>
    public Dictionary<string, string> Config { get; set; } = [];
}
