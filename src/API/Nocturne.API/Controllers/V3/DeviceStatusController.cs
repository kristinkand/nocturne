using System.Text.Json;
using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Attributes;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Abstractions;

namespace Nocturne.API.Controllers.V3;

/// <summary>
/// V3 DeviceStatus controller that provides full V3 API compatibility with Nightscout devicestatus endpoints
/// Implements the /api/v3/devicestatus endpoints with pagination, field selection, sorting, and advanced filtering
/// </summary>
[ApiController]
[Route("api/v3/[controller]")]
public class DeviceStatusController : BaseV3Controller<DeviceStatus>
{
    public DeviceStatusController(
        IPostgreSqlService postgreSqlService,
        IDataFormatService dataFormatService,
        IDocumentProcessingService documentProcessingService,
        ILogger<DeviceStatusController> logger
    )
        : base(postgreSqlService, dataFormatService, documentProcessingService, logger) { }

    /// <summary>
    /// Get device status records with V3 API features including pagination, field selection, and advanced filtering
    /// </summary>
    /// <returns>V3 device status collection response</returns>
    [HttpGet]
    [NightscoutEndpoint("/api/v3/devicestatus")]
    [ProducesResponseType(typeof(V3CollectionResponse<object>), 200)]
    [ProducesResponseType(typeof(V3ErrorResponse), 400)]
    [ProducesResponseType(304)]
    [ProducesResponseType(500)]
    public async Task<ActionResult> GetDeviceStatus(CancellationToken cancellationToken = default)
    {
        _logger.LogDebug(
            "V3 devicestatus endpoint requested from {RemoteIpAddress}",
            HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown"
        );

        try
        {
            var parameters = ParseV3QueryParameters();

            // Convert V3 parameters to backend query for compatibility
            var findQuery = ConvertV3FilterToV1Find(parameters.Filter);
            var reverseResults = ExtractSortDirection(parameters.Sort);

            // Get device status using existing backend with V3 parameters
            var deviceStatusRecords =
                await _postgreSqlService.GetDeviceStatusWithAdvancedFilterAsync(
                    count: parameters.Limit,
                    skip: parameters.Offset,
                    findQuery: findQuery,
                    reverseResults: reverseResults,
                    cancellationToken: cancellationToken
                );

            var deviceStatusList = deviceStatusRecords.ToList();

            // Get total count for pagination (approximation for performance)
            var totalCount = await GetTotalCountAsync(
                null,
                findQuery,
                cancellationToken,
                "devicestatus"
            ); // Check for conditional requests (304 Not Modified)
            var lastModified = GetLastModified(deviceStatusList.Cast<object>());
            var etag = GenerateETag(deviceStatusList);

            if (lastModified.HasValue && ShouldReturn304(etag, lastModified.Value, parameters))
            {
                return StatusCode(304);
            }

            // Create V3 response
            var response = CreateV3CollectionResponse(deviceStatusList, parameters, totalCount);

            _logger.LogDebug(
                "Successfully returned {Count} device status records with V3 format",
                deviceStatusList.Count
            );

            return Ok(response);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid V3 devicestatus request parameters");
            return CreateV3ErrorResponse(400, "Invalid request parameters", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving V3 devicestatus");
            return CreateV3ErrorResponse(500, "Internal server error", ex.Message);
        }
    }

    /// <summary>
    /// Get a specific device status record by ID with V3 format
    /// </summary>
    /// <param name="id">Device status ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Single device status record in V3 format</returns>
    [HttpGet("{id}")]
    [NightscoutEndpoint("/api/v3/devicestatus/{id}")]
    [ProducesResponseType(typeof(DeviceStatus), 200)]
    [ProducesResponseType(typeof(V3ErrorResponse), 404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult> GetDeviceStatusById(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(
            "V3 devicestatus by ID endpoint requested for ID {Id} from {RemoteIpAddress}",
            id,
            HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown"
        );

        try
        {
            var deviceStatus = await _postgreSqlService.GetDeviceStatusByIdAsync(
                id,
                cancellationToken
            );

            if (deviceStatus == null)
            {
                return CreateV3ErrorResponse(
                    404,
                    "Device status not found",
                    $"Device status with ID '{id}' was not found"
                );
            }

            var parameters = ParseV3QueryParameters(); // Apply field selection if specified
            var result = ApplyFieldSelection(new[] { deviceStatus }, parameters.Fields)
                .FirstOrDefault();

            _logger.LogDebug("Successfully returned device status with ID {Id}", id);

            return Ok(result);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving device status with ID {Id}", id);
            return CreateV3ErrorResponse(500, "Internal server error", ex.Message);
        }
    }

    /// <summary>
    /// Create new device status records with V3 format and deduplication support
    /// </summary>
    /// <param name="deviceStatusData">Device status data to create (single object or array)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created device status records</returns>
    [HttpPost]
    [NightscoutEndpoint("/api/v3/devicestatus")]
    [ProducesResponseType(typeof(DeviceStatus[]), 201)]
    [ProducesResponseType(typeof(V3ErrorResponse), 400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult> CreateDeviceStatus(
        [FromBody] JsonElement deviceStatusData,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(
            "V3 devicestatus create endpoint requested from {RemoteIpAddress}",
            HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown"
        );
        try
        {
            var deviceStatusRecords = ParseCreateRequestFromJsonElement(deviceStatusData);

            if (!deviceStatusRecords.Any())
            {
                return CreateV3ErrorResponse(
                    400,
                    "Invalid request body",
                    "Request body must contain valid device status data"
                );
            }

            // Process each device status record (date parsing, validation, etc.)
            foreach (var deviceStatus in deviceStatusRecords)
            {
                ProcessDeviceStatusForCreation(deviceStatus);
            }

            // Create device status records with deduplication support
            var createdRecords = await _postgreSqlService.CreateDeviceStatusAsync(
                deviceStatusRecords,
                cancellationToken
            );

            _logger.LogDebug(
                "Successfully created {Count} device status records",
                createdRecords.Count()
            );

            return StatusCode(201, createdRecords.ToArray());
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid V3 devicestatus create request");
            return CreateV3ErrorResponse(400, "Invalid request data", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error creating V3 devicestatus");
            return CreateV3ErrorResponse(500, "Internal server error", ex.Message);
        }
    }

    /// <summary>
    /// Update a device status record by ID with V3 format
    /// </summary>
    /// <param name="id">Device status ID to update</param>
    /// <param name="deviceStatus">Updated device status data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated device status record</returns>
    [HttpPut("{id}")]
    [NightscoutEndpoint("/api/v3/devicestatus/{id}")]
    [ProducesResponseType(typeof(DeviceStatus), 200)]
    [ProducesResponseType(typeof(V3ErrorResponse), 404)]
    [ProducesResponseType(typeof(V3ErrorResponse), 400)]
    [ProducesResponseType(500)]
    public async Task<ActionResult> UpdateDeviceStatus(
        string id,
        [FromBody] DeviceStatus deviceStatus,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(
            "V3 devicestatus update endpoint requested for ID {Id} from {RemoteIpAddress}",
            id,
            HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown"
        );

        try
        {
            if (deviceStatus == null)
            {
                return CreateV3ErrorResponse(
                    400,
                    "Invalid request body",
                    "Request body must contain valid device status data"
                );
            }

            ProcessDeviceStatusForCreation(deviceStatus);

            var updatedDeviceStatus = await _postgreSqlService.UpdateDeviceStatusAsync(
                id,
                deviceStatus,
                cancellationToken
            );

            if (updatedDeviceStatus == null)
            {
                return CreateV3ErrorResponse(
                    404,
                    "Device status not found",
                    $"Device status with ID '{id}' was not found"
                );
            }

            _logger.LogDebug("Successfully updated device status with ID {Id}", id);

            return Ok(updatedDeviceStatus);
        }
        catch (ArgumentException ex)
        {
            _logger.LogWarning(ex, "Invalid V3 devicestatus update request for ID {Id}", id);
            return CreateV3ErrorResponse(400, "Invalid request data", ex.Message);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error updating device status with ID {Id}", id);
            return CreateV3ErrorResponse(500, "Internal server error", ex.Message);
        }
    }

    /// <summary>
    /// Delete a device status record by ID
    /// </summary>
    /// <param name="id">Device status ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>No content on success</returns>
    [HttpDelete("{id}")]
    [NightscoutEndpoint("/api/v3/devicestatus/{id}")]
    [ProducesResponseType(204)]
    [ProducesResponseType(typeof(V3ErrorResponse), 404)]
    [ProducesResponseType(500)]
    public async Task<ActionResult> DeleteDeviceStatus(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        _logger.LogDebug(
            "V3 devicestatus delete endpoint requested for ID {Id} from {RemoteIpAddress}",
            id,
            HttpContext?.Connection?.RemoteIpAddress?.ToString() ?? "unknown"
        );

        try
        {
            var deleted = await _postgreSqlService.DeleteDeviceStatusAsync(id, cancellationToken);

            if (!deleted)
            {
                return CreateV3ErrorResponse(
                    404,
                    "Device status not found",
                    $"Device status with ID '{id}' was not found"
                );
            }

            _logger.LogDebug("Successfully deleted device status with ID {Id}", id);

            return NoContent();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error deleting device status with ID {Id}", id);
            return CreateV3ErrorResponse(500, "Internal server error", ex.Message);
        }
    }

    /// <summary>
    /// Process device status for creation/update (date parsing, validation, etc.)
    /// Follows the legacy API v3 behavior exactly
    /// </summary>
    /// <param name="deviceStatus">Device status to process</param>
    private void ProcessDeviceStatusForCreation(DeviceStatus deviceStatus)
    {
        // Generate identifier if not present (legacy behavior)
        if (string.IsNullOrEmpty(deviceStatus.Id))
        {
            deviceStatus.Id = GenerateIdentifier(deviceStatus);
        }

        // Ensure DeviceStatus has required properties for V3 compatibility
        if (string.IsNullOrEmpty(deviceStatus.CreatedAt))
        {
            deviceStatus.CreatedAt = DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ");
        }
    }

    /// <summary>
    /// Generate identifier for device status following legacy API v3 logic
    /// Uses created_at and device fields for deduplication fallback
    /// </summary>
    /// <param name="deviceStatus">Device status record</param>
    /// <returns>Generated identifier</returns>
    private string GenerateIdentifier(DeviceStatus deviceStatus)
    {
        // Legacy API v3 uses created_at + device for devicestatus deduplication
        var identifierParts = new List<string>();

        if (!string.IsNullOrEmpty(deviceStatus.CreatedAt))
        {
            identifierParts.Add(deviceStatus.CreatedAt);
        }

        if (!string.IsNullOrEmpty(deviceStatus.Device))
        {
            identifierParts.Add(deviceStatus.Device);
        }

        // Add timestamp for uniqueness
        identifierParts.Add(DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"));

        // If we have identifying parts, create a hash-based identifier
        if (identifierParts.Any())
        {
            var combined = string.Join("-", identifierParts);
            return $"devicestatus-{combined.GetHashCode():X}";
        }

        // Fallback to GUID for unique identification
        return Guid.CreateVersion7().ToString();
    }

    /// <summary>
    /// Parse create request from JsonElement for DeviceStatus objects
    /// </summary>
    /// <param name="jsonElement">JsonElement containing device status data (single object or array)</param>
    /// <returns>Collection of DeviceStatus objects</returns>
    private IEnumerable<DeviceStatus> ParseCreateRequestFromJsonElement(JsonElement jsonElement)
    {
        var deviceStatusRecords = new List<DeviceStatus>();

        try
        {
            if (jsonElement.ValueKind == JsonValueKind.Array)
            {
                foreach (var element in jsonElement.EnumerateArray())
                {
                    var deviceStatus = JsonSerializer.Deserialize<DeviceStatus>(
                        element.GetRawText(),
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                    );
                    if (deviceStatus != null)
                    {
                        deviceStatusRecords.Add(deviceStatus);
                    }
                }
            }
            else if (jsonElement.ValueKind == JsonValueKind.Object)
            {
                var deviceStatus = JsonSerializer.Deserialize<DeviceStatus>(
                    jsonElement.GetRawText(),
                    new JsonSerializerOptions { PropertyNameCaseInsensitive = true }
                );
                if (deviceStatus != null)
                {
                    deviceStatusRecords.Add(deviceStatus);
                }
            }
        }
        catch (JsonException ex)
        {
            _logger.LogWarning(ex, "Failed to parse device status data from JsonElement");
            throw new ArgumentException("Invalid device status data format", ex);
        }

        return deviceStatusRecords;
    }

    /// <summary>
    /// Get total count for pagination support
    /// </summary>
    private async Task<long> GetTotalCountAsync(
        string? type,
        string? findQuery,
        CancellationToken cancellationToken,
        string collection = "devicestatus"
    )
    {
        try
        {
            return await _postgreSqlService.CountDeviceStatusAsync(findQuery, cancellationToken);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Could not get total count for {Collection}, using approximation",
                collection
            );
            return 0; // Return 0 for count errors to maintain API functionality
        }
    }
}
