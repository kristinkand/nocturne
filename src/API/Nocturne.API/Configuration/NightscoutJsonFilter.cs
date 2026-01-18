using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Filters;
using Nocturne.Core.Models.Attributes;

namespace Nocturne.API.Configuration;

/// <summary>
/// Action filter that applies Nightscout-compatible JSON serialization to v1-v3 endpoints.
/// This filter modifies the JsonSerializerOptions to:
/// - Ignore null values
/// - Exclude properties marked with [NocturneOnly]
/// </summary>
public class NightscoutJsonFilter : IAsyncResultFilter
{
    private static readonly JsonSerializerOptions NightscoutOptions = CreateNightscoutOptions();

    private static JsonSerializerOptions CreateNightscoutOptions()
    {
        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
            WriteIndented = false,
            TypeInfoResolver = new DefaultJsonTypeInfoResolver
            {
                Modifiers = { ExcludeNocturneOnlyProperties }
            }
        };

        return options;
    }

    private static void ExcludeNocturneOnlyProperties(JsonTypeInfo typeInfo)
    {
        if (typeInfo.Kind != JsonTypeInfoKind.Object)
            return;

        foreach (var property in typeInfo.Properties)
        {
            // Use AttributeProvider to get the actual property/field info with its attributes
            // This works correctly regardless of JsonPropertyName remapping
            if (property.AttributeProvider?.GetCustomAttributes(typeof(NocturneOnlyAttribute), true).Length > 0)
            {
                property.ShouldSerialize = (_, _) => false;
            }
        }
    }

    public async Task OnResultExecutionAsync(ResultExecutingContext context, ResultExecutionDelegate next)
    {
        // Check if this is a Nightscout endpoint (v1, v2, v3)
        var path = context.HttpContext.Request.Path.Value?.ToLowerInvariant() ?? "";
        var isNightscoutEndpoint = path.StartsWith("/api/v1/") ||
                                   path.StartsWith("/api/v2/") ||
                                   path.StartsWith("/api/v3/");

        if (isNightscoutEndpoint && context.Result is ObjectResult objectResult)
        {
            // Replace with JsonResult using Nightscout options
            context.Result = new JsonResult(objectResult.Value, NightscoutOptions)
            {
                StatusCode = objectResult.StatusCode
            };
        }

        await next();
    }
}

/// <summary>
/// Attribute to apply Nightscout JSON formatting to a controller or action
/// </summary>
[AttributeUsage(AttributeTargets.Class | AttributeTargets.Method)]
public class NightscoutJsonAttribute : TypeFilterAttribute
{
    public NightscoutJsonAttribute() : base(typeof(NightscoutJsonFilter))
    {
    }
}
