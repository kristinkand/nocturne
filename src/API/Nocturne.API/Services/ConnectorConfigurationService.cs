using System.Reflection;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Nocturne.API.Hubs;
using Nocturne.Connectors.Core.Extensions;
using Nocturne.Connectors.Core.Services;
using Nocturne.Core.Contracts;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services;

/// <summary>
/// Service for managing connector configurations stored in the database.
/// Handles merging of environment variables (secrets) with database-stored runtime configuration.
/// </summary>
public class ConnectorConfigurationService : IConnectorConfigurationService
{
    private readonly NocturneDbContext _context;
    private readonly ISecretEncryptionService _encryptionService;
    private readonly ISignalRBroadcastService _broadcastService;
    private readonly ILogger<ConnectorConfigurationService> _logger;
    private static readonly JsonSerializerOptions _jsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public ConnectorConfigurationService(
        NocturneDbContext context,
        ISecretEncryptionService encryptionService,
        ISignalRBroadcastService broadcastService,
        ILogger<ConnectorConfigurationService> logger)
    {
        _context = context;
        _encryptionService = encryptionService;
        _broadcastService = broadcastService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ConnectorConfigurationResponse?> GetConfigurationAsync(
        string connectorName,
        bool includeSecrets = false,
        CancellationToken ct = default)
    {
        var entity = await _context.ConnectorConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConnectorName == connectorName, ct);

        if (entity == null)
        {
            _logger.LogDebug("No configuration found for connector {ConnectorName}", connectorName);
            return null;
        }

        var response = new ConnectorConfigurationResponse
        {
            ConnectorName = entity.ConnectorName,
            Configuration = JsonDocument.Parse(entity.ConfigurationJson),
            SchemaVersion = entity.SchemaVersion,
            IsActive = entity.IsActive,
            LastModified = entity.LastModified,
            ModifiedBy = entity.ModifiedBy
        };

        // Note: We don't include secrets in the configuration response.
        // Connectors should call GetSecretsAsync separately for internal use.

        return response;
    }

    /// <inheritdoc />
    public async Task<ConnectorConfigurationResponse> SaveConfigurationAsync(
        string connectorName,
        JsonDocument configuration,
        string? modifiedBy = null,
        CancellationToken ct = default)
    {
        var entity = await _context.ConnectorConfigurations
            .FirstOrDefaultAsync(c => c.ConnectorName == connectorName, ct);

        var configJson = configuration.RootElement.GetRawText();

        if (entity == null)
        {
            entity = new ConnectorConfigurationEntity
            {
                ConnectorName = connectorName,
                ConfigurationJson = configJson,
                SecretsJson = "{}",
                IsActive = true,
                LastModified = DateTimeOffset.UtcNow,
                ModifiedBy = modifiedBy
            };
            _context.ConnectorConfigurations.Add(entity);
            _logger.LogInformation("Creating new configuration for connector {ConnectorName}", connectorName);
        }
        else
        {
            entity.ConfigurationJson = configJson;
            entity.LastModified = DateTimeOffset.UtcNow;
            entity.ModifiedBy = modifiedBy;
            _logger.LogInformation("Updating configuration for connector {ConnectorName}", connectorName);
        }

        await _context.SaveChangesAsync(ct);

        // Broadcast configuration change
        await _broadcastService.BroadcastConfigChangeAsync(new ConfigurationChangeEvent
        {
            ConnectorName = connectorName,
            ChangeType = "updated",
            ModifiedBy = modifiedBy
        });

        return new ConnectorConfigurationResponse
        {
            ConnectorName = entity.ConnectorName,
            Configuration = JsonDocument.Parse(entity.ConfigurationJson),
            SchemaVersion = entity.SchemaVersion,
            IsActive = entity.IsActive,
            LastModified = entity.LastModified,
            ModifiedBy = entity.ModifiedBy
        };
    }

    /// <inheritdoc />
    public async Task SaveSecretsAsync(
        string connectorName,
        Dictionary<string, string> secrets,
        string? modifiedBy = null,
        CancellationToken ct = default)
    {
        if (!_encryptionService.IsConfigured)
        {
            throw new InvalidOperationException(
                "Secret encryption is not configured. Ensure api-secret is set in configuration.");
        }

        var entity = await _context.ConnectorConfigurations
            .FirstOrDefaultAsync(c => c.ConnectorName == connectorName, ct);

        var encryptedSecrets = _encryptionService.EncryptSecrets(secrets);
        var secretsJson = JsonSerializer.Serialize(encryptedSecrets, _jsonOptions);

        if (entity == null)
        {
            entity = new ConnectorConfigurationEntity
            {
                ConnectorName = connectorName,
                ConfigurationJson = "{}",
                SecretsJson = secretsJson,
                IsActive = true,
                LastModified = DateTimeOffset.UtcNow,
                ModifiedBy = modifiedBy
            };
            _context.ConnectorConfigurations.Add(entity);
            _logger.LogInformation("Creating new secrets for connector {ConnectorName}", connectorName);
        }
        else
        {
            entity.SecretsJson = secretsJson;
            entity.LastModified = DateTimeOffset.UtcNow;
            entity.ModifiedBy = modifiedBy;
            _logger.LogInformation("Updating secrets for connector {ConnectorName}", connectorName);
        }

        await _context.SaveChangesAsync(ct);

        // Broadcast secrets update (note: doesn't reveal actual secrets)
        await _broadcastService.BroadcastConfigChangeAsync(new ConfigurationChangeEvent
        {
            ConnectorName = connectorName,
            ChangeType = "secrets_updated",
            ModifiedBy = modifiedBy
        });
    }

    /// <inheritdoc />
    public async Task<Dictionary<string, string>> GetSecretsAsync(
        string connectorName,
        CancellationToken ct = default)
    {
        if (!_encryptionService.IsConfigured)
        {
            _logger.LogWarning("Secret encryption not configured, returning empty secrets for {ConnectorName}", connectorName);
            return new Dictionary<string, string>();
        }

        var entity = await _context.ConnectorConfigurations
            .AsNoTracking()
            .FirstOrDefaultAsync(c => c.ConnectorName == connectorName, ct);

        if (entity == null || string.IsNullOrEmpty(entity.SecretsJson) || entity.SecretsJson == "{}")
        {
            return new Dictionary<string, string>();
        }

        var encryptedSecrets = JsonSerializer.Deserialize<Dictionary<string, string>>(
            entity.SecretsJson, _jsonOptions) ?? new Dictionary<string, string>();

        return _encryptionService.DecryptSecrets(encryptedSecrets);
    }

    /// <inheritdoc />
    public Task<JsonDocument> GetSchemaAsync(string connectorName, CancellationToken ct = default)
    {
        var connectorInfo = ConnectorMetadataService.GetByConnectorId(connectorName);
        if (connectorInfo == null)
        {
            _logger.LogWarning("Unknown connector {ConnectorName}, returning empty schema", connectorName);
            return Task.FromResult(JsonDocument.Parse("{}"));
        }

        // Find the configuration class type
        var configType = FindConfigurationType(connectorName);
        if (configType == null)
        {
            _logger.LogWarning("Could not find configuration type for connector {ConnectorName}", connectorName);
            return Task.FromResult(JsonDocument.Parse("{}"));
        }

        var schema = GenerateSchemaFromType(configType);
        return Task.FromResult(schema);
    }

    /// <inheritdoc />
    public async Task<IReadOnlyList<ConnectorStatusInfo>> GetAllConnectorStatusAsync(CancellationToken ct = default)
    {
        var allConnectors = ConnectorMetadataService.GetAll();
        var dbConfigs = await _context.ConnectorConfigurations
            .AsNoTracking()
            .ToDictionaryAsync(c => c.ConnectorName, ct);

        var result = new List<ConnectorStatusInfo>();

        foreach (var connector in allConnectors)
        {
            var hasDbConfig = dbConfigs.TryGetValue(connector.ConnectorName, out var dbConfig);

            var status = new ConnectorStatusInfo
            {
                ConnectorName = connector.ConnectorName,
                IsEnabled = hasDbConfig && dbConfig!.IsActive,
                HasDatabaseConfig = hasDbConfig,
                HasSecrets = hasDbConfig && !string.IsNullOrEmpty(dbConfig!.SecretsJson) && dbConfig.SecretsJson != "{}",
                LastModified = hasDbConfig ? dbConfig!.LastModified : null
            };

            result.Add(status);
        }

        return result;
    }

    /// <inheritdoc />
    public async Task SetActiveAsync(
        string connectorName,
        bool isActive,
        string? modifiedBy = null,
        CancellationToken ct = default)
    {
        var entity = await _context.ConnectorConfigurations
            .FirstOrDefaultAsync(c => c.ConnectorName == connectorName, ct);

        if (entity == null)
        {
            entity = new ConnectorConfigurationEntity
            {
                ConnectorName = connectorName,
                ConfigurationJson = "{}",
                SecretsJson = "{}",
                IsActive = isActive,
                LastModified = DateTimeOffset.UtcNow,
                ModifiedBy = modifiedBy
            };
            _context.ConnectorConfigurations.Add(entity);
        }
        else
        {
            entity.IsActive = isActive;
            entity.LastModified = DateTimeOffset.UtcNow;
            entity.ModifiedBy = modifiedBy;
        }

        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Set connector {ConnectorName} active={IsActive}", connectorName, isActive);

        // Broadcast enable/disable change
        await _broadcastService.BroadcastConfigChangeAsync(new ConfigurationChangeEvent
        {
            ConnectorName = connectorName,
            ChangeType = isActive ? "enabled" : "disabled",
            ModifiedBy = modifiedBy
        });
    }

    /// <inheritdoc />
    public async Task<bool> DeleteConfigurationAsync(string connectorName, CancellationToken ct = default)
    {
        var entity = await _context.ConnectorConfigurations
            .FirstOrDefaultAsync(c => c.ConnectorName == connectorName, ct);

        if (entity == null)
        {
            return false;
        }

        _context.ConnectorConfigurations.Remove(entity);
        await _context.SaveChangesAsync(ct);
        _logger.LogInformation("Deleted configuration for connector {ConnectorName}", connectorName);

        // Broadcast deletion
        await _broadcastService.BroadcastConfigChangeAsync(new ConfigurationChangeEvent
        {
            ConnectorName = connectorName,
            ChangeType = "deleted"
        });

        return true;
    }

    /// <summary>
    /// Finds the configuration class Type for a given connector name.
    /// </summary>
    private static Type? FindConfigurationType(string connectorName)
    {
        var assemblies = AppDomain.CurrentDomain.GetAssemblies()
            .Where(a => a.FullName?.Contains("Nocturne.Connectors") == true);

        foreach (var assembly in assemblies)
        {
            try
            {
                foreach (var type in assembly.GetTypes())
                {
                    var attr = type.GetCustomAttribute<ConnectorRegistrationAttribute>();
                    if (attr != null && attr.ConnectorName.Equals(connectorName, StringComparison.OrdinalIgnoreCase))
                    {
                        return type;
                    }
                }
            }
            catch (ReflectionTypeLoadException)
            {
                // Some types may not be loadable, skip them
            }
        }

        return null;
    }

    /// <summary>
    /// Generates a JSON Schema from a configuration type based on attributes.
    /// Only includes properties marked with [RuntimeConfigurable].
    /// </summary>
    private static JsonDocument GenerateSchemaFromType(Type configType)
    {
        var properties = new Dictionary<string, object>();
        var required = new List<string>();

        foreach (var property in configType.GetProperties(BindingFlags.Public | BindingFlags.Instance))
        {
            var runtimeAttr = property.GetCustomAttribute<RuntimeConfigurableAttribute>();
            if (runtimeAttr == null)
            {
                continue; // Only include runtime-configurable properties
            }

            var secretAttr = property.GetCustomAttribute<SecretAttribute>();
            if (secretAttr != null)
            {
                continue; // Don't include secrets in schema
            }

            var schemaAttr = property.GetCustomAttribute<ConfigSchemaAttribute>();
            var propertySchema = GeneratePropertySchema(property.PropertyType, runtimeAttr, schemaAttr);

            properties[ToCamelCase(property.Name)] = propertySchema;

            // Check for Required attribute
            if (property.GetCustomAttribute<System.ComponentModel.DataAnnotations.RequiredAttribute>() != null)
            {
                required.Add(ToCamelCase(property.Name));
            }
        }

        var schema = new Dictionary<string, object>
        {
            ["$schema"] = "https://json-schema.org/draft/2020-12/schema",
            ["type"] = "object",
            ["title"] = configType.Name,
            ["properties"] = properties
        };

        if (required.Count > 0)
        {
            schema["required"] = required;
        }

        var json = JsonSerializer.Serialize(schema, _jsonOptions);
        return JsonDocument.Parse(json);
    }

    /// <summary>
    /// Generates a JSON Schema property definition for a property.
    /// </summary>
    private static Dictionary<string, object> GeneratePropertySchema(
        Type propertyType,
        RuntimeConfigurableAttribute runtimeAttr,
        ConfigSchemaAttribute? schemaAttr)
    {
        var schema = new Dictionary<string, object>();

        // Determine JSON Schema type
        var underlyingType = Nullable.GetUnderlyingType(propertyType) ?? propertyType;

        if (underlyingType == typeof(bool))
        {
            schema["type"] = "boolean";
        }
        else if (underlyingType == typeof(int) || underlyingType == typeof(long) ||
                 underlyingType == typeof(short) || underlyingType == typeof(byte))
        {
            schema["type"] = "integer";
        }
        else if (underlyingType == typeof(float) || underlyingType == typeof(double) ||
                 underlyingType == typeof(decimal))
        {
            schema["type"] = "number";
        }
        else if (underlyingType.IsEnum)
        {
            schema["type"] = "string";
            schema["enum"] = Enum.GetNames(underlyingType);
        }
        else
        {
            schema["type"] = "string";
        }

        // Add title/description from RuntimeConfigurable
        if (!string.IsNullOrEmpty(runtimeAttr.DisplayName))
        {
            schema["title"] = runtimeAttr.DisplayName;
        }

        if (!string.IsNullOrEmpty(runtimeAttr.Description))
        {
            schema["description"] = runtimeAttr.Description;
        }

        // Add constraints from ConfigSchema
        if (schemaAttr != null)
        {
            if (schemaAttr.HasMinimum)
            {
                schema["minimum"] = schemaAttr.Minimum;
            }

            if (schemaAttr.HasMaximum)
            {
                schema["maximum"] = schemaAttr.Maximum;
            }

            if (schemaAttr.HasMinLength)
            {
                schema["minLength"] = schemaAttr.MinLength;
            }

            if (schemaAttr.HasMaxLength)
            {
                schema["maxLength"] = schemaAttr.MaxLength;
            }

            if (!string.IsNullOrEmpty(schemaAttr.Pattern))
            {
                schema["pattern"] = schemaAttr.Pattern;
            }

            if (schemaAttr.Enum != null && schemaAttr.Enum.Length > 0)
            {
                schema["enum"] = schemaAttr.Enum;
            }

            if (!string.IsNullOrEmpty(schemaAttr.Format))
            {
                schema["format"] = schemaAttr.Format;
            }
        }

        return schema;
    }

    private static string ToCamelCase(string name)
    {
        if (string.IsNullOrEmpty(name))
            return name;

        return char.ToLowerInvariant(name[0]) + name.Substring(1);
    }
}
