using dotAPNS;

namespace Nocturne.API.Services;

/// <summary>
/// Factory interface for creating APNS clients
/// Allows mocking in tests while maintaining production behavior
/// </summary>
public interface IApnsClientFactory
{
    /// <summary>
    /// Creates an APNS client configured for the specified bundle ID
    /// </summary>
    /// <param name="bundleId">iOS app bundle identifier</param>
    /// <returns>Configured APNS client, or null if configuration is invalid</returns>
    IApnsClient? CreateClient(string bundleId);

    /// <summary>
    /// Checks if the factory is properly configured
    /// </summary>
    bool IsConfigured { get; }
}
