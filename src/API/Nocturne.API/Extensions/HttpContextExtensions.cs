using Nocturne.API.Middleware;
using Nocturne.Core.Models;

namespace Nocturne.API.Extensions;

/// <summary>
/// Extension methods for HttpContext to handle authentication and permissions
/// </summary>
public static class HttpContextExtensions
{
    /// <summary>
    /// Get the authentication context from the request
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>Authentication context</returns>
    public static AuthenticationContext GetAuthContext(this HttpContext context)
    {
        return context.Items["AuthContext"] as AuthenticationContext ?? new AuthenticationContext();
    }

    /// <summary>
    /// Check if the current request has a specific permission
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <param name="permission">Permission to check</param>
    /// <returns>True if permission is granted</returns>
    public static bool HasPermission(this HttpContext context, string permission)
    {
        var authContext = context.GetAuthContext();
        if (!authContext.IsAuthenticated)
        {
            return false;
        }

        var permissionTrie = context.Items["PermissionTrie"] as PermissionTrie;
        if (permissionTrie == null)
        {
            return false;
        }

        return permissionTrie.Check(permission);
    }

    /// <summary>
    /// Check if the current request is authenticated
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>True if authenticated</returns>
    public static bool IsAuthenticated(this HttpContext context)
    {
        return context.GetAuthContext().IsAuthenticated;
    }

    /// <summary>
    /// Get the subject ID for the current request
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>Subject ID or null if not authenticated</returns>
    public static string? GetSubjectId(this HttpContext context)
    {
        return context.GetAuthContext().SubjectId;
    }

    /// <summary>
    /// Check if the current request has admin permissions
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>True if has admin permissions</returns>
    public static bool IsAdmin(this HttpContext context)
    {
        return context.HasPermission("admin") || context.HasPermission("*");
    }

    /// <summary>
    /// Check if the current request has read permissions
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>True if has read permissions</returns>
    public static bool CanRead(this HttpContext context)
    {
        return context.HasPermission("*")
            || context.HasPermission("api:*")
            || context.HasPermission("api:*:read")
            || context.HasPermission("readable");
    }

    /// <summary>
    /// Check if the current request has write permissions
    /// </summary>
    /// <param name="context">HTTP context</param>
    /// <returns>True if has write permissions</returns>
    public static bool CanWrite(this HttpContext context)
    {
        return context.HasPermission("*")
            || context.HasPermission("api:*")
            || context.HasPermission("api:*:create")
            || context.HasPermission("api:*:update")
            || context.HasPermission("api:*:delete");
    }
}
