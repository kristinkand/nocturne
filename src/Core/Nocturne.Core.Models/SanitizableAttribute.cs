namespace Nocturne.Core.Models;

/// <summary>
/// Attribute to mark properties that should be sanitized for HTML content
/// </summary>
[AttributeUsage(AttributeTargets.Property)]
public sealed class SanitizableAttribute : Attribute { }
