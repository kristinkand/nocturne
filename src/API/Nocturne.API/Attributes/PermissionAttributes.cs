using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nocturne.API.Extensions;

namespace Nocturne.API.Attributes;

/// <summary>
/// Attribute to require specific permissions for controller actions
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequirePermissionAttribute : Attribute, IAuthorizationFilter
{
    private readonly string[] _permissions;
    private readonly bool _requireAll;

    /// <summary>
    /// Initialize with required permissions and combination logic
    /// </summary>
    /// <param name="requireAll">Whether all permissions are required (true) or any one (false)</param>
    /// <param name="permissions">Required permissions</param>
    public RequirePermissionAttribute(bool requireAll, params string[] permissions)
    {
        _permissions = permissions;
        _requireAll = requireAll;
    }

    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var httpContext = context.HttpContext;

        // Check if user is authenticated
        if (!httpContext.IsAuthenticated())
        {
            context.Result = new UnauthorizedResult();
            return;
        }

        // Check permissions
        var hasPermission = _requireAll
            ? _permissions.All(p => httpContext.HasPermission(p))
            : _permissions.Any(p => httpContext.HasPermission(p));

        if (!hasPermission)
        {
            context.Result = new ForbidResult();
            return;
        }
    }
}

/// <summary>
/// Attribute to require authentication (but no specific permissions)
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAuthenticationAttribute : Attribute, IAuthorizationFilter
{
    public void OnAuthorization(AuthorizationFilterContext context)
    {
        var httpContext = context.HttpContext;

        if (!httpContext.IsAuthenticated())
        {
            context.Result = new UnauthorizedResult();
            return;
        }
    }
}

/// <summary>
/// Attribute to require admin permissions
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireAdminAttribute : RequirePermissionAttribute
{
    public RequireAdminAttribute()
        : base(false, "admin", "*") { }
}

/// <summary>
/// Attribute to require read permissions
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireReadAttribute : RequirePermissionAttribute
{
    public RequireReadAttribute()
        : base(false, "*", "api:*", "api:*:read", "readable") { }
}

/// <summary>
/// Attribute to require write permissions
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class RequireWriteAttribute : RequirePermissionAttribute
{
    public RequireWriteAttribute()
        : base(false, "*", "api:*", "api:*:create", "api:*:update", "api:*:delete") { }
}
