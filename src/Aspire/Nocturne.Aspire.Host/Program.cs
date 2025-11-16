using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Nocturne.Aspire.Host.Services;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Core.Constants;

class Program
{
    public record ConnectorSetup(
        ConnectSource ConnectSource,
        string ServiceName,
        string EnvironmentPrefix,
        Dictionary<string, string> RequiredProperties,
        Dictionary<string, string> OptionalProperties
    );

    static async Task Main(string[] args)
    {
        var builder = DistributedApplication.CreateBuilder(args);

        // Add Docker Compose environment for generating docker-compose.yml files
        // builder.AddDockerComposeEnvironment("production");

        // Get the solution root directory
        var solutionRoot = Path.GetFullPath(
            Path.Combine(builder.AppHostDirectory, "..", "..", "..")
        );

        // Load appsettings from solution root
        builder.Configuration.AddJsonFile(
            Path.Combine(solutionRoot, "appsettings.json"),
            optional: true,
            reloadOnChange: true
        );
        builder.Configuration.AddJsonFile(
            Path.Combine(solutionRoot, $"appsettings.{builder.Environment.EnvironmentName}.json"),
            optional: true,
            reloadOnChange: true
        );

        // Initialize configuration services
        var configService = new ConfigurationService(solutionRoot);
        var interactiveConfigService = new InteractiveConfigurationService(builder, configService);

        // Check for interactive mode (default: non-interactive)
        var isInteractive =
            args.Contains(ServiceNames.ConfigKeys.InteractiveArg)
            || args.Contains(ServiceNames.ConfigKeys.InteractiveShort)
            || Environment
                .GetEnvironmentVariable(ServiceNames.ConfigKeys.NocturneInteractive)
                ?.ToLowerInvariant() == ServiceNames.ConfigKeys.TrueValue;

        // Get configuration parameters (interactive or default)
        var configParameters = isInteractive
            ? await interactiveConfigService.ConfigureServicesAsync()
            : interactiveConfigService.GetDefaultConfiguration();

        // Add PostgreSQL database - use remote database connection or local container
        var useRemoteDb = builder.Configuration.GetValue<bool>("UseRemoteDatabase", false);

        Console.WriteLine($"[Aspire] UseRemoteDatabase: {useRemoteDb}");
        Console.WriteLine($"[Aspire] Environment: {builder.Environment.EnvironmentName}");

        // Get remote connection string if using remote database
        string? remoteConnectionString = null;
        if (useRemoteDb)
        {
            remoteConnectionString = builder.Configuration.GetConnectionString(
                ServiceNames.PostgreSql
            );

            if (string.IsNullOrWhiteSpace(remoteConnectionString))
            {
                throw new InvalidOperationException(
                    $"Remote database enabled but connection string '{ServiceNames.PostgreSql}' not found in configuration."
                );
            }

            Console.WriteLine($"[Aspire] Using remote database: {remoteConnectionString}");
        }

        IResourceBuilder<IResourceWithConnectionString> nocturnedb;

        if (!useRemoteDb)
        {
            // Use local PostgreSQL container
            var postgresUsername = builder.AddParameter(
                ServiceNames.Parameters.PostgresUsername,
                value: configParameters.GetValueOrDefault(
                    ServiceNames.Parameters.PostgresUsername,
                    ServiceNames.Defaults.PostgresUser
                ),
                secret: false
            );
            var postgresPassword = builder.AddParameter(
                ServiceNames.Parameters.PostgresPassword,
                value: configParameters.GetValueOrDefault(
                    ServiceNames.Parameters.PostgresPassword,
                    ServiceNames.Defaults.PostgresPassword
                ),
                secret: true
            );
            var postgresDbName = builder.AddParameter(
                ServiceNames.Parameters.PostgresDbName,
                value: configParameters.GetValueOrDefault(
                    ServiceNames.Parameters.PostgresDbName,
                    ServiceNames.Defaults.PostgresDatabase
                ),
                secret: false
            );
            var postgres = builder
                .AddPostgres(ServiceNames.PostgreSql)
                .WithLifetime(ContainerLifetime.Persistent)
                .WithUserName(postgresUsername)
                .WithPassword(postgresPassword);

            // Only add PgAdmin in development to save resources
            if (builder.Environment.IsDevelopment())
            {
                postgres.WithPgAdmin();
            }

            postgres.WithDataVolume(ServiceNames.Volumes.PostgresData);

            nocturnedb = postgres.AddDatabase(
                configParameters[ServiceNames.Parameters.PostgresDbName]
            );
            postgresUsername.WithParentRelationship(postgres);
            postgresPassword.WithParentRelationship(postgres);
            postgresDbName.WithParentRelationship(postgres);
        }
        else
        {
            // For remote database, create a placeholder resource
            // We'll inject the connection string directly via environment variable
            // @TODO This is really ugly but we are H A C K E R M O D E atm
            nocturnedb = builder.AddConnectionString(ServiceNames.Defaults.PostgresDatabase);
        }

        // Add the Nocturne API service (without embedded connectors)
        // Aspire will auto-generate a Dockerfile during publish
        var api = builder
            .AddProject<Projects.Nocturne_API>(ServiceNames.NocturneApi)
            .WithExternalHttpEndpoints();

        // For remote database, inject connection string directly as environment variable
        if (useRemoteDb)
        {
            api.WithEnvironment(
                $"ConnectionStrings__{ServiceNames.Defaults.PostgresDatabase}",
                remoteConnectionString!
            );
        }
        else
        {
            // For local database, use WithReference which automatically injects the connection string
            api.WaitFor(nocturnedb).WithReference(nocturnedb);
        }

        // Add connector services as independent services
        var enabledConnectorConfigs = configService.GetAllEnabledConnectorConfigurations().ToList();

        // Helper method to check if config is of specific type
        static bool IsConnectorType(IConnectorConfiguration config, ConnectSource expectedSource)
        {
            return config.ConnectSource == expectedSource;
        }

        // Dexcom Connector Service
        var dexcomConfig = enabledConnectorConfigs.FirstOrDefault(c =>
            IsConnectorType(c, ConnectSource.Dexcom)
        );
        if (dexcomConfig != null)
        {
            var dexcomSetup = new ConnectorSetup(
                ConnectSource.Dexcom,
                ServiceNames.DexcomConnector,
                ServiceNames.ConnectorEnvironment.DexcomPrefix,
                new Dictionary<string, string>
                {
                    ["DexcomUsername"] = "DexcomUsername",
                    ["DexcomPassword"] = "DexcomPassword",
                    ["DexcomRegion"] = "DexcomRegion",
                },
                new Dictionary<string, string> { ["DexcomServer"] = "DexcomServer" }
            );

            AddConnectorService<Projects.Nocturne_Connectors_Dexcom>(
                builder,
                dexcomConfig,
                dexcomSetup
            );
        }

        // Glooko Connector Service
        var glookoConfig = enabledConnectorConfigs.FirstOrDefault(c =>
            IsConnectorType(c, ConnectSource.Glooko)
        );
        if (glookoConfig != null)
        {
            var glookoSetup = new ConnectorSetup(
                ConnectSource.Glooko,
                ServiceNames.GlookoConnector,
                ServiceNames.ConnectorEnvironment.GlookoPrefix,
                new Dictionary<string, string>
                {
                    ["GlookoEmail"] = "GlookoEmail",
                    ["GlookoPassword"] = "GlookoPassword",
                    ["GlookoTimezoneOffset"] = "GlookoTimezoneOffset",
                },
                new Dictionary<string, string> { ["GlookoServer"] = "GlookoServer" }
            );

            AddConnectorService<Projects.Nocturne_Connectors_Glooko>(
                builder,
                glookoConfig,
                glookoSetup
            );
        }

        // FreeStyle LibreLink Connector Service
        var libreConfig = enabledConnectorConfigs.FirstOrDefault(c =>
            IsConnectorType(c, ConnectSource.LibreLinkUp)
        );
        if (libreConfig != null)
        {
            var libreSetup = new ConnectorSetup(
                ConnectSource.LibreLinkUp,
                ServiceNames.LibreConnector,
                ServiceNames.ConnectorEnvironment.FreeStylePrefix,
                new Dictionary<string, string>
                {
                    ["LibreUsername"] = "LibreUsername",
                    ["LibrePassword"] = "LibrePassword",
                    ["LibreRegion"] = "LibreRegion",
                },
                new Dictionary<string, string>
                {
                    ["LibrePatientId"] = "LibrePatientId",
                    ["LibreServer"] = "LibreServer",
                }
            );

            AddConnectorService<Projects.Nocturne_Connectors_FreeStyle>(
                builder,
                libreConfig,
                libreSetup
            );
        }

        // MiniMed CareLink Connector Service
        var carelinkConfig = enabledConnectorConfigs.FirstOrDefault(c =>
            IsConnectorType(c, ConnectSource.CareLink)
        );
        if (carelinkConfig != null)
        {
            var carelinkSetup = new ConnectorSetup(
                ConnectSource.CareLink,
                ServiceNames.MiniMedConnector,
                ServiceNames.ConnectorEnvironment.MiniMedPrefix,
                new Dictionary<string, string>
                {
                    ["CarelinkUsername"] = "CarelinkUsername",
                    ["CarelinkPassword"] = "CarelinkPassword",
                    ["CarelinkRegion"] = "CarelinkRegion",
                },
                new Dictionary<string, string>
                {
                    ["CarelinkCountryCode"] = "CarelinkCountryCode",
                    ["CarelinkPatientUsername"] = "CarelinkPatientUsername",
                }
            );

            AddConnectorService<Projects.Nocturne_Connectors_MiniMed>(
                builder,
                carelinkConfig,
                carelinkSetup
            );
        }

        // Nightscout Connector Service
        var nightscoutConfig = enabledConnectorConfigs.FirstOrDefault(c =>
            IsConnectorType(c, ConnectSource.Nightscout)
        );
        if (nightscoutConfig != null)
        {
            var nightscoutSetup = new ConnectorSetup(
                ConnectSource.Nightscout,
                ServiceNames.NightscoutConnector,
                ServiceNames.ConnectorEnvironment.NightscoutPrefix,
                new Dictionary<string, string> { ["SourceEndpoint"] = "SourceEndpoint" },
                new Dictionary<string, string> { ["SourceApiSecret"] = "SourceApiSecret" }
            );

            AddConnectorService<Projects.Nocturne_Connectors_Nightscout>(
                builder,
                nightscoutConfig,
                nightscoutSetup
            );
        }

        // MyFitnessPal Connector Service
        var myFitnessPalConfig = enabledConnectorConfigs.FirstOrDefault(c =>
            IsConnectorType(c, ConnectSource.MyFitnessPal)
        );
        if (myFitnessPalConfig != null)
        {
            var myFitnessPalSetup = new ConnectorSetup(
                ConnectSource.MyFitnessPal,
                ServiceNames.MyFitnessPalConnector,
                ServiceNames.ConnectorEnvironment.MyFitnessPalPrefix,
                new Dictionary<string, string>
                {
                    ["MyFitnessPalUsername"] = "MyFitnessPalUsername",
                    ["MyFitnessPalPassword"] = "MyFitnessPalPassword",
                },
                new Dictionary<string, string> { ["MyFitnessPalApiKey"] = "MyFitnessPalApiKey" }
            );

            AddConnectorService<Projects.Nocturne_Connectors_MyFitnessPal>(
                builder,
                myFitnessPalConfig,
                myFitnessPalSetup
            );
        }

        // Add API_SECRET parameter for authentication
        var apiSecret = builder.AddParameter(
            ServiceNames.Parameters.ApiSecret,
            value: configParameters.GetValueOrDefault(
                ServiceNames.Parameters.ApiSecret,
                ServiceNames.Defaults.DefaultApiSecret
            ),
            secret: true
        );

        // Add SignalR Hub URL parameter for the web app's integrated WebSocket bridge
        var signalrHubUrl = builder.AddParameter(
            "SignalRHubUrl",
            value: configParameters.GetValueOrDefault(
                "SignalRHubUrl",
                "http://localhost:1612/hubs/data"
            ),
            secret: false
        );

        // Add the SvelteKit web application (with integrated WebSocket bridge)
        // For Azure deployment, use container. For local dev, use npm app.
        IResourceBuilder<IResourceWithEndpoints> web;
        if (builder.ExecutionContext.IsPublishMode)
        {
            // Use containerized deployment for publish/Azure
            // Build from workspace root with Dockerfile in packages/app
            var webWorkspaceRoot = Path.Combine(solutionRoot, "src", "Web");
            var dockerfilePath = Path.Combine("packages", "app", "Dockerfile");

            web = builder
                .AddDockerfile(ServiceNames.NocturneWeb, webWorkspaceRoot, dockerfilePath)
                .WithHttpEndpoint(port: 5173, targetPort: 5173, name: "http", isProxied: false)
                .WithExternalHttpEndpoints()
                .WaitFor(api)
                .WithEnvironment(ServiceNames.ConfigKeys.ApiSecret, apiSecret)
                .WithEnvironment("SIGNALR_HUB_URL", signalrHubUrl)
                .WithEnvironment("PUBLIC_API_URL", api.GetEndpoint("http"));
        }
        else
        {
            // Use npm app for local development
            var webRootPath = Path.Combine(solutionRoot, "src", "Web");
            web = builder
                .AddNpmApp(ServiceNames.NocturneWeb, webRootPath, "dev")
                .WithHttpEndpoint(port: 5173, targetPort: 5173, name: "http", isProxied: false)
                .WithExternalHttpEndpoints()
                .WaitFor(api)
                .WithReference(api)
                .WithEnvironment(ServiceNames.ConfigKeys.ApiSecret, apiSecret)
                .WithEnvironment("SIGNALR_HUB_URL", signalrHubUrl);
        }

        apiSecret.WithParentRelationship(web);
        signalrHubUrl.WithParentRelationship(web);

        // Add conditional notification services
        if (
            configParameters.GetValueOrDefault(
                ServiceNames.ConfigKeys.NotificationsConfigured,
                ServiceNames.ConfigKeys.FalseValue
            ) == ServiceNames.ConfigKeys.TrueValue
        )
        {
            if (configParameters.ContainsKey(ServiceNames.Parameters.PushoverApiToken))
            {
                var pushoverApiToken = builder.AddParameter(
                    ServiceNames.Parameters.PushoverApiToken,
                    value: configParameters[ServiceNames.Parameters.PushoverApiToken],
                    secret: true
                );
                var pushoverUserKey = builder.AddParameter(
                    ServiceNames.Parameters.PushoverUserKey,
                    value: configParameters[ServiceNames.Parameters.PushoverUserKey],
                    secret: true
                );

                // Note: Actual notification service projects would be added here when they exist
            }
        }

        // Add conditional OpenTelemetry services
        if (
            configParameters.GetValueOrDefault(
                ServiceNames.ConfigKeys.TelemetryConfigured,
                ServiceNames.ConfigKeys.FalseValue
            ) == ServiceNames.ConfigKeys.TrueValue
        )
        {
            var otlpEndpoint = builder.AddParameter(
                ServiceNames.Parameters.OtlpEndpoint,
                value: configParameters.GetValueOrDefault(
                    ServiceNames.Parameters.OtlpEndpoint,
                    ServiceNames.Defaults.DefaultOtlpEndpoint
                ),
                secret: false
            );

            // Note: OTEL collector or Jaeger could be added here
            // builder.AddContainer("jaeger", "jaegertracing/all-in-one")
            //     .WithEndpoint(16686, targetPort: 16686, name: "jaeger-ui")
            //     .WithEndpoint(14268, targetPort: 14268, name: "jaeger-collector");
        }

        var app = builder.Build();

        app.Run();
    }

    // Helper method to get property value as string (handles both string and non-string types)
    static string GetPropertyValueAsString(IConnectorConfiguration config, string propertyName)
    {
        try
        {
            var property = config.GetType().GetProperty(propertyName);
            if (property != null && property.CanRead)
            {
                var value = property.GetValue(config);
                if (value != null)
                {
                    return value.ToString() ?? "";
                }
            }
        }
        catch
        {
            // Ignore reflection errors and return empty string
        }
        return "";
    }

    // Generic method to add a connector service
    static IResourceBuilder<ProjectResource> AddConnectorService<TProject>(
        IDistributedApplicationBuilder builder,
        IConnectorConfiguration config,
        ConnectorSetup setup
    )
        where TProject : IProjectMetadata, new()
    {
        var connector = builder.AddProject<TProject>(setup.ServiceName).WithExternalHttpEndpoints();

        // Add common environment variables
        connector
            .WithEnvironment($"{setup.EnvironmentPrefix}NightscoutUrl", config.NightscoutUrl)
            .WithEnvironment(
                $"{setup.EnvironmentPrefix}NightscoutApiSecret",
                config.NightscoutApiSecret ?? ""
            )
            .WithEnvironment(
                $"{setup.EnvironmentPrefix}SyncIntervalMinutes",
                config.SyncIntervalMinutes.ToString()
            )
            .WithEnvironment(
                $"{setup.EnvironmentPrefix}ConnectSource",
                config.ConnectSource.ToString()
            );

        // Add required properties
        foreach (var (envVar, propName) in setup.RequiredProperties)
        {
            var value = GetPropertyValueAsString(config, propName);
            connector.WithEnvironment($"{setup.EnvironmentPrefix}{envVar}", value);
        }

        // Add optional properties
        foreach (var (envVar, propName) in setup.OptionalProperties)
        {
            var value = GetPropertyValueAsString(config, propName);
            if (!string.IsNullOrEmpty(value))
            {
                connector.WithEnvironment($"{setup.EnvironmentPrefix}{envVar}", value);
            }
        }

        return connector;
    }
}
