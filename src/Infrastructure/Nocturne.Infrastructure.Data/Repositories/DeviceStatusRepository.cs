using System.Net;
using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Mappers;

namespace Nocturne.Infrastructure.Data.Repositories;

/// <summary>
/// PostgreSQL repository for DeviceStatus operations
/// </summary>
public class DeviceStatusRepository
{
    private readonly NocturneDbContext _context;

    /// <summary>
    /// Initializes a new instance of the DeviceStatusRepository class
    /// </summary>
    /// <param name="context">The database context</param>
    public DeviceStatusRepository(NocturneDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get device status entries with optional filtering and pagination
    /// </summary>
    public async Task<IEnumerable<DeviceStatus>> GetDeviceStatusAsync(
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        // Order by Mills descending (most recent first), then apply pagination
        var entities = await _context
            .DeviceStatuses.OrderByDescending(ds => ds.Mills)
            .Skip(skip)
            .Take(count)
            .ToListAsync(cancellationToken);

        return entities.Select(DeviceStatusMapper.ToDomainModel);
    }

    /// <summary>
    /// Get a specific device status by ID
    /// </summary>
    public async Task<DeviceStatus?> GetDeviceStatusByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        DeviceStatusEntity? entity;

        if (Guid.TryParse(id, out var guidId))
        {
            // Try to find by GUID ID first
            entity = await _context.DeviceStatuses.FirstOrDefaultAsync(
                ds => ds.Id == guidId,
                cancellationToken
            );
        }
        else
        {
            // Try to find by original MongoDB ID
            entity = await _context.DeviceStatuses.FirstOrDefaultAsync(
                ds => ds.OriginalId == id,
                cancellationToken
            );
        }

        return entity != null ? DeviceStatusMapper.ToDomainModel(entity) : null;
    }

    /// <summary>
    /// Get device status entries with advanced filtering support including find queries and reverse ordering
    /// </summary>
    public async Task<IEnumerable<DeviceStatus>> GetDeviceStatusWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.DeviceStatuses.AsQueryable();

        // Apply find query filtering if specified
        // Note: This is a simplified implementation. Full MongoDB query parsing would be more complex.
        if (!string.IsNullOrEmpty(findQuery))
        {
            // Basic device filter support - could be extended for more complex queries
            if (findQuery.Contains("device"))
            {
                // Extract device value from query string - simplified parsing
                var deviceMatch = System.Text.RegularExpressions.Regex.Match(
                    findQuery,
                    @"device[^=]*=([^&]*)"
                );
                if (deviceMatch.Success)
                {
                    var deviceValue = WebUtility.UrlDecode(deviceMatch.Groups[1].Value);
                    query = query.Where(ds => ds.Device.Contains(deviceValue));
                }
            }
        }

        // Apply ordering
        if (reverseResults)
        {
            query = query.OrderBy(ds => ds.Mills);
        }
        else
        {
            query = query.OrderByDescending(ds => ds.Mills);
        }

        // Apply pagination
        var entities = await query.Skip(skip).Take(count).ToListAsync(cancellationToken);

        return entities.Select(DeviceStatusMapper.ToDomainModel);
    }

    /// <summary>
    /// Create multiple device status entries
    /// </summary>
    public async Task<IEnumerable<DeviceStatus>> CreateDeviceStatusAsync(
        IEnumerable<DeviceStatus> deviceStatuses,
        CancellationToken cancellationToken = default
    )
    {
        var entities = deviceStatuses.Select(DeviceStatusMapper.ToEntity).ToList();

        await _context.DeviceStatuses.AddRangeAsync(entities, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return entities.Select(DeviceStatusMapper.ToDomainModel);
    }

    /// <summary>
    /// Update an existing device status by ID
    /// </summary>
    public async Task<DeviceStatus?> UpdateDeviceStatusAsync(
        string id,
        DeviceStatus deviceStatus,
        CancellationToken cancellationToken = default
    )
    {
        DeviceStatusEntity? entity;

        if (Guid.TryParse(id, out var guidId))
        {
            entity = await _context.DeviceStatuses.FirstOrDefaultAsync(
                ds => ds.Id == guidId,
                cancellationToken
            );
        }
        else
        {
            entity = await _context.DeviceStatuses.FirstOrDefaultAsync(
                ds => ds.OriginalId == id,
                cancellationToken
            );
        }

        if (entity == null)
            return null;

        DeviceStatusMapper.UpdateEntity(entity, deviceStatus);
        await _context.SaveChangesAsync(cancellationToken);

        return DeviceStatusMapper.ToDomainModel(entity);
    }

    /// <summary>
    /// Delete a device status by ID
    /// </summary>
    public async Task<bool> DeleteDeviceStatusAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        DeviceStatusEntity? entity;

        if (Guid.TryParse(id, out var guidId))
        {
            entity = await _context.DeviceStatuses.FirstOrDefaultAsync(
                ds => ds.Id == guidId,
                cancellationToken
            );
        }
        else
        {
            entity = await _context.DeviceStatuses.FirstOrDefaultAsync(
                ds => ds.OriginalId == id,
                cancellationToken
            );
        }

        if (entity == null)
            return false;

        _context.DeviceStatuses.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);
        return true;
    }

    /// <summary>
    /// Bulk delete device status entries using query filters
    /// </summary>
    public async Task<long> BulkDeleteDeviceStatusAsync(
        string findQuery,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.DeviceStatuses.AsQueryable();

        // Apply find query filtering if specified
        if (!string.IsNullOrEmpty(findQuery))
        {
            // Basic device filter support - could be extended for more complex queries
            if (findQuery.Contains("device"))
            {
                var deviceMatch = System.Text.RegularExpressions.Regex.Match(
                    findQuery,
                    @"device[^=]*=([^&]*)"
                );
                if (deviceMatch.Success)
                {
                    var deviceValue = WebUtility.UrlDecode(deviceMatch.Groups[1].Value);
                    query = query.Where(ds => ds.Device.Contains(deviceValue));
                }
            }
        }

        var entitiesToDelete = await query.ToListAsync(cancellationToken);
        var deletedCount = entitiesToDelete.Count;

        if (deletedCount > 0)
        {
            _context.DeviceStatuses.RemoveRange(entitiesToDelete);
            await _context.SaveChangesAsync(cancellationToken);
        }

        return deletedCount;
    }

    /// <summary>
    /// Count device status entries matching specific criteria
    /// </summary>
    public async Task<long> CountDeviceStatusAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.DeviceStatuses.AsQueryable();

        // Apply find query filtering if specified
        if (!string.IsNullOrEmpty(findQuery))
        {
            // Basic device filter support - could be extended for more complex queries
            if (findQuery.Contains("device"))
            {
                var deviceMatch = System.Text.RegularExpressions.Regex.Match(
                    findQuery,
                    @"device[^=]*=([^&]*)"
                );
                if (deviceMatch.Success)
                {
                    var deviceValue = WebUtility.UrlDecode(deviceMatch.Groups[1].Value);
                    query = query.Where(ds => ds.Device.Contains(deviceValue));
                }
            }
        }

        return await query.CountAsync(cancellationToken);
    }
}
