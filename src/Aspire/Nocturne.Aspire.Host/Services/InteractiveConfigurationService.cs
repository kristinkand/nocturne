using Aspire.Hosting;
using Microsoft.Extensions.Hosting;
using Nocturne.Aspire.Host.Services;
using Nocturne.Core.Constants;
using Spectre.Console;

namespace Nocturne.Aspire.Host.Services;

public class InteractiveConfigurationService
{
    private readonly ConfigurationService _configService;
    private readonly IDistributedApplicationBuilder _builder;

    public InteractiveConfigurationService(
        IDistributedApplicationBuilder builder,
        ConfigurationService configService
    )
    {
        _configService = configService;
        _builder = builder;
    }

    public Task<Dictionary<string, string>> ConfigureServicesAsync()
    {
        var parameters = new Dictionary<string, string>();

        AnsiConsole.Write(new FigletText("Nocturne").LeftJustified().Color(Color.Blue));

        AnsiConsole.Write(new Rule("[bold blue]Interactive Configuration[/]").RuleStyle("grey"));
        AnsiConsole.WriteLine();

        // Always configure basic database parameters from config or defaults
        ConfigureDatabaseParameters(parameters);

        // Configure optional services based on user preferences
        ConfigureCompatibilityProxyService(parameters);
        ConfigureConnectorServices(parameters);
        ConfigureNotificationServices(parameters);
        ConfigureOpenTelemetry(parameters);

        AnsiConsole.WriteLine();
        AnsiConsole.MarkupLine("[green]✅ Configuration complete! Starting services...[/]");
        AnsiConsole.WriteLine();

        return Task.FromResult(parameters);
    }

    public Dictionary<string, string> GetDefaultConfiguration()
    {
        var parameters = new Dictionary<string, string>();

        // Configure basic database parameters from config or defaults
        ConfigureDatabaseParameters(parameters);

        // Configure optional services with defaults (non-interactive)
        ConfigureCompatibilityProxyServiceNonInteractive(parameters);
        ConfigureConnectorServicesNonInteractive(parameters);
        ConfigureNotificationServicesNonInteractive(parameters);
        ConfigureOpenTelemetryNonInteractive(parameters);

        Console.WriteLine("Using default configuration for non-interactive mode...");

        return parameters;
    }

    private void ConfigureDatabaseParameters(Dictionary<string, string> parameters)
    {
        // Get database config from appsettings or use defaults
        var postgresConfig = _configService.GetSection<PostgreSqlConfig>(
            ServiceNames.ConfigKeys.PostgreSqlSection
        );

        // PostgreSQL parameters
        parameters[ServiceNames.Parameters.PostgresUsername] =
            ExtractUsernameFromConnectionString(postgresConfig?.ConnectionString)
            ?? ServiceNames.Defaults.PostgresUser;
        parameters[ServiceNames.Parameters.PostgresPassword] =
            ExtractPasswordFromConnectionString(postgresConfig?.ConnectionString)
            ?? ServiceNames.Defaults.PostgresPassword;
        parameters[ServiceNames.Parameters.PostgresDbName] =
            ExtractDatabaseFromConnectionString(postgresConfig?.ConnectionString)
            ?? ServiceNames.Defaults.PostgresDatabase;
    }

    private void ConfigureCompatibilityProxyService(Dictionary<string, string> parameters)
    {
        var compatibilityProxyConfig = _configService.GetCompatibilityProxyConfig();

        if (!string.IsNullOrWhiteSpace(compatibilityProxyConfig?.NightscoutUrl))
        {
            // Compatibility Proxy is already configured
            parameters[ServiceNames.Parameters.CompatibilityProxyTargetUrl] =
                compatibilityProxyConfig.NightscoutUrl;
            AnsiConsole.MarkupLine(
                $"[blue]🔄 Compatibility Proxy:[/] Configured from settings - [cyan]{compatibilityProxyConfig.NightscoutUrl}[/]"
            );
            return;
        }

        AnsiConsole.Write(new Rule("[blue]🔄 Nightscout Compatibility Proxy Configuration[/]"));
        AnsiConsole.MarkupLine(
            "[dim]The compatibility proxy can forward requests to Nightscout for migration or A/B testing.[/]"
        );

        var enableCompatibilityProxy = AnsiConsole.Confirm(
            "Would you like to enable Nightscout forwarding/comparison?",
            false
        );

        if (enableCompatibilityProxy)
        {
            var targetUrl = AnsiConsole.Ask<string>(
                "Enter your [cyan]Nightscout URL[/] (e.g., https://your-nightscout.herokuapp.com):"
            );

            if (!string.IsNullOrWhiteSpace(targetUrl))
            {
                parameters[ServiceNames.Parameters.CompatibilityProxyTargetUrl] = targetUrl;
                AnsiConsole.MarkupLine("[green]✅ Compatibility Proxy configured[/]");
            }
            else
            {
                parameters[ServiceNames.Parameters.CompatibilityProxyTargetUrl] = "";
                AnsiConsole.MarkupLine("[red]❌ Invalid URL, compatibility proxy disabled[/]");
            }
        }
        else
        {
            parameters[ServiceNames.Parameters.CompatibilityProxyTargetUrl] = "";
            AnsiConsole.MarkupLine("[yellow]⏭️  Compatibility proxy disabled[/]");
        }

        Console.WriteLine();
    }

    private void ConfigureConnectorServices(Dictionary<string, string> parameters)
    {
        var connectorSettings = _configService.GetConnectorSettings();

        // Check if any connectors are already enabled in config
        var hasEnabledConnectors =
            connectorSettings?.Glooko?.Enabled == true
            || connectorSettings?.DexcomShare?.Enabled == true
            || connectorSettings?.LibreLinkUp?.Enabled == true
            || connectorSettings?.MiniMedCareLink?.Enabled == true;

        if (hasEnabledConnectors)
        {
            parameters[ServiceNames.ConfigKeys.ConnectorsConfigured] = ServiceNames
                .ConfigKeys
                .TrueValue;
            AnsiConsole.MarkupLine("[blue]🔗 Connectors:[/] Already configured in settings");
            return;
        }

        AnsiConsole.Write(new Rule("[blue]🔗 Data Connector Configuration[/]"));

        AnsiConsole.MarkupLine(
            "You can configure these later in [cyan]appsettings.json[/] if preferred."
        );

        var enableConnectors = AnsiConsole.Confirm(
            "Would you like to configure data connectors (Dexcom, Libre, Glooko, etc.) now?",
            false
        );

        if (enableConnectors)
        {
            ConfigureSpecificConnectors(parameters);
        }
        else
        {
            parameters[ServiceNames.ConfigKeys.ConnectorsConfigured] = ServiceNames
                .ConfigKeys
                .FalseValue;
            AnsiConsole.MarkupLine("[yellow]⏭️  Connectors will be configured later[/]");
        }
    }

    private void ConfigureSpecificConnectors(Dictionary<string, string> parameters)
    {
        var choice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("[yellow]Select connector to configure:[/]")
                .AddChoices(
                    ["Dexcom Share", "LibreLinkUp", "Glooko", "MiniMed CareLink", "Configure later"]
                )
        );

        switch (choice)
        {
            case "Dexcom Share":
                ConfigureDexcomConnector(parameters);
                break;
            case "LibreLinkUp":
                ConfigureLibreConnector(parameters);
                break;
            case "Glooko":
                ConfigureGlookoConnector(parameters);
                break;
            case "MiniMed CareLink":
                ConfigureCareLinkConnector(parameters);
                break;
            default:
                parameters[ServiceNames.ConfigKeys.ConnectorsConfigured] = ServiceNames
                    .ConfigKeys
                    .FalseValue;
                AnsiConsole.MarkupLine("[yellow]⏭️  Connectors will be configured later[/]");
                break;
        }
    }

    private void ConfigureDexcomConnector(Dictionary<string, string> parameters)
    {
        AnsiConsole.Write(new Rule("[blue]📊 Dexcom Share Configuration[/]"));

        var username = AnsiConsole.Ask<string>("Enter [cyan]Dexcom Username[/]:");

        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [cyan]Dexcom Password[/]:").Secret()
        );

        var region = AnsiConsole
            .Prompt(
                new SelectionPrompt<string>()
                    .Title("Select [yellow]region[/]:")
                    .AddChoices(["US", "EU/International"])
            )
            .Replace("EU/International", "eu")
            .Replace("US", "us");

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            parameters["dexcom-username"] = username;
            parameters["dexcom-password"] = password;
            parameters["dexcom-region"] = region;
            parameters[ServiceNames.ConfigKeys.ConnectorsConfigured] = ServiceNames
                .ConfigKeys
                .TrueValue;
            AnsiConsole.MarkupLine("[green]✅ Dexcom connector configured[/]");
        }
        else
        {
            AnsiConsole.MarkupLine(
                "[red]❌ Invalid credentials, Dexcom connector not configured[/]"
            );
        }
    }

    private void ConfigureLibreConnector(Dictionary<string, string> parameters)
    {
        AnsiConsole.Write(new Rule("[blue]📊 LibreLinkUp Configuration[/]"));

        var username = AnsiConsole.Ask<string>("Enter [cyan]LibreLinkUp Username[/]:");

        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [cyan]LibreLinkUp Password[/]:").Secret()
        );

        var regionChoice = AnsiConsole.Prompt(
            new SelectionPrompt<string>()
                .Title("Select [yellow]region[/]:")
                .AddChoices(["Europe", "United States", "Asia Pacific"])
        );

        var region = regionChoice switch
        {
            "United States" => "US",
            "Asia Pacific" => "AP",
            _ => "EU",
        };

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            parameters["libre-username"] = username;
            parameters["libre-password"] = password;
            parameters["libre-region"] = region;
            parameters[ServiceNames.ConfigKeys.ConnectorsConfigured] = ServiceNames
                .ConfigKeys
                .TrueValue;
            AnsiConsole.MarkupLine("[green]✅ LibreLinkUp connector configured[/]");
        }
        else
        {
            AnsiConsole.MarkupLine(
                "[red]❌ Invalid credentials, LibreLinkUp connector not configured[/]"
            );
        }
    }

    private void ConfigureGlookoConnector(Dictionary<string, string> parameters)
    {
        AnsiConsole.Write(new Rule("[blue]📊 Glooko Configuration[/]"));

        var email = AnsiConsole.Ask<string>("Enter [cyan]Glooko Email[/]:");

        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [cyan]Glooko Password[/]:").Secret()
        );

        if (!string.IsNullOrWhiteSpace(email) && !string.IsNullOrWhiteSpace(password))
        {
            parameters["glooko-email"] = email;
            parameters["glooko-password"] = password;
            parameters[ServiceNames.ConfigKeys.ConnectorsConfigured] = ServiceNames
                .ConfigKeys
                .TrueValue;
            AnsiConsole.MarkupLine("[green]✅ Glooko connector configured[/]");
        }
        else
        {
            AnsiConsole.MarkupLine(
                "[red]❌ Invalid credentials, Glooko connector not configured[/]"
            );
        }
    }

    private void ConfigureCareLinkConnector(Dictionary<string, string> parameters)
    {
        AnsiConsole.Write(new Rule("[blue]📊 MiniMed CareLink Configuration[/]"));

        var username = AnsiConsole.Ask<string>("Enter [cyan]CareLink Username[/]:");

        var password = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [cyan]CareLink Password[/]:").Secret()
        );

        if (!string.IsNullOrWhiteSpace(username) && !string.IsNullOrWhiteSpace(password))
        {
            parameters["carelink-username"] = username;
            parameters["carelink-password"] = password;
            parameters[ServiceNames.ConfigKeys.ConnectorsConfigured] = ServiceNames
                .ConfigKeys
                .TrueValue;
            AnsiConsole.MarkupLine("[green]✅ CareLink connector configured[/]");
        }
        else
        {
            AnsiConsole.MarkupLine(
                "[red]❌ Invalid credentials, CareLink connector not configured[/]"
            );
        }
    }

    private void ConfigureNotificationServices(Dictionary<string, string> parameters)
    {
        var notificationSettings = _configService.GetNotificationSettings();

        if (
            notificationSettings?.Pushover?.Enabled == true
            || notificationSettings?.Email?.Enabled == true
        )
        {
            parameters[ServiceNames.ConfigKeys.NotificationsConfigured] = ServiceNames
                .ConfigKeys
                .TrueValue;
            AnsiConsole.MarkupLine("[blue]🔔 Notifications:[/] Already configured in settings");
            return;
        }

        AnsiConsole.Write(new Rule("[blue]🔔 Notification Configuration[/]"));

        var enableNotifications = AnsiConsole.Confirm(
            "Would you like to configure notifications (Pushover, Email) for alerts?",
            false
        );

        if (enableNotifications)
        {
            ConfigurePushover(parameters);
        }
        else
        {
            parameters[ServiceNames.ConfigKeys.NotificationsConfigured] = ServiceNames
                .ConfigKeys
                .FalseValue;
            AnsiConsole.MarkupLine("[yellow]⏭️  Notifications disabled[/]");
        }
    }

    private void ConfigurePushover(Dictionary<string, string> parameters)
    {
        AnsiConsole.Write(new Rule("[blue]🔔 Pushover Configuration[/]"));

        var apiToken = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [cyan]Pushover API Token[/]:").Secret()
        );

        var userKey = AnsiConsole.Prompt(
            new TextPrompt<string>("Enter [cyan]Pushover User Key[/]:").Secret()
        );

        if (!string.IsNullOrWhiteSpace(apiToken) && !string.IsNullOrWhiteSpace(userKey))
        {
            parameters[ServiceNames.Parameters.PushoverApiToken] = apiToken;
            parameters[ServiceNames.Parameters.PushoverUserKey] = userKey;
            parameters[ServiceNames.ConfigKeys.NotificationsConfigured] = ServiceNames
                .ConfigKeys
                .TrueValue;
            AnsiConsole.MarkupLine("[green]✅ Pushover configured[/]");
        }
        else
        {
            AnsiConsole.MarkupLine("[red]❌ Invalid credentials, Pushover not configured[/]");
        }
    }

    private void ConfigureOpenTelemetry(Dictionary<string, string> parameters)
    {
        var otelConfig = _configService.GetOpenTelemetryConfig();

        if (otelConfig?.Tracing?.Enabled == true || otelConfig?.Metrics?.Enabled == true)
        {
            parameters[ServiceNames.ConfigKeys.TelemetryConfigured] = ServiceNames
                .ConfigKeys
                .TrueValue;
            AnsiConsole.MarkupLine("[blue]📊 OpenTelemetry:[/] Already configured in settings");
            return;
        }

        AnsiConsole.Write(new Rule("[blue]📊 OpenTelemetry Configuration[/]"));

        var enableTelemetry = AnsiConsole.Confirm(
            "Would you like to enable OpenTelemetry for observability (tracing, metrics)?",
            false
        );

        if (enableTelemetry)
        {
            var endpoint = AnsiConsole.Prompt(
                new TextPrompt<string>("Enter [cyan]OTLP Endpoint[/]:")
                    .DefaultValue(ServiceNames.Defaults.DefaultOtlpEndpoint)
                    .ShowDefaultValue()
            );

            parameters[ServiceNames.Parameters.OtlpEndpoint] = endpoint;
            parameters[ServiceNames.ConfigKeys.TelemetryConfigured] = ServiceNames
                .ConfigKeys
                .TrueValue;
            AnsiConsole.MarkupLine("[green]✅ OpenTelemetry configured[/]");
        }
        else
        {
            parameters[ServiceNames.ConfigKeys.TelemetryConfigured] = ServiceNames
                .ConfigKeys
                .FalseValue;
            AnsiConsole.MarkupLine("[yellow]⏭️  OpenTelemetry disabled[/]");
        }
    }

    // Helper methods for parsing connection strings
    private static string? ExtractUsernameFromConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        var parts = connectionString.Split(';');
        var usernamePart = parts.FirstOrDefault(p =>
            p.Trim().StartsWith("Username=", StringComparison.OrdinalIgnoreCase)
        );
        return usernamePart?.Split('=')[1];
    }

    private static string? ExtractPasswordFromConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        var parts = connectionString.Split(';');
        var passwordPart = parts.FirstOrDefault(p =>
            p.Trim().StartsWith("Password=", StringComparison.OrdinalIgnoreCase)
        );
        return passwordPart?.Split('=')[1];
    }

    private static string? ExtractDatabaseFromConnectionString(string? connectionString)
    {
        if (string.IsNullOrWhiteSpace(connectionString))
            return null;

        var parts = connectionString.Split(';');
        var databasePart = parts.FirstOrDefault(p =>
            p.Trim().StartsWith("Database=", StringComparison.OrdinalIgnoreCase)
        );
        return databasePart?.Split('=')[1];
    }

    // Non-interactive configuration methods
    private void ConfigureCompatibilityProxyServiceNonInteractive(
        Dictionary<string, string> parameters
    )
    {
        var compatibilityProxyConfig = _configService.GetCompatibilityProxyConfig();

        if (!string.IsNullOrWhiteSpace(compatibilityProxyConfig?.NightscoutUrl))
        {
            parameters[ServiceNames.Parameters.CompatibilityProxyTargetUrl] =
                compatibilityProxyConfig.NightscoutUrl;
        }
        else
        {
            // Default: disable compatibility proxy
            parameters[ServiceNames.Parameters.CompatibilityProxyTargetUrl] = "";
        }
    }

    private void ConfigureConnectorServicesNonInteractive(Dictionary<string, string> parameters)
    {
        var connectorSettings = _configService.GetConnectorSettings();

        var hasEnabledConnectors =
            connectorSettings?.Glooko?.Enabled == true
            || connectorSettings?.DexcomShare?.Enabled == true
            || connectorSettings?.LibreLinkUp?.Enabled == true
            || connectorSettings?.MiniMedCareLink?.Enabled == true;

        parameters[ServiceNames.ConfigKeys.ConnectorsConfigured] = hasEnabledConnectors
            ? ServiceNames.ConfigKeys.TrueValue
            : ServiceNames.ConfigKeys.FalseValue;
    }

    private void ConfigureNotificationServicesNonInteractive(Dictionary<string, string> parameters)
    {
        var notificationSettings = _configService.GetNotificationSettings();

        var hasNotifications =
            notificationSettings?.Pushover?.Enabled == true
            || notificationSettings?.Email?.Enabled == true;

        parameters[ServiceNames.ConfigKeys.NotificationsConfigured] = hasNotifications
            ? ServiceNames.ConfigKeys.TrueValue
            : ServiceNames.ConfigKeys.FalseValue;
    }

    private void ConfigureOpenTelemetryNonInteractive(Dictionary<string, string> parameters)
    {
        var otelConfig = _configService.GetOpenTelemetryConfig();

        var hasTelemetry =
            otelConfig?.Tracing?.Enabled == true || otelConfig?.Metrics?.Enabled == true;

        if (hasTelemetry)
        {
            parameters[ServiceNames.ConfigKeys.TelemetryConfigured] = ServiceNames
                .ConfigKeys
                .TrueValue;
        }
        else
        {
            // Default: disable telemetry
            parameters[ServiceNames.ConfigKeys.TelemetryConfigured] = ServiceNames
                .ConfigKeys
                .FalseValue;
        }
    }
}

// Additional config models
public class PostgreSqlConfig
{
    public string ConnectionString { get; set; } = string.Empty;
}
