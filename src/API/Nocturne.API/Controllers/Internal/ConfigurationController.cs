using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.Core.Contracts;

namespace Nocturne.API.Controllers.Internal;

/// <summary>
/// Internal API for connector configuration management.
/// This endpoint is intended for internal use by connectors via mTLS authentication.
/// In the initial implementation, it uses standard API authentication.
/// </summary>
[ApiController]
[Route("internal/config")]
[Authorize]
[ApiExplorerSettings(GroupName = "internal")]
public class ConfigurationController : ControllerBase
{
    private readonly IConnectorConfigurationService _configService;
    private readonly ILogger<ConfigurationController> _logger;

    public ConfigurationController(
        IConnectorConfigurationService configService,
        ILogger<ConfigurationController> logger)
    {
        _configService = configService;
        _logger = logger;
    }

    /// <summary>
    /// Gets the configuration for a specific connector.
    /// Returns runtime configuration only (secrets are not included).
    /// </summary>
    /// <param name="connectorName">The connector name (e.g., "Dexcom", "Glooko")</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Configuration response or 404 if not found</returns>
    [HttpGet("{connectorName}")]
    [ProducesResponseType(typeof(ConnectorConfigurationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<ConnectorConfigurationResponse>> GetConfiguration(
        string connectorName,
        CancellationToken ct)
    {
        _logger.LogDebug("Getting configuration for connector {ConnectorName}", connectorName);

        var config = await _configService.GetConfigurationAsync(connectorName, includeSecrets: false, ct);
        if (config == null)
        {
            return NotFound(new { message = $"No configuration found for connector '{connectorName}'" });
        }

        return Ok(config);
    }

    /// <summary>
    /// Gets the JSON Schema for a connector's configuration.
    /// Schema is generated from the connector's configuration class attributes.
    /// </summary>
    /// <param name="connectorName">The connector name</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>JSON Schema document</returns>
    [HttpGet("{connectorName}/schema")]
    [ProducesResponseType(typeof(JsonDocument), StatusCodes.Status200OK)]
    public async Task<ActionResult<JsonDocument>> GetSchema(
        string connectorName,
        CancellationToken ct)
    {
        _logger.LogDebug("Getting schema for connector {ConnectorName}", connectorName);

        var schema = await _configService.GetSchemaAsync(connectorName, ct);
        return Ok(schema);
    }

    /// <summary>
    /// Saves or updates runtime configuration for a connector.
    /// Only properties marked with [RuntimeConfigurable] are accepted.
    /// </summary>
    /// <param name="connectorName">The connector name</param>
    /// <param name="configuration">Configuration values as JSON</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>The saved configuration</returns>
    [HttpPut("{connectorName}")]
    [ProducesResponseType(typeof(ConnectorConfigurationResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<ConnectorConfigurationResponse>> SaveConfiguration(
        string connectorName,
        [FromBody] JsonDocument configuration,
        CancellationToken ct)
    {
        var modifiedBy = User.Identity?.Name ?? "api";
        _logger.LogInformation("Saving configuration for connector {ConnectorName} by {ModifiedBy}",
            connectorName, modifiedBy);

        var result = await _configService.SaveConfigurationAsync(connectorName, configuration, modifiedBy, ct);
        return Ok(result);
    }

    /// <summary>
    /// Saves encrypted secrets for a connector.
    /// Secrets are encrypted using AES-256-GCM before storage.
    /// </summary>
    /// <param name="connectorName">The connector name</param>
    /// <param name="secrets">Dictionary of secret property names to plaintext values</param>
    /// <param name="ct">Cancellation token</param>
    [HttpPut("{connectorName}/secrets")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveSecrets(
        string connectorName,
        [FromBody] Dictionary<string, string> secrets,
        CancellationToken ct)
    {
        var modifiedBy = User.Identity?.Name ?? "api";
        _logger.LogInformation("Saving secrets for connector {ConnectorName} by {ModifiedBy}",
            connectorName, modifiedBy);

        try
        {
            await _configService.SaveSecretsAsync(connectorName, secrets, modifiedBy, ct);
            return NoContent();
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Failed to save secrets - encryption not configured");
            return BadRequest(new { message = ex.Message });
        }
    }

    /// <summary>
    /// Gets status information for all registered connectors.
    /// </summary>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of connector status information</returns>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ConnectorStatusInfo>), StatusCodes.Status200OK)]
    public async Task<ActionResult<IReadOnlyList<ConnectorStatusInfo>>> GetAllConnectorStatus(
        CancellationToken ct)
    {
        _logger.LogDebug("Getting all connector status");

        var status = await _configService.GetAllConnectorStatusAsync(ct);
        return Ok(status);
    }

    /// <summary>
    /// Enables or disables a connector.
    /// </summary>
    /// <param name="connectorName">The connector name</param>
    /// <param name="request">Request containing the active state</param>
    /// <param name="ct">Cancellation token</param>
    [HttpPatch("{connectorName}/active")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> SetActive(
        string connectorName,
        [FromBody] SetActiveRequest request,
        CancellationToken ct)
    {
        var modifiedBy = User.Identity?.Name ?? "api";
        _logger.LogInformation("Setting connector {ConnectorName} active={IsActive} by {ModifiedBy}",
            connectorName, request.IsActive, modifiedBy);

        await _configService.SetActiveAsync(connectorName, request.IsActive, modifiedBy, ct);
        return NoContent();
    }

    /// <summary>
    /// Deletes all configuration and secrets for a connector.
    /// </summary>
    /// <param name="connectorName">The connector name</param>
    /// <param name="ct">Cancellation token</param>
    [HttpDelete("{connectorName}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> DeleteConfiguration(
        string connectorName,
        CancellationToken ct)
    {
        _logger.LogInformation("Deleting configuration for connector {ConnectorName}", connectorName);

        var deleted = await _configService.DeleteConfigurationAsync(connectorName, ct);
        if (!deleted)
        {
            return NotFound(new { message = $"No configuration found for connector '{connectorName}'" });
        }

        return NoContent();
    }
}

/// <summary>
/// Request model for setting connector active state.
/// </summary>
public class SetActiveRequest
{
    /// <summary>
    /// Whether the connector should be active.
    /// </summary>
    public bool IsActive { get; set; }
}
