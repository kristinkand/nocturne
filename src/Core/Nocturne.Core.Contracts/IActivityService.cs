using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts;

/// <summary>
/// Domain service for activity operations with WebSocket broadcasting
/// </summary>
public interface IActivityService
{
    /// <summary>
    /// Get activity records with optional filtering and pagination
    /// </summary>
    /// <param name="find">Optional MongoDB query filter</param>
    /// <param name="count">Maximum number of records to return</param>
    /// <param name="skip">Number of records to skip for pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of activity records</returns>
    Task<IEnumerable<Activity>> GetActivitiesAsync(
        string? find = null,
        int? count = null,
        int? skip = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get a specific activity record by ID
    /// </summary>
    /// <param name="id">Activity ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Activity record if found, null otherwise</returns>
    Task<Activity?> GetActivityByIdAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Create new activity records with WebSocket broadcasting
    /// </summary>
    /// <param name="activities">Activity records to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created activity records with assigned IDs</returns>
    Task<IEnumerable<Activity>> CreateActivitiesAsync(
        IEnumerable<Activity> activities,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update an existing activity record with WebSocket broadcasting
    /// </summary>
    /// <param name="id">Activity ID to update</param>
    /// <param name="activity">Updated activity data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated activity record if successful, null otherwise</returns>
    Task<Activity?> UpdateActivityAsync(
        string id,
        Activity activity,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete an activity record with WebSocket broadcasting
    /// </summary>
    /// <param name="id">Activity ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted successfully, false otherwise</returns>
    Task<bool> DeleteActivityAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete multiple activity records with optional filtering
    /// </summary>
    /// <param name="find">Optional MongoDB query filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of records deleted</returns>
    Task<long> DeleteMultipleActivitiesAsync(
        string? find = null,
        CancellationToken cancellationToken = default
    );
}
