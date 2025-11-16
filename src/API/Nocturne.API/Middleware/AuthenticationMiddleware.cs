using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.IdentityModel.Tokens;
using Nocturne.Core.Constants;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Middleware;

/// <summary>
/// Middleware for handling Nightscout authentication including API secrets and JWT tokens
/// </summary>
public class AuthenticationMiddleware
{
    private readonly RequestDelegate _next;
    private readonly IConfiguration _configuration;
    private readonly ILogger<AuthenticationMiddleware> _logger;
    private readonly string _apiSecret;
    private readonly byte[] _jwtKey;

    public AuthenticationMiddleware(
        RequestDelegate next,
        IConfiguration configuration,
        ILogger<AuthenticationMiddleware> logger
    )
    {
        _next = next;
        _configuration = configuration;
        _logger = logger;

        _apiSecret = _configuration[ServiceNames.ConfigKeys.ApiSecret] ?? "";
        var jwtSecret = _configuration[ServiceNames.ConfigKeys.JwtSecret] ?? _apiSecret;
        _jwtKey = Encoding.UTF8.GetBytes(jwtSecret);
    }

    public async Task InvokeAsync(HttpContext context)
    {
        await AuthenticateRequest(context);
        await _next(context);
    }

    private async Task AuthenticateRequest(HttpContext context)
    {
        try
        {
            // Check for JWT Bearer token first
            var authHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (
                !string.IsNullOrEmpty(authHeader)
                && authHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase)
            )
            {
                var token = authHeader["Bearer ".Length..].Trim();
                await AuthenticateJwtToken(context, token);
                return;
            }

            // Check for API secret
            var apiSecretHeader = context.Request.Headers["api-secret"].FirstOrDefault();
            if (!string.IsNullOrEmpty(apiSecretHeader))
            {
                await AuthenticateApiSecret(context, apiSecretHeader);
                return;
            }

            // No authentication provided
            SetUnauthenticated(context);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during authentication");
            SetUnauthenticated(context);
        }
    }

    private async Task AuthenticateJwtToken(HttpContext context, string token)
    {
        try
        {
            var tokenHandler = new JwtSecurityTokenHandler();
            var validationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(_jwtKey),
                ValidateIssuer = false,
                ValidateAudience = false,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.Zero,
            };

            var principal = tokenHandler.ValidateToken(
                token,
                validationParameters,
                out var validatedToken
            );
            var jwtToken = validatedToken as JwtSecurityToken;

            if (jwtToken != null)
            {
                var subjectId = principal.FindFirst("sub")?.Value;
                var permissionsClaim = principal.FindFirst("permissions")?.Value;

                if (!string.IsNullOrEmpty(subjectId))
                {
                    // Get authorization service to check permissions
                    var authService =
                        context.RequestServices.GetRequiredService<IAuthorizationService>();

                    var permissions = new List<string>();
                    if (!string.IsNullOrEmpty(permissionsClaim))
                    {
                        permissions = permissionsClaim.Split(',').ToList();
                    }

                    // Set authentication context
                    var authContext = new AuthenticationContext
                    {
                        IsAuthenticated = true,
                        AuthenticationType = AuthenticationType.JwtToken,
                        SubjectId = subjectId,
                        Permissions = permissions,
                        Token = token,
                    };

                    context.Items["AuthContext"] = authContext;

                    // Build permission trie for fast checking
                    var permissionTrie = new PermissionTrie();
                    if (permissions.Count > 0)
                    {
                        permissionTrie.Add(permissions);
                    }
                    context.Items["PermissionTrie"] = permissionTrie;

                    _logger.LogDebug(
                        "JWT authentication successful for subject {SubjectId}",
                        subjectId
                    );
                    return;
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "JWT token validation failed");
        }

        SetUnauthenticated(context);
        await Task.CompletedTask;
    }

    private async Task AuthenticateApiSecret(HttpContext context, string providedHash)
    {
        try
        {
            if (string.IsNullOrEmpty(_apiSecret))
            {
                _logger.LogWarning("API_SECRET not configured but api-secret header provided");
                SetUnauthenticated(context);
                return;
            }

            // Calculate SHA1 hash of the API secret
            using var sha1 = SHA1.Create();
            var secretBytes = Encoding.UTF8.GetBytes(_apiSecret);
            var hashBytes = sha1.ComputeHash(secretBytes);
            var expectedHash = BitConverter.ToString(hashBytes).Replace("-", "").ToLowerInvariant();

            if (providedHash.ToLowerInvariant() == expectedHash)
            {
                // API secret authentication successful
                var authContext = new AuthenticationContext
                {
                    IsAuthenticated = true,
                    AuthenticationType = AuthenticationType.ApiSecret,
                    SubjectId = "admin", // API secret gives admin privileges
                    Permissions = new List<string> { "*" }, // Full permissions
                    Token = null,
                };

                context.Items["AuthContext"] = authContext;

                // Create admin permission trie
                var permissionTrie = new PermissionTrie();
                permissionTrie.Add(new[] { "*" });
                context.Items["PermissionTrie"] = permissionTrie;

                _logger.LogDebug("API secret authentication successful");
                return;
            }
            else
            {
                _logger.LogWarning("Invalid API secret provided");
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error validating API secret");
        }

        SetUnauthenticated(context);
        await Task.CompletedTask;
    }

    private static void SetUnauthenticated(HttpContext context)
    {
        var authContext = new AuthenticationContext
        {
            IsAuthenticated = false,
            AuthenticationType = AuthenticationType.None,
            SubjectId = null,
            Permissions = new List<string>(),
            Token = null,
        };

        context.Items["AuthContext"] = authContext;
        context.Items["PermissionTrie"] = new PermissionTrie(); // Empty trie
    }
}

/// <summary>
/// Authentication context information
/// </summary>
public class AuthenticationContext
{
    /// <summary>
    /// Whether the request is authenticated
    /// </summary>
    public bool IsAuthenticated { get; set; }

    /// <summary>
    /// Type of authentication used
    /// </summary>
    public AuthenticationType AuthenticationType { get; set; }

    /// <summary>
    /// Subject identifier (user/device ID)
    /// </summary>
    public string? SubjectId { get; set; }

    /// <summary>
    /// List of permissions for this authentication
    /// </summary>
    public List<string> Permissions { get; set; } = new();

    /// <summary>
    /// JWT token if using token authentication
    /// </summary>
    public string? Token { get; set; }
}

/// <summary>
/// Types of authentication supported
/// </summary>
public enum AuthenticationType
{
    /// <summary>
    /// No authentication
    /// </summary>
    None,

    /// <summary>
    /// API secret authentication
    /// </summary>
    ApiSecret,

    /// <summary>
    /// JWT token authentication
    /// </summary>
    JwtToken,
}
