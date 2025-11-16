using System.Text.Json;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Nocturne.Tools.Abstractions.Configuration;
using Nocturne.Tools.Abstractions.Services;

namespace Nocturne.Tools.Core.Configuration;

/// <summary>
/// Implementation of configuration management for tools.
/// </summary>
public class ConfigurationManager : Abstractions.Configuration.IConfigurationManager
{
    private readonly ILogger<ConfigurationManager> _logger;
    private readonly IValidationService _validationService;

    /// <summary>
    /// Initializes a new instance of the <see cref="ConfigurationManager"/> class.
    /// </summary>
    /// <param name="logger">The logger.</param>
    /// <param name="validationService">The validation service.</param>
    public ConfigurationManager(
        ILogger<ConfigurationManager> logger,
        IValidationService validationService
    )
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _validationService =
            validationService ?? throw new ArgumentNullException(nameof(validationService));
    }

    /// <inheritdoc/>
    public IConfiguration LoadConfiguration(string? configurationPath = null)
    {
        var builder = new ConfigurationBuilder()
            .SetBasePath(Directory.GetCurrentDirectory())
            .AddJsonFile("appsettings.json", optional: true, reloadOnChange: true);

        // Add environment-specific configuration
        var environment = Environment.GetEnvironmentVariable("DOTNET_ENVIRONMENT") ?? "Development";
        builder.AddJsonFile(
            $"appsettings.{environment}.json",
            optional: true,
            reloadOnChange: true
        );

        // Add custom configuration file if specified
        if (!string.IsNullOrEmpty(configurationPath) && File.Exists(configurationPath))
        {
            builder.AddJsonFile(configurationPath, optional: false, reloadOnChange: true);
        }

        // Add environment variables
        builder.AddEnvironmentVariables();

        var configuration = builder.Build();

        _logger.LogDebug(
            "Configuration loaded from {Sources}",
            string.Join(", ", configuration.AsEnumerable().Select(x => x.Key))
        );

        return configuration;
    }

    /// <inheritdoc/>
    public T LoadConfiguration<T>(string? configurationPath = null)
        where T : class, IToolConfiguration, new()
    {
        var configuration = LoadConfiguration(configurationPath);
        var instance = new T();

        configuration.Bind(instance);

        _logger.LogDebug("Configuration bound to type {Type}", typeof(T).Name);

        return instance;
    }

    /// <inheritdoc/>
    public bool ValidateConfiguration(IToolConfiguration configuration)
    {
        var validationResult = _validationService.ValidateObject(configuration);

        if (!validationResult.IsValid)
        {
            _logger.LogError("Configuration validation failed:");
            foreach (var error in validationResult.Errors)
            {
                _logger.LogError(
                    "  {PropertyName}: {ErrorMessage}",
                    error.PropertyName,
                    error.ErrorMessage
                );
            }
            return false;
        }

        // Also validate the configuration using its own validation method
        var configValidationResult = configuration.ValidateConfiguration();
        if (
            configValidationResult != System.ComponentModel.DataAnnotations.ValidationResult.Success
        )
        {
            _logger.LogError(
                "Configuration validation failed: {ErrorMessage}",
                configValidationResult.ErrorMessage
            );
            return false;
        }

        _logger.LogDebug("Configuration validation passed for {ToolName}", configuration.ToolName);
        return true;
    }

    /// <inheritdoc/>
    public async Task CreateConfigurationTemplateAsync(string outputPath, string toolName)
    {
        var template = new
        {
            ToolName = toolName,
            Version = "1.0.0",
            Logging = new { LogLevel = new { Default = "Information", Microsoft = "Warning" } },
            ConnectionStrings = new
            {
                DefaultConnection = "Server=localhost;Database=YourDatabase;Trusted_Connection=true;",
            },
        };

        var json = JsonSerializer.Serialize(
            template,
            new JsonSerializerOptions { WriteIndented = true }
        );

        await File.WriteAllTextAsync(outputPath, json);

        _logger.LogInformation("Configuration template created at {OutputPath}", outputPath);
    }
}
