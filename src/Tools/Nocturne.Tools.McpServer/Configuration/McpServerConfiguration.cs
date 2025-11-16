using System.ComponentModel.DataAnnotations;
using System.Reflection;
using Nocturne.Tools.Abstractions.Configuration;

namespace Nocturne.Tools.McpServer.Configuration;

/// <summary>
/// Configuration for the Nocturne MCP Server tool.
/// </summary>
public class McpServerConfiguration : IToolConfiguration
{
    /// <inheritdoc/>
    public string ToolName => "Nocturne MCP Server";

    /// <inheritdoc/>
    public string Version =>
        Assembly.GetExecutingAssembly().GetName().Version?.ToString() ?? "1.0.0";

    /// <summary>
    /// Whether to use SSE (Server-Sent Events) transport instead of stdio.
    /// </summary>
    public bool UseWebServer { get; set; } = false;

    /// <summary>
    /// Port for the web server when using SSE transport.
    /// </summary>
    [Range(1, 65535, ErrorMessage = "Port must be between 1 and 65535")]
    public int Port { get; set; } = 5000;

    /// <summary>
    /// Base URL for the Nocturne API.
    /// </summary>
    [Required(ErrorMessage = "API base URL is required")]
    [Url(ErrorMessage = "API base URL must be a valid URL")]
    public string ApiBaseUrl { get; set; } = "http://localhost:1612";

    /// <summary>
    /// Timeout in seconds for API requests.
    /// </summary>
    [Range(1, 300, ErrorMessage = "Timeout must be between 1 and 300 seconds")]
    public int ApiTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Whether to enable verbose logging.
    /// </summary>
    public bool VerboseLogging { get; set; } = false;

    /// <summary>
    /// Configuration file path for additional settings.
    /// </summary>
    public string? ConfigPath { get; set; }

    /// <inheritdoc/>
    public ValidationResult ValidateConfiguration()
    {
        if (UseWebServer && (Port < 1 || Port > 65535))
        {
            return new ValidationResult($"Invalid port {Port}. Port must be between 1 and 65535.");
        }

        if (!Uri.TryCreate(ApiBaseUrl, UriKind.Absolute, out var uri))
        {
            return new ValidationResult(
                $"Invalid API base URL '{ApiBaseUrl}'. Must be a valid absolute URL."
            );
        }

        if (uri.Scheme != "http" && uri.Scheme != "https")
        {
            return new ValidationResult(
                $"Invalid API base URL scheme '{uri.Scheme}'. Only HTTP and HTTPS are supported."
            );
        }

        return ValidationResult.Success!;
    }
}

/// <summary>
/// Transport mode for MCP server communication.
/// </summary>
public enum McpTransportMode
{
    /// <summary>
    /// Standard input/output transport (default for console applications)
    /// </summary>
    Stdio,

    /// <summary>
    /// Server-Sent Events transport (for web-based clients)
    /// </summary>
    Sse,
}
