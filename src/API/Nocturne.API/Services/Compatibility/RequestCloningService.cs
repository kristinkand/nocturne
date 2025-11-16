using Nocturne.API.Models.Compatibility;

namespace Nocturne.API.Services.Compatibility;

/// <summary>
/// Service for cloning HTTP requests
/// </summary>
public interface IRequestCloningService
{
    /// <summary>
    /// Clone an HTTP request for forwarding to target systems
    /// </summary>
    /// <param name="request">The original HTTP request</param>
    /// <returns>A cloned request object</returns>
    Task<ClonedRequest> CloneRequestAsync(HttpRequest request);
}

/// <summary>
/// Implementation of request cloning service
/// </summary>
public class RequestCloningService : IRequestCloningService
{
    private readonly ILogger<RequestCloningService> _logger;

    /// <summary>
    /// Initializes a new instance of the RequestCloningService class
    /// </summary>
    /// <param name="logger">Logger instance for this service</param>
    public RequestCloningService(ILogger<RequestCloningService> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<ClonedRequest> CloneRequestAsync(HttpRequest request)
    {
        _logger.LogDebug("Cloning request: {Method} {Path}", request.Method, request.Path);

        var clonedRequest = new ClonedRequest
        {
            Method = request.Method,
            Path = request.Path + request.QueryString,
            ContentType = request.ContentType,
        };

        // Clone headers
        foreach (var header in request.Headers)
        {
            // Skip headers that shouldn't be forwarded
            if (ShouldForwardHeader(header.Key))
            {
                clonedRequest.Headers[header.Key] = header.Value.ToArray()!;
            }
        }

        // Clone query parameters
        foreach (var query in request.Query)
        {
            clonedRequest.QueryParameters[query.Key] = query.Value.ToArray()!;
        }

        // Clone body if present
        if (request.ContentLength > 0 && request.Body.CanRead)
        {
            using var memoryStream = new MemoryStream();
            await request.Body.CopyToAsync(memoryStream);
            clonedRequest.Body = memoryStream.ToArray();

            // Reset the original request body stream for potential reuse if possible
            if (request.Body.CanSeek)
            {
                request.Body.Position = 0;
            }
            else
            {
                // For non-seekable streams (like in testing), we need to replace the stream
                request.Body = new MemoryStream(clonedRequest.Body);
            }
        }

        _logger.LogDebug(
            "Request cloned successfully. Headers: {HeaderCount}, Body size: {BodySize}",
            clonedRequest.Headers.Count,
            clonedRequest.Body?.Length ?? 0
        );

        return clonedRequest;
    }

    private static bool ShouldForwardHeader(string headerName)
    {
        // Skip host-specific and connection-specific headers
        var skipHeaders = new[]
        {
            "host",
            "connection",
            "content-length",
            "transfer-encoding",
            "upgrade",
            "proxy-connection",
            "proxy-authenticate",
            "proxy-authorization",
        };

        return !skipHeaders.Contains(headerName.ToLowerInvariant());
    }
}
