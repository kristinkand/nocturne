namespace Nocturne.Tools.McpServer.Services;

/// <summary>
/// Configuration options for the Nocturne API connection
/// </summary>
public class NocturneApiOptions
{
    /// <summary>
    /// Base URL for the Nocturne API
    /// </summary>
    public string BaseUrl { get; set; } = "http://localhost:1612";

    /// <summary>
    /// Timeout in seconds for API requests
    /// </summary>
    public int TimeoutSeconds { get; set; } = 30;
}
