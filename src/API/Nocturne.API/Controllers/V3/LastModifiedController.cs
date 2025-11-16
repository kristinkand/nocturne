using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Attributes;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Controllers.V3;

/// <summary>
/// LastModified controller that provides timestamps for when collections were last modified
/// </summary>
[ApiController]
[Route("api/v3/[controller]")]
public class LastModifiedController : ControllerBase
{
    private readonly IStatusService _statusService;
    private readonly ILogger<LastModifiedController> _logger;

    public LastModifiedController(
        IStatusService statusService,
        ILogger<LastModifiedController> logger
    )
    {
        _statusService = statusService;
        _logger = logger;
    }

    /// <summary>
    /// Get last modified timestamps for all collections
    /// </summary>
    /// <returns>Last modified timestamps for each collection</returns>
    [HttpGet]
    [NightscoutEndpoint("/api/v3/lastModified")]
    [ProducesResponseType(typeof(LastModifiedResponse), 200)]
    public async Task<ActionResult<LastModifiedResponse>> GetLastModified()
    {
        _logger.LogDebug(
            "LastModified endpoint requested from {RemoteIpAddress}",
            HttpContext.Connection.RemoteIpAddress
        );

        try
        {
            var lastModified = await _statusService.GetLastModifiedAsync();

            _logger.LogDebug("Successfully generated last modified response");

            return Ok(lastModified);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating last modified response");

            // Return minimal response even on error to maintain compatibility
            var serverTime = DateTime.UtcNow;
            return Ok(
                new LastModifiedResponse
                {
                    ServerTime = serverTime,
                    Entries = null,
                    Treatments = null,
                    Profile = null,
                    DeviceStatus = null,
                    Food = null,
                    Settings = null,
                    Activity = null,
                    Additional = new Dictionary<string, DateTime>(),
                }
            );
        }
    }
}
