using Microsoft.EntityFrameworkCore;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Repositories;

/// <summary>
/// PostgreSQL repository for AlertHistory operations
/// </summary>
public class AlertHistoryRepository
{
    private readonly NocturneDbContext _context;

    /// <summary>
    /// Initializes a new instance of the AlertHistoryRepository class
    /// </summary>
    /// <param name="context">The database context</param>
    public AlertHistoryRepository(NocturneDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get active alerts for a specific user
    /// </summary>
    public virtual async Task<AlertHistoryEntity[]> GetActiveAlertsForUserAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .AlertHistory.Include(h => h.AlertRule)
            .Where(h =>
                h.UserId == userId
                && h.Status == "ACTIVE"
                && (h.SnoozeUntil == null || h.SnoozeUntil <= DateTime.UtcNow)
            )
            .OrderByDescending(h => h.TriggerTime)
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Get alert history for a specific user with pagination
    /// </summary>
    public async Task<AlertHistoryEntity[]> GetAlertHistoryForUserAsync(
        string userId,
        int count = 50,
        int skip = 0,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .AlertHistory.Include(h => h.AlertRule)
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.TriggerTime)
            .Skip(skip)
            .Take(count)
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Get a specific alert by ID
    /// </summary>
    public async Task<AlertHistoryEntity?> GetAlertByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .AlertHistory.Include(h => h.AlertRule)
            .FirstOrDefaultAsync(h => h.Id == id, cancellationToken);
    }

    /// <summary>
    /// Create a new alert history entry
    /// </summary>
    public virtual async Task<AlertHistoryEntity> CreateAlertAsync(
        AlertHistoryEntity alert,
        CancellationToken cancellationToken = default
    )
    {
        alert.Id = Guid.CreateVersion7();
        alert.CreatedAt = DateTime.UtcNow;
        alert.UpdatedAt = DateTime.UtcNow;

        _context.AlertHistory.Add(alert);
        await _context.SaveChangesAsync(cancellationToken);

        return alert;
    }

    /// <summary>
    /// Update alert status (acknowledge, resolve, etc.)
    /// </summary>
    public virtual async Task<AlertHistoryEntity?> UpdateAlertStatusAsync(
        Guid id,
        string status,
        DateTime? acknowledgedAt = null,
        DateTime? resolvedAt = null,
        DateTime? snoozeUntil = null,
        CancellationToken cancellationToken = default
    )
    {
        var alert = await _context.AlertHistory.FirstOrDefaultAsync(
            h => h.Id == id,
            cancellationToken
        );

        if (alert == null)
            return null;

        alert.Status = status;
        alert.UpdatedAt = DateTime.UtcNow;

        if (acknowledgedAt.HasValue)
            alert.AcknowledgedAt = acknowledgedAt.Value;

        if (resolvedAt.HasValue)
            alert.ResolvedAt = resolvedAt.Value;

        if (snoozeUntil.HasValue)
            alert.SnoozeUntil = snoozeUntil.Value;

        await _context.SaveChangesAsync(cancellationToken);

        return alert;
    }

    /// <summary>
    /// Resolve alerts for a specific user and alert type
    /// </summary>
    public virtual async Task<int> ResolveAlertsAsync(
        string userId,
        string alertType,
        CancellationToken cancellationToken = default
    )
    {
        var activeAlerts = await _context
            .AlertHistory.Where(h =>
                h.UserId == userId && h.AlertType == alertType && h.Status == "ACTIVE"
            )
            .ToListAsync(cancellationToken);

        var resolvedCount = 0;
        var now = DateTime.UtcNow;

        foreach (var alert in activeAlerts)
        {
            alert.Status = "RESOLVED";
            alert.ResolvedAt = now;
            alert.UpdatedAt = now;
            resolvedCount++;
        }

        if (resolvedCount > 0)
        {
            await _context.SaveChangesAsync(cancellationToken);
        }

        return resolvedCount;
    }

    /// <summary>
    /// Get count of active alerts for a user
    /// </summary>
    public virtual async Task<int> GetActiveAlertCountForUserAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.AlertHistory.CountAsync(
            h =>
                h.UserId == userId
                && h.Status == "ACTIVE"
                && (h.SnoozeUntil == null || h.SnoozeUntil <= DateTime.UtcNow),
            cancellationToken
        );
    }

    /// <summary>
    /// Check if there's an active alert for a specific rule and type
    /// </summary>
    public virtual async Task<AlertHistoryEntity?> GetActiveAlertForRuleAndTypeAsync(
        string userId,
        Guid? alertRuleId,
        string alertType,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.AlertHistory.FirstOrDefaultAsync(
            h =>
                h.UserId == userId
                && h.AlertRuleId == alertRuleId
                && h.AlertType == alertType
                && h.Status == "ACTIVE"
                && (h.SnoozeUntil == null || h.SnoozeUntil <= DateTime.UtcNow),
            cancellationToken
        );
    }

    /// <summary>
    /// Update escalation level for an alert
    /// </summary>
    public virtual async Task<bool> UpdateEscalationLevelAsync(
        Guid id,
        int escalationLevel,
        CancellationToken cancellationToken = default
    )
    {
        var alert = await _context.AlertHistory.FirstOrDefaultAsync(
            h => h.Id == id,
            cancellationToken
        );

        if (alert == null)
            return false;

        alert.EscalationLevel = escalationLevel;
        alert.UpdatedAt = DateTime.UtcNow;

        var result = await _context.SaveChangesAsync(cancellationToken);
        return result > 0;
    }

    /// <summary>
    /// Update notifications sent tracking for an alert
    /// </summary>
    public virtual async Task<bool> UpdateNotificationsSentAsync(
        Guid id,
        string notificationsSent,
        CancellationToken cancellationToken = default
    )
    {
        var alert = await _context.AlertHistory.FirstOrDefaultAsync(
            h => h.Id == id,
            cancellationToken
        );

        if (alert == null)
            return false;

        alert.NotificationsSent = notificationsSent;
        alert.UpdatedAt = DateTime.UtcNow;

        var result = await _context.SaveChangesAsync(cancellationToken);
        return result > 0;
    }

    /// <summary>
    /// Delete old resolved alerts to maintain database size
    /// </summary>
    public virtual async Task<int> CleanupOldAlertsAsync(
        int daysToKeep = 30,
        CancellationToken cancellationToken = default
    )
    {
        var cutoffDate = DateTime.UtcNow.AddDays(-daysToKeep);

        var deletedCount = await _context
            .AlertHistory.Where(h => h.Status == "RESOLVED" && h.ResolvedAt < cutoffDate)
            .ExecuteDeleteAsync(cancellationToken);

        return deletedCount;
    }

    /// <summary>
    /// Get all active alerts for a user (compatible with escalation services)
    /// </summary>
    public virtual async Task<List<AlertHistoryEntity>> GetActiveAlertsByUserIdAsync(string userId)
    {
        return await _context
            .AlertHistory.Include(h => h.AlertRule)
            .Where(h => h.UserId == userId && (h.Status == "ACTIVE" || h.Status == "SNOOZED"))
            .OrderByDescending(h => h.TriggerTime)
            .ToListAsync();
    }

    /// <summary>
    /// Get alert by ID (compatible with escalation services)
    /// </summary>
    public virtual async Task<AlertHistoryEntity?> GetByIdAsync(Guid id)
    {
        return await _context
            .AlertHistory.Include(h => h.AlertRule)
            .FirstOrDefaultAsync(h => h.Id == id);
    }

    /// <summary>
    /// Update an alert (compatible with escalation services)
    /// </summary>
    public virtual async Task UpdateAsync(AlertHistoryEntity alert)
    {
        alert.UpdatedAt = DateTime.UtcNow;
        _context.AlertHistory.Update(alert);
        await _context.SaveChangesAsync();
    }

    /// <summary>
    /// Get expired snoozed alerts that need reactivation
    /// </summary>
    public virtual async Task<List<AlertHistoryEntity>> GetExpiredSnoozedAlertsAsync()
    {
        return await _context
            .AlertHistory.Where(h =>
                h.Status == "SNOOZED"
                && h.SnoozeUntil.HasValue
                && h.SnoozeUntil.Value <= DateTime.UtcNow
            )
            .ToListAsync();
    }

    /// <summary>
    /// Get alerts that need escalation processing
    /// </summary>
    public virtual async Task<List<AlertHistoryEntity>> GetAlertsForEscalationAsync()
    {
        return await _context
            .AlertHistory.Where(h =>
                h.Status == "ACTIVE"
                && !h.EscalationPaused
                && (h.NextEscalationTime == null || h.NextEscalationTime <= DateTime.UtcNow)
            )
            .ToListAsync();
    }
}
