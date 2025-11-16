using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts;

/// <summary>
/// Domain service for profile data operations with WebSocket broadcasting
/// This handles CRUD operations for profile data, separate from profile calculations
/// </summary>
public interface IProfileDataService
{
    /// <summary>
    /// Get profiles with optional filtering and pagination
    /// </summary>
    /// <param name="find">Optional MongoDB query filter</param>
    /// <param name="count">Maximum number of profiles to return</param>
    /// <param name="skip">Number of profiles to skip for pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of profiles</returns>
    Task<IEnumerable<Profile>> GetProfilesAsync(
        string? find = null,
        int? count = null,
        int? skip = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get a specific profile by ID
    /// </summary>
    /// <param name="id">Profile ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Profile if found, null otherwise</returns>
    Task<Profile?> GetProfileByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Get the current active profile
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Current active profile if exists, null otherwise</returns>
    Task<Profile?> GetCurrentProfileAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new profiles with WebSocket broadcasting
    /// </summary>
    /// <param name="profiles">Profiles to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created profiles with assigned IDs</returns>
    Task<IEnumerable<Profile>> CreateProfilesAsync(
        IEnumerable<Profile> profiles,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update an existing profile with WebSocket broadcasting
    /// </summary>
    /// <param name="id">Profile ID to update</param>
    /// <param name="profile">Updated profile data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated profile if successful, null otherwise</returns>
    Task<Profile?> UpdateProfileAsync(
        string id,
        Profile profile,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete a profile with WebSocket broadcasting
    /// </summary>
    /// <param name="id">Profile ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted successfully, false otherwise</returns>
    Task<bool> DeleteProfileAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete multiple profiles with optional filtering
    /// </summary>
    /// <param name="find">Optional MongoDB query filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of profiles deleted</returns>
    Task<long> DeleteProfilesAsync(
        string? find = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get profile active at a specific timestamp
    /// </summary>
    /// <param name="timestamp">Unix timestamp for profile lookup</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Profile active at the specified timestamp if found, null otherwise</returns>
    Task<Profile?> GetProfileAtTimestampAsync(
        long timestamp,
        CancellationToken cancellationToken = default
    );
}
