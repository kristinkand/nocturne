using System.Reflection;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Text.Json.Serialization.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Formatters;
using Nocturne.Core.Models.Attributes;

namespace Nocturne.API.Configuration;

/// <summary>
/// Custom JSON output formatter for Nightscout-compatible endpoints (v1, v2, v3).
/// This formatter:
/// - Ignores null values (Nightscout doesn't include null fields)
/// - Excludes properties marked with [NocturneOnly] attribute
/// - Uses camelCase property naming
/// </summary>
public class NightscoutJsonOutputFormatter : SystemTextJsonOutputFormatter
{
    public NightscoutJsonOutputFormatter() : base(CreateOptions())
    {
    }

    private static JsonSerializerOptions CreateOptions()
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

    /// <summary>
    /// Modifier that excludes properties marked with [NocturneOnly] attribute
    /// </summary>
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
}

/// <summary>
/// Extension methods for configuring Nightscout-compatible JSON serialization
/// </summary>
public static class NightscoutJsonExtensions
{
    /// <summary>
    /// Configures MVC to use Nightscout-compatible JSON formatting for v1-v3 endpoints.
    /// V4+ endpoints will use standard JSON serialization.
    /// </summary>
    public static IMvcBuilder AddNightscoutJsonFormatters(this IMvcBuilder builder)
    {
        builder.AddMvcOptions(options =>
        {
            // Insert our custom formatter at the beginning for Nightscout endpoints
            options.OutputFormatters.Insert(0, new NightscoutJsonOutputFormatter());
        });

        return builder;
    }
}
