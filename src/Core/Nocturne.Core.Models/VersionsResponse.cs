namespace Nocturne.Core.Models;

/// <summary>
/// Versions list response model that maintains 1:1 compatibility with Nightscout /api/versions endpoint
/// </summary>
public class VersionsResponse
{
    /// <summary>
    /// List of supported API versions
    /// </summary>
    public List<string> Versions { get; set; } = new();
}
