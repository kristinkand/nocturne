using Microsoft.AspNetCore.Mvc;
using Nocturne.API.Attributes;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Controllers.V1;

/// <summary>
/// Status controller that provides 1:1 compatibility with Nightscout status endpoint
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
public class StatusController : ControllerBase
{
    private readonly IStatusService _statusService;
    private readonly ILogger<StatusController> _logger;

    public StatusController(IStatusService statusService, ILogger<StatusController> logger)
    {
        _statusService = statusService;
        _logger = logger;
    }

    /// <summary>
    /// Get the current system status
    /// </summary>
    /// <returns>System status information</returns>
    [HttpGet]
    [NightscoutEndpoint("/api/v1/status")]
    [ProducesResponseType(typeof(StatusResponse), 200)]
    public async Task<ActionResult<StatusResponse>> GetStatus()
    {
        _logger.LogDebug(
            "Status endpoint requested from {RemoteIpAddress}",
            HttpContext.Connection.RemoteIpAddress
        );

        try
        {
            var status = await _statusService.GetSystemStatusAsync();

            _logger.LogDebug("Successfully generated status response");

            return Ok(status);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating status response");

            // Return minimal status response even on error to maintain compatibility
            return Ok(
                new StatusResponse
                {
                    Status = "error",
                    Name = "Nocturne",
                    Version = "unknown",
                    ServerTime = DateTime.UtcNow,
                }
            );
        }
    }
}
