using Microsoft.Extensions.Options;
using Nocturne.API.Configuration;

namespace Nocturne.API.Services.Compatibility;

/// <summary>
/// Service for generating and managing request correlation IDs
/// </summary>
public interface ICorrelationService
{
    /// <summary>
    /// Generate a new correlation ID for a request
    /// </summary>
    /// <returns>Unique correlation ID</returns>
    string GenerateCorrelationId();

    /// <summary>
    /// Get the current correlation ID from context
    /// </summary>
    /// <returns>Current correlation ID or null if not set</returns>
    string? GetCurrentCorrelationId();

    /// <summary>
    /// Set the correlation ID in the current context
    /// </summary>
    /// <param name="correlationId">Correlation ID to set</param>
    void SetCorrelationId(string correlationId);
}

/// <summary>
/// Implementation of correlation service using AsyncLocal for context storage
/// </summary>
public class CorrelationService : ICorrelationService
{
    private static readonly AsyncLocal<string?> _correlationId = new();
    private readonly IOptions<CompatibilityProxyConfiguration> _configuration;
    private readonly ILogger<CorrelationService> _logger;

    /// <summary>
    /// Initializes a new instance of the CorrelationService class
    /// </summary>
    /// <param name="configuration">Compatibility proxy configuration settings</param>
    /// <param name="logger">Logger instance for this service</param>
    public CorrelationService(
        IOptions<CompatibilityProxyConfiguration> configuration,
        ILogger<CorrelationService> logger
    )
    {
        _configuration = configuration;
        _logger = logger;
    }

    /// <inheritdoc />
    public string GenerateCorrelationId()
    {
        if (!_configuration.Value.EnableCorrelationTracking)
        {
            return string.Empty;
        }

        var correlationId = $"INT-{DateTimeOffset.UtcNow:yyyyMMdd-HHmmss}-{Guid.CreateVersion7():N}";

        _logger.LogDebug("Generated correlation ID: {CorrelationId}", correlationId);

        return correlationId;
    }

    /// <inheritdoc />
    public string? GetCurrentCorrelationId()
    {
        return _correlationId.Value;
    }

    /// <inheritdoc />
    public void SetCorrelationId(string correlationId)
    {
        _correlationId.Value = correlationId;
        _logger.LogDebug("Set correlation ID in context: {CorrelationId}", correlationId);
    }
}
