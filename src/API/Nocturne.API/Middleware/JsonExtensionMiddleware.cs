using System.Threading.Tasks;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;

namespace Nocturne.API.Middleware;

/// <summary>
/// Middleware to handle .json extensions in API routes for legacy Nightscout compatibility.
/// Strips .json extensions from paths so they can be handled by standard controller routes.
/// </summary>
public class JsonExtensionMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<JsonExtensionMiddleware> _logger;

    public JsonExtensionMiddleware(RequestDelegate next, ILogger<JsonExtensionMiddleware> logger)
    {
        _next = next;
        _logger = logger;
    }

    public async Task InvokeAsync(HttpContext context)
    {
        var path = context.Request.Path.Value;

        // Check if the path ends with .json
        if (
            !string.IsNullOrEmpty(path)
            && path.EndsWith(".json", StringComparison.OrdinalIgnoreCase)
        )
        {
            // Remove the .json extension
            var newPath = path.Substring(0, path.Length - 5);
            context.Request.Path = new PathString(newPath);

            // Ensure the Accept header includes application/json
            if (
                !context.Request.Headers.ContainsKey("Accept")
                || !context.Request.Headers["Accept"].ToString().Contains("application/json")
            )
            {
                context.Request.Headers["Accept"] = "application/json";
            }

            // Log the path rewrite for debugging
            _logger.LogDebug(
                "JsonExtensionMiddleware: Rewrote '{OriginalPath}' to '{NewPath}'",
                path,
                newPath
            );
        }

        await _next(context);
    }
}
