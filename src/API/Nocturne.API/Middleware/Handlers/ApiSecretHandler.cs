using Nocturne.Connectors.Core.Utilities;
using Nocturne.Core.Constants;
using Nocturne.Core.Models.Authorization;

namespace Nocturne.API.Middleware.Handlers;

/// <summary>
/// Authentication handler for legacy Nightscout API secret.
/// Validates SHA1 hash of the API secret sent in the api-secret header.
/// Grants full admin (*) permissions.
/// </summary>
public class ApiSecretHandler : IAuthHandler
{
    /// <summary>
    /// Handler priority (400 - last in chain)
    /// </summary>
    public int Priority => 400;

    /// <summary>
    /// Handler name for logging
    /// </summary>
    public string Name => "ApiSecretHandler";

    private readonly IConfiguration _configuration;
    private readonly ILogger<ApiSecretHandler> _logger;
    private readonly string _apiSecretHash;

    /// <summary>
    /// Creates a new instance of ApiSecretHandler
    /// </summary>
    public ApiSecretHandler(IConfiguration configuration, ILogger<ApiSecretHandler> logger)
    {
        _configuration = configuration;
        _logger = logger;

        // Pre-compute the expected hash
        // Check both the new Parameters:api-secret location and legacy API_SECRET for backwards compatibility
        var apiSecret =
            _configuration[$"Parameters:{ServiceNames.Parameters.ApiSecret}"]
            ?? _configuration[ServiceNames.ConfigKeys.ApiSecret]
            ?? "";
        _apiSecretHash = !string.IsNullOrEmpty(apiSecret) ? HashUtils.Sha1Hex(apiSecret) : "";
    }

    /// <inheritdoc />
    public Task<AuthResult> AuthenticateAsync(HttpContext context)
    {
        // Check for api-secret header
        var apiSecretHeader = context.Request.Headers["api-secret"].FirstOrDefault();

        if (string.IsNullOrEmpty(apiSecretHeader))
        {
            // No api-secret header, skip to next handler
            return Task.FromResult(AuthResult.Skip());
        }

        // API secret not configured on server
        if (string.IsNullOrEmpty(_apiSecretHash))
        {
            _logger.LogWarning(
                "api-secret header provided but API_SECRET not configured on server"
            );
            return Task.FromResult(AuthResult.Failure("API secret not configured"));
        }

        // Validate the hash (case-insensitive comparison)
        if (!string.Equals(apiSecretHeader, _apiSecretHash, StringComparison.OrdinalIgnoreCase))
        {
            _logger.LogWarning("Invalid API secret provided");
            return Task.FromResult(AuthResult.Failure("Invalid API secret"));
        }

        // API secret authentication successful - grant admin permissions
        var authContext = new AuthContext
        {
            IsAuthenticated = true,
            AuthType = AuthType.ApiSecret,
            SubjectName = "admin",
            Permissions = ["*"],
            Roles = ["admin"],
        };

        _logger.LogDebug("API secret authentication successful");
        return Task.FromResult(AuthResult.Success(authContext));
    }
}
