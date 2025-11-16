using System.Text.Json.Serialization;
using Microsoft.AspNetCore.Mvc;
using Nocturne.Core.Constants;

namespace Nocturne.API.Controllers;

/// <summary>
/// Metadata controller that exposes type definitions for frontend clients
/// </summary>
[ApiController]
[Route("api/[controller]")]
public class MetadataController : ControllerBase
{
    /// <summary>
    /// Get WebSocket event types metadata
    /// This endpoint exists primarily to ensure NSwag generates TypeScript types for WebSocket events
    /// </summary>
    /// <returns>WebSocket events metadata</returns>
    [HttpGet("websocket-events")]
    [ProducesResponseType(typeof(WebSocketEventsMetadata), 200)]
    public ActionResult<WebSocketEventsMetadata> GetWebSocketEvents()
    {
        return Ok(
            new WebSocketEventsMetadata
            {
                AvailableEvents = Enum.GetValues<WebSocketEvents>(),
                Description = "Available WebSocket event types for real-time communication",
            }
        );
    }
}

/// <summary>
/// Metadata about available WebSocket events
/// </summary>
public class WebSocketEventsMetadata
{
    /// <summary>
    /// Array of all available WebSocket event types
    /// </summary>
    [JsonConverter(typeof(JsonStringEnumConverter))]
    public WebSocketEvents[] AvailableEvents { get; set; } = [];

    /// <summary>
    /// Description of the WebSocket events
    /// </summary>
    public string Description { get; set; } = string.Empty;
}
