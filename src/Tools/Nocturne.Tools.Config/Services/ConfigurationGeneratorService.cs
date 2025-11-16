using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Logging;
using Nocturne.API.Configuration;
using Nocturne.Core.Constants;
using Nocturne.Infrastructure.Cache.Configuration;
using Nocturne.Tools.Abstractions.Services;
using Nocturne.Tools.Config.Configuration;

namespace Nocturne.Tools.Config.Services;

/// <summary>
/// Service for generating configuration files for Nocturne.
/// </summary>
public class ConfigurationGeneratorService
{
    private readonly ILogger<ConfigurationGeneratorService> _logger;
    private readonly IProgressReporter _progressReporter;
    private readonly JsonSerializerOptions _jsonOptions;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationGeneratorService"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="progressReporter">The progress reporter.</param>
    public ConfigurationGeneratorService(
        ILogger<ConfigurationGeneratorService> logger,
        IProgressReporter progressReporter
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _progressReporter =
            progressReporter ?? throw new ArgumentNullException(nameof(progressReporter));

        _jsonOptions = new JsonSerializerOptions
        {
            WriteIndented = true,
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };
    }

    /// <summary>
    /// Generates a configuration file based on the provided configuration.
    /// </summary>
    /// <param name="config">The configuration for generation.</param>
    /// <param name="cancellationToken">The cancellation token.</param>
    /// <returns>A task representing the asynchronous operation.</returns>
    public async Task GenerateConfigurationAsync(
        ConfigConfiguration config,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogInformation("Generating configuration file: {OutputPath}", config.OutputPath);

        _progressReporter.ReportProgress(
            new ProgressInfo("Generation", 1, 5, "Analyzing configuration classes")
        );

        // Check if file exists and handle overwrite logic
        if (File.Exists(config.OutputPath) && !config.OverwriteExisting)
        {
            throw new InvalidOperationException(
                $"Output file '{config.OutputPath}' already exists. Use --overwrite to replace it."
            );
        }

        _progressReporter.ReportProgress(
            new ProgressInfo("Generation", 2, 5, "Building configuration structure")
        );

        var configData = await BuildConfigurationAsync(config, cancellationToken);

        _progressReporter.ReportProgress(
            new ProgressInfo("Generation", 3, 5, "Formatting configuration")
        );

        string content = config.Format switch
        {
            ConfigFormat.Json => await FormatAsJsonAsync(configData, config),
            ConfigFormat.EnvironmentVariables => await FormatAsEnvironmentVariablesAsync(
                configData,
                config
            ),
            ConfigFormat.Yaml => await FormatAsYamlAsync(configData, config),
            _ => throw new NotSupportedException(
                $"Configuration format '{config.Format}' is not supported"
            ),
        };

        _progressReporter.ReportProgress(
            new ProgressInfo("Generation", 4, 5, "Writing configuration file")
        );

        await File.WriteAllTextAsync(config.OutputPath, content, cancellationToken);

        _progressReporter.ReportProgress(
            new ProgressInfo("Generation", 5, 5, "Configuration generation completed")
        );

        _logger.LogInformation(
            "Configuration file generated successfully: {OutputPath}",
            config.OutputPath
        );
    }

    private async Task<Dictionary<string, object>> BuildConfigurationAsync(
        ConfigConfiguration config,
        CancellationToken cancellationToken
    )
    {
        var exampleConfig = new Dictionary<string, object>();

        // Add configuration sections
        AddConnectionStrings(exampleConfig);
        AddAspireConfiguration(exampleConfig);
        AddLoggingConfiguration(exampleConfig);
        AddJwtSettings(exampleConfig);
        AddProxyConfiguration(exampleConfig);
        AddKestrelConfiguration(exampleConfig);
        AddReverseProxyConfiguration(exampleConfig);
        AddMiscellaneousSettings(exampleConfig);

        return exampleConfig;
    }

    private async Task<string> FormatAsJsonAsync(
        Dictionary<string, object> configData,
        ConfigConfiguration config
    )
    {
        if (config.IncludeComments)
        {
            // Add comments as special entries that will be formatted
            var commentedConfig = AddCommentsToConfig(configData);
            return JsonSerializer.Serialize(commentedConfig, _jsonOptions);
        }

        return JsonSerializer.Serialize(configData, _jsonOptions);
    }

    private async Task<string> FormatAsEnvironmentVariablesAsync(
        Dictionary<string, object> configData,
        ConfigConfiguration config
    )
    {
        var envVars = new List<string>();

        if (config.IncludeComments)
        {
            envVars.Add("# Nocturne Configuration - Environment Variables");
            envVars.Add($"# Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            envVars.Add("");
        }

        FlattenConfigToEnvironmentVariables(configData, "", envVars);

        return string.Join(Environment.NewLine, envVars);
    }

    private async Task<string> FormatAsYamlAsync(
        Dictionary<string, object> configData,
        ConfigConfiguration config
    )
    {
        // For now, we'll provide a simple YAML representation
        // In a full implementation, you'd use a YAML library like YamlDotNet
        var yaml = new List<string>();

        if (config.IncludeComments)
        {
            yaml.Add("# Nocturne Configuration - YAML");
            yaml.Add($"# Generated on {DateTime.Now:yyyy-MM-dd HH:mm:ss}");
            yaml.Add("");
        }

        ConvertToYaml(configData, yaml, 0);

        return string.Join(Environment.NewLine, yaml);
    }

    private Dictionary<string, object> AddCommentsToConfig(Dictionary<string, object> config)
    {
        // Add helpful comments as special keys
        var commented = new Dictionary<string, object>
        {
            ["_comments"] = new Dictionary<string, object>
            {
                ["description"] = "Nocturne Configuration Example",
                ["generated"] = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"),
                ["note"] = "Replace example values with your actual configuration",
            },
        };

        foreach (var kvp in config)
        {
            commented[kvp.Key] = kvp.Value;
        }

        return commented;
    }

    private void FlattenConfigToEnvironmentVariables(
        object obj,
        string prefix,
        List<string> envVars
    )
    {
        if (obj is Dictionary<string, object> dict)
        {
            foreach (var kvp in dict)
            {
                var key = string.IsNullOrEmpty(prefix) ? kvp.Key : $"{prefix}__{kvp.Key}";
                FlattenConfigToEnvironmentVariables(kvp.Value, key, envVars);
            }
        }
        else if (obj is object[] array)
        {
            for (int i = 0; i < array.Length; i++)
            {
                var key = $"{prefix}__{i}";
                FlattenConfigToEnvironmentVariables(array[i], key, envVars);
            }
        }
        else
        {
            envVars.Add($"{prefix.Replace("__", "__").ToUpperInvariant()}={obj}");
        }
    }

    private void ConvertToYaml(object obj, List<string> yaml, int indent)
    {
        var indentStr = new string(' ', indent * 2);

        if (obj is Dictionary<string, object> dict)
        {
            foreach (var kvp in dict)
            {
                yaml.Add($"{indentStr}{kvp.Key}:");
                ConvertToYaml(kvp.Value, yaml, indent + 1);
            }
        }
        else if (obj is object[] array)
        {
            foreach (var item in array)
            {
                yaml.Add($"{indentStr}- ");
                ConvertToYaml(item, yaml, indent + 1);
            }
        }
        else
        {
            yaml[yaml.Count - 1] += $" {obj}";
        }
    }

    // Include all the configuration methods from the original ConfigurationGenerator
    private void AddConnectionStrings(Dictionary<string, object> config)
    {
        config["ConnectionStrings"] = new Dictionary<string, object>
        {
            // In-memory cache is used by default, no connection string needed
        };
    }

    private void AddAspireConfiguration(Dictionary<string, object> config)
    {
        // Aspire configuration for service discovery can be added here if needed
    }

    private void AddLoggingConfiguration(Dictionary<string, object> config)
    {
        config["Logging"] = new Dictionary<string, object>
        {
            ["LogLevel"] = new Dictionary<string, object>
            {
                ["Default"] = "Information",
                ["Microsoft.AspNetCore"] = "Warning",
                ["Microsoft.Hosting.Lifetime"] = "Information",
                ["Microsoft.Extensions.Hosting"] = "Information",
                ["Yarp"] = "Information",
                ["Nocturne"] = "Information",
            },
            ["Console"] = new Dictionary<string, object>
            {
                ["IncludeScopes"] = false,
                ["LogLevel"] = new Dictionary<string, object> { ["Default"] = "Information" },
            },
        };
    }

    private void AddJwtSettings(Dictionary<string, object> config)
    {
        config["JwtSettings"] = new Dictionary<string, object>
        {
            ["SecretKey"] = "YourJWTSecretKeyShouldBeAtLeast32CharactersLongForSecurityReasons",
            ["Issuer"] = "Nocturne",
            ["Audience"] = "NightscoutClient",
            ["ExpirationHours"] = 24,
        };
    }

    private void AddProxyConfiguration(Dictionary<string, object> config)
    {
        config["Proxy"] = new Dictionary<string, object>
        {
            ["Enabled"] = false,
            ["TargetUrl"] = "https://your-nightscout-instance.herokuapp.com",
            ["TimeoutSeconds"] = 30,
            ["RetryAttempts"] = 3,
            ["Authentication"] = new Dictionary<string, object>
            {
                ["ForwardAuthHeaders"] = true,
                ["DefaultApiSecret"] = "your-api-secret-here",
            },
        };
    }

    private void AddKestrelConfiguration(Dictionary<string, object> config)
    {
        config["Kestrel"] = new Dictionary<string, object>
        {
            ["Endpoints"] = new Dictionary<string, object>
            {
                ["Http"] = new Dictionary<string, object> { ["Url"] = "http://localhost:1337" },
                ["Https"] = new Dictionary<string, object> { ["Url"] = "https://localhost:1612" },
            },
        };
    }

    private void AddReverseProxyConfiguration(Dictionary<string, object> config)
    {
        config["ReverseProxy"] = new Dictionary<string, object>
        {
            ["Routes"] = new Dictionary<string, object>
            {
                ["nightscout"] = new Dictionary<string, object>
                {
                    ["ClusterId"] = "nightscout",
                    ["Match"] = new Dictionary<string, object> { ["Path"] = "{**catch-all}" },
                    ["Transforms"] = new object[]
                    {
                        new Dictionary<string, object>
                        {
                            ["RequestHeader"] = "X-Forwarded-Proto",
                            ["Set"] = "https",
                        },
                    },
                },
            },
            ["Clusters"] = new Dictionary<string, object>
            {
                ["nightscout"] = new Dictionary<string, object>
                {
                    ["Destinations"] = new Dictionary<string, object>
                    {
                        ["target"] = new Dictionary<string, object>
                        {
                            ["Address"] = "https://your-nightscout-instance.herokuapp.com/",
                        },
                    },
                    ["HealthCheck"] = new Dictionary<string, object>
                    {
                        ["Active"] = new Dictionary<string, object>
                        {
                            ["Enabled"] = true,
                            ["Interval"] = "00:00:30",
                            ["Timeout"] = "00:00:05",
                            ["Policy"] = "ConsecutiveFailures",
                            ["Path"] = "/api/v1/status",
                        },
                    },
                },
            },
        };
    }


    private void AddMiscellaneousSettings(Dictionary<string, object> config)
    {
        // Add basic ASP.NET Core settings
        config["AllowedHosts"] = "*";

        // Add API configuration
        config["ApiSettings"] = new Dictionary<string, object>
        {
            ["DefaultPageSize"] = 50,
            ["MaxPageSize"] = 1000,
            ["EnableSwagger"] = true,
            ["EnableCors"] = true,
            ["RateLimiting"] = new Dictionary<string, object>
            {
                ["Enabled"] = false,
                ["RequestsPerMinute"] = 100,
            },
        };

        // Add Nightscout-specific settings
        config["NightscoutSettings"] = new Dictionary<string, object>
        {
            ["ApiSecret"] = "your-nightscout-api-secret",
            ["BaseUrl"] = "https://your-nightscout-instance.herokuapp.com",
            ["DefaultUnits"] = "mg/dl",
            ["TimeFormat"] = 12,
            ["Language"] = "en",
            ["Theme"] = "default",
            ["ShowPlugins"] = new[] { "delta", "direction", "timeago", "devicestatus" },
            ["Alarms"] = new Dictionary<string, object>
            {
                ["UrgentHigh"] = new Dictionary<string, object>
                {
                    ["Enabled"] = true,
                    ["Threshold"] = 400,
                    ["Minutes"] = new[] { 15, 30, 60 },
                },
                ["High"] = new Dictionary<string, object>
                {
                    ["Enabled"] = true,
                    ["Threshold"] = 260,
                    ["Minutes"] = new[] { 30, 60 },
                },
                ["Low"] = new Dictionary<string, object>
                {
                    ["Enabled"] = true,
                    ["Threshold"] = 55,
                    ["Minutes"] = new[] { 15, 30, 45, 60 },
                },
                ["UrgentLow"] = new Dictionary<string, object>
                {
                    ["Enabled"] = true,
                    ["Threshold"] = 39,
                    ["Minutes"] = new[] { 5, 10, 15, 30 },
                },
            },
            ["Thresholds"] = new Dictionary<string, object>
            {
                ["High"] = 260,
                ["TargetTop"] = 180,
                ["TargetBottom"] = 80,
                ["Low"] = 55,
            },
        };

        // Add notification settings
        config["NotificationSettings"] = new Dictionary<string, object>
        {
            ["Pushover"] = new Dictionary<string, object>
            {
                ["Enabled"] = false,
                ["ApiToken"] = "your-pushover-api-token",
                ["UserKey"] = "your-pushover-user-key",
                ["Sounds"] = new Dictionary<string, object>
                {
                    ["WARN"] = "default",
                    ["URGENT"] = "siren",
                    ["INFO"] = "none",
                },
            },
            ["Email"] = new Dictionary<string, object>
            {
                ["Enabled"] = false,
                ["SmtpServer"] = "smtp.example.com",
                ["SmtpPort"] = 587,
                ["Username"] = "your-email@example.com",
                ["Password"] = "your-email-password",
                ["FromAddress"] = "nightscout@example.com",
                ["ToAddress"] = "alerts@example.com",
            },
        };

        // Add connector settings for data sources
        config["ConnectorSettings"] = new Dictionary<string, object>
        {
            ["Glooko"] = new Dictionary<string, object>
            {
                ["Enabled"] = false,
                ["Email"] = "your-glooko-email@example.com",
                ["Password"] = "your-glooko-password",
                ["Server"] = "eu.api.glooko.com",
                ["TimezoneOffset"] = 0,
            },
            ["DexcomShare"] = new Dictionary<string, object>
            {
                ["Enabled"] = false,
                ["Username"] = "your-dexcom-username",
                ["Password"] = "your-dexcom-password",
                ["Region"] = "us",
                ["Server"] = "share2.dexcom.com",
            },
            ["LibreLinkUp"] = new Dictionary<string, object>
            {
                ["Enabled"] = false,
                ["Username"] = "your-libre-username",
                ["Password"] = "your-libre-password",
                ["Region"] = "EU",
                ["PatientId"] = "your-patient-id",
            },
            ["MiniMedCareLink"] = new Dictionary<string, object>
            {
                ["Enabled"] = false,
                ["Username"] = "your-carelink-username",
                ["Password"] = "your-carelink-password",
                ["Region"] = "us",
                ["CountryCode"] = "US",
            },
        };

        // Add health check settings
        config["HealthChecks"] = new Dictionary<string, object>
        {
            ["Enabled"] = true,
            ["DetailedErrors"] = false,
            ["Checks"] = new Dictionary<string, object>
            {
                ["Database"] = new Dictionary<string, object>
                {
                    ["Enabled"] = true,
                    ["Timeout"] = "00:00:30",
                },
            },
        };

        // Add OpenTelemetry settings
        config["OpenTelemetry"] = new Dictionary<string, object>
        {
            ["ServiceName"] = "Nocturne",
            ["ServiceVersion"] = "1.0.0",
            ["Tracing"] = new Dictionary<string, object>
            {
                ["Enabled"] = false,
                ["Exporters"] = new[] { "console", "otlp" },
                ["OtlpEndpoint"] = "http://localhost:4317",
            },
            ["Metrics"] = new Dictionary<string, object>
            {
                ["Enabled"] = false,
                ["Exporters"] = new[] { "console", "otlp" },
            },
        };
    }
}
