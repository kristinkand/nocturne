using Microsoft.EntityFrameworkCore;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Mappers;

namespace Nocturne.Infrastructure.Data.Repositories;

/// <summary>
/// PostgreSQL repository for Activity operations
/// </summary>
public class ActivityRepository
{
    private readonly NocturneDbContext _context;

    /// <summary>
    /// Initializes a new instance of the ActivityRepository class
    /// </summary>
    /// <param name="context">The database context</param>
    public ActivityRepository(NocturneDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get activities with optional filtering and pagination
    /// </summary>
    public async Task<IEnumerable<Activity>> GetActivitiesAsync(
        string? type = null,
        int count = 10,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.Activities.AsQueryable();

        // Apply type filter if specified
        if (!string.IsNullOrEmpty(type))
        {
            query = query.Where(a => a.Type == type);
        }

        // Order by Mills descending (most recent first), then apply pagination
        var entities = await query
            .OrderByDescending(a => a.Mills)
            .Skip(skip)
            .Take(count)
            .ToListAsync(cancellationToken);

        return entities.Select(ActivityMapper.ToDomainModel);
    }

    /// <summary>
    /// Get activities with advanced filtering and search capabilities
    /// </summary>
    public async Task<IEnumerable<Activity>> GetActivitiesWithAdvancedFilterAsync(
        int count = 10,
        int skip = 0,
        string? findQuery = null,
        bool reverseResults = false,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.Activities.AsQueryable();

        // Apply search query if specified
        if (!string.IsNullOrEmpty(findQuery))
        {
            // Search in type, description, notes, and enteredBy fields
            query = query.Where(a =>
                (a.Type != null && a.Type.Contains(findQuery))
                || (a.Description != null && a.Description.Contains(findQuery))
                || (a.Notes != null && a.Notes.Contains(findQuery))
                || (a.EnteredBy != null && a.EnteredBy.Contains(findQuery))
            );
        }

        // Apply ordering - reverse if requested
        if (reverseResults)
        {
            query = query.OrderBy(a => a.Mills);
        }
        else
        {
            query = query.OrderByDescending(a => a.Mills);
        }

        // Apply pagination
        var entities = await query.Skip(skip).Take(count).ToListAsync(cancellationToken);

        return entities.Select(ActivityMapper.ToDomainModel);
    }

    /// <summary>
    /// Get activity by ID
    /// </summary>
    public async Task<Activity?> GetActivityByIdAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        // Try to find by original MongoDB ID first
        var entity = await _context.Activities.FirstOrDefaultAsync(
            a => a.OriginalId == id,
            cancellationToken
        );

        // If not found by original ID, try parsing as GUID
        if (entity == null && Guid.TryParse(id, out var guidId))
        {
            entity = await _context.Activities.FirstOrDefaultAsync(
                a => a.Id == guidId,
                cancellationToken
            );
        }

        return entity != null ? ActivityMapper.ToDomainModel(entity) : null;
    }

    /// <summary>
    /// Create new activities
    /// </summary>
    public async Task<IEnumerable<Activity>> CreateActivitiesAsync(
        IEnumerable<Activity> activities,
        CancellationToken cancellationToken = default
    )
    {
        var entities = activities.Select(ActivityMapper.ToEntity).ToList();

        await _context.Activities.AddRangeAsync(entities, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return entities.Select(ActivityMapper.ToDomainModel);
    }

    /// <summary>
    /// Update existing activity
    /// </summary>
    public async Task<Activity?> UpdateActivityAsync(
        string id,
        Activity activity,
        CancellationToken cancellationToken = default
    )
    {
        // Find existing entity
        var entity = await _context.Activities.FirstOrDefaultAsync(
            a => a.OriginalId == id,
            cancellationToken
        );

        if (entity == null && Guid.TryParse(id, out var guidId))
        {
            entity = await _context.Activities.FirstOrDefaultAsync(
                a => a.Id == guidId,
                cancellationToken
            );
        }

        if (entity == null)
        {
            return null;
        }

        // Update entity with new data
        ActivityMapper.UpdateEntity(entity, activity);

        await _context.SaveChangesAsync(cancellationToken);

        return ActivityMapper.ToDomainModel(entity);
    }

    /// <summary>
    /// Delete activity by ID
    /// </summary>
    public async Task<bool> DeleteActivityAsync(
        string id,
        CancellationToken cancellationToken = default
    )
    {
        // Find existing entity
        var entity = await _context.Activities.FirstOrDefaultAsync(
            a => a.OriginalId == id,
            cancellationToken
        );

        if (entity == null && Guid.TryParse(id, out var guidId))
        {
            entity = await _context.Activities.FirstOrDefaultAsync(
                a => a.Id == guidId,
                cancellationToken
            );
        }

        if (entity == null)
        {
            return false;
        }

        _context.Activities.Remove(entity);
        await _context.SaveChangesAsync(cancellationToken);

        return true;
    }

    /// <summary>
    /// Count activities with optional filtering
    /// </summary>
    public async Task<long> CountActivitiesAsync(
        string? findQuery = null,
        CancellationToken cancellationToken = default
    )
    {
        var query = _context.Activities.AsQueryable();

        // Apply search query if specified
        if (!string.IsNullOrEmpty(findQuery))
        {
            query = query.Where(a =>
                (a.Type != null && a.Type.Contains(findQuery))
                || (a.Description != null && a.Description.Contains(findQuery))
                || (a.Notes != null && a.Notes.Contains(findQuery))
                || (a.EnteredBy != null && a.EnteredBy.Contains(findQuery))
            );
        }

        return await query.CountAsync(cancellationToken);
    }
}
