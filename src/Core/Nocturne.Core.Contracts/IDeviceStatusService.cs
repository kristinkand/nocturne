using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts;

/// <summary>
/// Domain service for device status operations with WebSocket broadcasting
/// </summary>
public interface IDeviceStatusService
{
    /// <summary>
    /// Get device status entries with optional filtering and pagination
    /// </summary>
    /// <param name="find">Optional MongoDB query filter</param>
    /// <param name="count">Maximum number of device status entries to return</param>
    /// <param name="skip">Number of device status entries to skip for pagination</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Collection of device status entries</returns>
    Task<IEnumerable<DeviceStatus>> GetDeviceStatusAsync(
        string? find = null,
        int? count = null,
        int? skip = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get a specific device status entry by ID
    /// </summary>
    /// <param name="id">Device status ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Device status if found, null otherwise</returns>
    Task<DeviceStatus?> GetDeviceStatusByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Create new device status entries with WebSocket broadcasting
    /// </summary>
    /// <param name="deviceStatusEntries">Device status entries to create</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Created device status entries with assigned IDs</returns>
    Task<IEnumerable<DeviceStatus>> CreateDeviceStatusAsync(
        IEnumerable<DeviceStatus> deviceStatusEntries,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Update an existing device status entry with WebSocket broadcasting
    /// </summary>
    /// <param name="id">Device status ID to update</param>
    /// <param name="deviceStatus">Updated device status data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Updated device status if successful, null otherwise</returns>
    Task<DeviceStatus?> UpdateDeviceStatusAsync(
        string id,
        DeviceStatus deviceStatus,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Delete a device status entry with WebSocket broadcasting
    /// </summary>
    /// <param name="id">Device status ID to delete</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if deleted successfully, false otherwise</returns>
    Task<bool> DeleteDeviceStatusAsync(string id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Delete multiple device status entries with optional filtering
    /// </summary>
    /// <param name="find">Optional MongoDB query filter</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Number of device status entries deleted</returns>
    Task<long> DeleteDeviceStatusEntriesAsync(
        string? find = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Get the most recent device status entries
    /// </summary>
    /// <param name="count">Number of recent entries to retrieve</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Most recent device status entries</returns>
    Task<IEnumerable<DeviceStatus>> GetRecentDeviceStatusAsync(
        int count = 10,
        CancellationToken cancellationToken = default
    );
}
