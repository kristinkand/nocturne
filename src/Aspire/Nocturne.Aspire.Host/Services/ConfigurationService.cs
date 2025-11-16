using System.Reflection;
using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Nocturne.Aspire.Host.Constants;
using Nocturne.Connectors.Core.Interfaces;
using Nocturne.Connectors.Core.Models;
using Nocturne.Connectors.Core.Services;
using Nocturne.Core.Constants;

namespace Nocturne.Aspire.Host.Services;

public class ConfigurationService
{
    private readonly IConfiguration _configuration;
    private readonly string _solutionRoot;

    public ConfigurationService(string solutionRoot)
    {
        _solutionRoot = solutionRoot;

        var builder = new ConfigurationBuilder()
            .SetBasePath(solutionRoot)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile("appsettings.Development.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables();

        _configuration = builder.Build();
    }

    public T? GetSection<T>(string sectionName)
        where T : class
    {
        return _configuration.GetSection(sectionName).Get<T>();
    }

    public string? GetValue(string key)
    {
        return _configuration[key];
    }

    public bool HasSection(string sectionName)
    {
        return _configuration.GetSection(sectionName).Exists();
    }

    public bool IsValueSet(string key)
    {
        var value = _configuration[key];
        return !string.IsNullOrWhiteSpace(value);
    }

    public bool IsSectionEnabled(
        string sectionName,
        string enabledKey = ServiceNames.ConfigKeys.EnabledKey
    )
    {
        var section = _configuration.GetSection(sectionName);
        return section.Exists() && section.GetValue<bool>(enabledKey, false);
    }

    public CompatibilityProxyConfig? GetCompatibilityProxyConfig() =>
        GetSection<CompatibilityProxyConfig>(ServiceNames.ConfigKeys.CompatibilityProxySection);

    public ConnectorSettingsConfig? GetConnectorSettings() =>
        GetSection<ConnectorSettingsConfig>(ServiceNames.ConfigKeys.ConnectorSettingsSection);

    public NotificationSettingsConfig? GetNotificationSettings() =>
        GetSection<NotificationSettingsConfig>(ServiceNames.ConfigKeys.NotificationSettingsSection);

    public NightscoutSettingsConfig? GetNightscoutSettings() =>
        GetSection<NightscoutSettingsConfig>(ServiceNames.ConfigKeys.NightscoutSettingsSection);

    public OpenTelemetryConfig? GetOpenTelemetryConfig() =>
        GetSection<OpenTelemetryConfig>(ServiceNames.ConfigKeys.OpenTelemetrySection);

    public IConnectorConfiguration? CreateConnectorConfiguration(string connectSource)
    {
        if (string.IsNullOrWhiteSpace(connectSource))
            return null;

        try
        {
            var source = ConnectorConfigurationFactory.ParseConnectSource(connectSource);
            var config = CreateConnectorConfigurationByReflection(source);

            if (config != null)
            {
                PopulateConnectorConfigurationFromSettings(config, source);
            }

            return config;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }

    private static IConnectorConfiguration? CreateConnectorConfigurationByReflection(
        ConnectSource source
    )
    {
        if (!ConnectorConfigurationConstants.TypeMapping.TryGetValue(source, out var typeName))
        {
            throw new ArgumentException($"Unsupported connector source: {source}");
        }

        try
        {
            var type = Type.GetType(typeName, throwOnError: true);
            if (type == null)
            {
                throw new InvalidOperationException($"Could not load type: {typeName}");
            }

            var instance = Activator.CreateInstance(type);
            return instance as IConnectorConfiguration;
        }
        catch (Exception ex)
        {
            throw new InvalidOperationException(
                $"Failed to create connector configuration for {source}: {ex.Message}",
                ex
            );
        }
    }

    public IEnumerable<IConnectorConfiguration> GetAllEnabledConnectorConfigurations()
    {
        var connectorSettings = GetConnectorSettings();
        if (connectorSettings == null)
        {
            yield break;
        }

        var connectorConfigs = new Dictionary<
            string,
            (bool Enabled, Func<IConnectorConfiguration?> Creator)
        >
        {
            {
                "glooko",
                (
                    connectorSettings.Glooko?.Enabled ?? false,
                    () => CreateConnectorConfiguration("glooko")
                )
            },
            {
                "dexcom",
                (
                    connectorSettings.DexcomShare?.Enabled ?? false,
                    () => CreateConnectorConfiguration("dexcom")
                )
            },
            {
                "librelinkup",
                (
                    connectorSettings.LibreLinkUp?.Enabled ?? false,
                    () => CreateConnectorConfiguration("librelinkup")
                )
            },
            {
                "carelink",
                (
                    connectorSettings.MiniMedCareLink?.Enabled ?? false,
                    () => CreateConnectorConfiguration("carelink")
                )
            },
        };

        foreach (var (key, (enabled, creator)) in connectorConfigs)
        {
            if (enabled)
            {
                var config = creator();
                if (config != null)
                {
                    yield return config;
                }
            }
        }
    }

    private void PopulateConnectorConfigurationFromSettings(
        IConnectorConfiguration config,
        ConnectSource source
    )
    {
        var connectorSettings = GetConnectorSettings();
        var nightscoutSettings = GetNightscoutSettings();

        if (nightscoutSettings != null)
        {
            config.NightscoutUrl = nightscoutSettings.BaseUrl;
            config.ApiSecret = nightscoutSettings.ApiSecret;
        }

        switch (source)
        {
            case ConnectSource.Glooko:
                if (connectorSettings?.Glooko != null)
                {
                    SetConfigProperty(
                        config,
                        ConnectorConfigurationConstants.Glooko.Email,
                        connectorSettings.Glooko.Email
                    );
                    SetConfigProperty(
                        config,
                        ConnectorConfigurationConstants.Glooko.Password,
                        connectorSettings.Glooko.Password
                    );
                    SetConfigProperty(
                        config,
                        ConnectorConfigurationConstants.Glooko.Server,
                        connectorSettings.Glooko.Server
                    );
                    SetConfigProperty(
                        config,
                        ConnectorConfigurationConstants.Glooko.TimezoneOffset,
                        connectorSettings.Glooko.TimezoneOffset
                    );
                }
                break;

            case ConnectSource.Dexcom:
                if (connectorSettings?.DexcomShare != null)
                {
                    SetConfigProperty(
                        config,
                        ConnectorConfigurationConstants.Dexcom.Username,
                        connectorSettings.DexcomShare.Username
                    );
                    SetConfigProperty(
                        config,
                        ConnectorConfigurationConstants.Dexcom.Password,
                        connectorSettings.DexcomShare.Password
                    );
                    SetConfigProperty(
                        config,
                        ConnectorConfigurationConstants.Dexcom.Region,
                        connectorSettings.DexcomShare.Region
                    );
                    SetConfigProperty(
                        config,
                        ConnectorConfigurationConstants.Dexcom.Server,
                        connectorSettings.DexcomShare.Server
                    );
                }
                break;

            case ConnectSource.LibreLinkUp:
                if (connectorSettings?.LibreLinkUp != null)
                {
                    SetConfigProperty(
                        config,
                        ConnectorConfigurationConstants.LibreLinkUp.Username,
                        connectorSettings.LibreLinkUp.Username
                    );
                    SetConfigProperty(
                        config,
                        ConnectorConfigurationConstants.LibreLinkUp.Password,
                        connectorSettings.LibreLinkUp.Password
                    );
                    SetConfigProperty(
                        config,
                        ConnectorConfigurationConstants.LibreLinkUp.Region,
                        connectorSettings.LibreLinkUp.Region
                    );
                    SetConfigProperty(
                        config,
                        ConnectorConfigurationConstants.LibreLinkUp.PatientId,
                        connectorSettings.LibreLinkUp.PatientId
                    );
                }
                break;

            case ConnectSource.CareLink:
                if (connectorSettings?.MiniMedCareLink != null)
                {
                    SetConfigProperty(
                        config,
                        ConnectorConfigurationConstants.CareLink.Username,
                        connectorSettings.MiniMedCareLink.Username
                    );
                    SetConfigProperty(
                        config,
                        ConnectorConfigurationConstants.CareLink.Password,
                        connectorSettings.MiniMedCareLink.Password
                    );
                    SetConfigProperty(
                        config,
                        ConnectorConfigurationConstants.CareLink.Region,
                        connectorSettings.MiniMedCareLink.Region
                    );
                    SetConfigProperty(
                        config,
                        ConnectorConfigurationConstants.CareLink.CountryCode,
                        connectorSettings.MiniMedCareLink.CountryCode
                    );
                }
                break;
        }
    }

    private static void SetConfigProperty(
        IConnectorConfiguration config,
        string propertyName,
        object? value
    )
    {
        if (config == null || string.IsNullOrEmpty(propertyName))
            return;

        try
        {
            var property = config.GetType().GetProperty(propertyName);
            if (property != null && property.CanWrite)
            {
                property.SetValue(config, value);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine(
                $"Warning: Could not set property {propertyName} on {config.GetType().Name}: {ex.Message}"
            );
        }
    }

    public Type? GetConnectorConfigurationType(string connectSource)
    {
        if (string.IsNullOrWhiteSpace(connectSource))
            return null;

        try
        {
            var source = ConnectorConfigurationFactory.ParseConnectSource(connectSource);
            if (ConnectorConfigurationConstants.TypeMapping.TryGetValue(source, out var typeName))
            {
                return Type.GetType(typeName, throwOnError: true);
            }
            return null;
        }
        catch (ArgumentException)
        {
            return null;
        }
    }
}

// Configuration models matching appsettings.json structure

public class CompatibilityProxyConfig
{
    public string NightscoutUrl { get; set; } = string.Empty;
    public string NocturneUrl { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; }
    public int RetryAttempts { get; set; }
    public string DefaultStrategy { get; set; } = string.Empty;
    public bool EnableDetailedLogging { get; set; }
}

public class ConnectorSettingsConfig
{
    public GlookoConfig? Glooko { get; set; }
    public DexcomShareConfig? DexcomShare { get; set; }
    public LibreLinkUpConfig? LibreLinkUp { get; set; }
    public MiniMedCareLinkConfig? MiniMedCareLink { get; set; }
}

public class GlookoConfig
{
    public bool Enabled { get; set; }
    public string Email { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
    public int TimezoneOffset { get; set; }
}

public class DexcomShareConfig
{
    public bool Enabled { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string Server { get; set; } = string.Empty;
}

public class LibreLinkUpConfig
{
    public bool Enabled { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string PatientId { get; set; } = string.Empty;
}

public class MiniMedCareLinkConfig
{
    public bool Enabled { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string Region { get; set; } = string.Empty;
    public string CountryCode { get; set; } = string.Empty;
}

public class NotificationSettingsConfig
{
    public PushoverConfig? Pushover { get; set; }
    public EmailConfig? Email { get; set; }
}

public class PushoverConfig
{
    public bool Enabled { get; set; }
    public string ApiToken { get; set; } = string.Empty;
    public string UserKey { get; set; } = string.Empty;
}

public class EmailConfig
{
    public bool Enabled { get; set; }
    public string SmtpServer { get; set; } = string.Empty;
    public int SmtpPort { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string FromAddress { get; set; } = string.Empty;
    public string ToAddress { get; set; } = string.Empty;
}

public class NightscoutSettingsConfig
{
    public string ApiSecret { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = string.Empty;
    public string DefaultUnits { get; set; } = string.Empty;
    public int TimeFormat { get; set; }
    public string Language { get; set; } = string.Empty;
    public string Theme { get; set; } = string.Empty;
}

public class OpenTelemetryConfig
{
    public string ServiceName { get; set; } = string.Empty;
    public string ServiceVersion { get; set; } = string.Empty;
    public TracingConfig? Tracing { get; set; }
    public MetricsConfig? Metrics { get; set; }
}

public class TracingConfig
{
    public bool Enabled { get; set; }
    public string[] Exporters { get; set; } = Array.Empty<string>();
    public string OtlpEndpoint { get; set; } = string.Empty;
}

public class MetricsConfig
{
    public bool Enabled { get; set; }
    public string[] Exporters { get; set; } = Array.Empty<string>();
}
