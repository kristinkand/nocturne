using Microsoft.EntityFrameworkCore;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.Infrastructure.Data.Repositories;

/// <summary>
/// PostgreSQL repository for AlertRule operations
/// </summary>
public class AlertRuleRepository
{
    private readonly NocturneDbContext _context;

    /// <summary>
    /// Initializes a new instance of the AlertRuleRepository class
    /// </summary>
    /// <param name="context">The database context</param>
    public AlertRuleRepository(NocturneDbContext context)
    {
        _context = context;
    }

    /// <summary>
    /// Get active alert rules for a specific user
    /// </summary>
    public virtual async Task<AlertRuleEntity[]> GetActiveRulesForUserAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .AlertRules.Where(r => r.UserId == userId && r.IsEnabled)
            .OrderBy(r => r.Name)
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Get all alert rules for a specific user
    /// </summary>
    public virtual async Task<AlertRuleEntity[]> GetRulesForUserAsync(
        string userId,
        CancellationToken cancellationToken = default
    )
    {
        return await _context
            .AlertRules.Where(r => r.UserId == userId)
            .OrderBy(r => r.Name)
            .ToArrayAsync(cancellationToken);
    }

    /// <summary>
    /// Get a specific alert rule by ID
    /// </summary>
    public virtual async Task<AlertRuleEntity?> GetRuleByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default
    )
    {
        return await _context.AlertRules.FirstOrDefaultAsync(r => r.Id == id, cancellationToken);
    }

    /// <summary>
    /// Create a new alert rule
    /// </summary>
    public virtual async Task<AlertRuleEntity> CreateRuleAsync(
        AlertRuleEntity rule,
        CancellationToken cancellationToken = default
    )
    {
        rule.Id = Guid.CreateVersion7();
        rule.CreatedAt = DateTime.UtcNow;
        rule.UpdatedAt = DateTime.UtcNow;

        _context.AlertRules.Add(rule);
        await _context.SaveChangesAsync(cancellationToken);

        return rule;
    }

    /// <summary>
    /// Update an existing alert rule
    /// </summary>
    public virtual async Task<AlertRuleEntity?> UpdateRuleAsync(
        Guid id,
        AlertRuleEntity updatedRule,
        CancellationToken cancellationToken = default
    )
    {
        var existingRule = await _context.AlertRules.FirstOrDefaultAsync(
            r => r.Id == id,
            cancellationToken
        );

        if (existingRule == null)
            return null;

        // Update properties
        existingRule.Name = updatedRule.Name;
        existingRule.IsEnabled = updatedRule.IsEnabled;
        existingRule.LowThreshold = updatedRule.LowThreshold;
        existingRule.HighThreshold = updatedRule.HighThreshold;
        existingRule.UrgentLowThreshold = updatedRule.UrgentLowThreshold;
        existingRule.UrgentHighThreshold = updatedRule.UrgentHighThreshold;
        existingRule.ActiveHours = updatedRule.ActiveHours;
        existingRule.DaysOfWeek = updatedRule.DaysOfWeek;
        existingRule.NotificationChannels = updatedRule.NotificationChannels;
        existingRule.EscalationDelayMinutes = updatedRule.EscalationDelayMinutes;
        existingRule.MaxEscalations = updatedRule.MaxEscalations;
        existingRule.DefaultSnoozeMinutes = updatedRule.DefaultSnoozeMinutes;
        existingRule.MaxSnoozeMinutes = updatedRule.MaxSnoozeMinutes;
        existingRule.UpdatedAt = DateTime.UtcNow;

        await _context.SaveChangesAsync(cancellationToken);

        return existingRule;
    }

    /// <summary>
    /// Delete an alert rule
    /// </summary>
    public async Task<bool> DeleteRuleAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var rule = await _context.AlertRules.FirstOrDefaultAsync(
            r => r.Id == id,
            cancellationToken
        );

        if (rule == null)
            return false;

        _context.AlertRules.Remove(rule);
        var result = await _context.SaveChangesAsync(cancellationToken);
        return result > 0;
    }

    /// <summary>
    /// Enable or disable an alert rule
    /// </summary>
    public async Task<bool> SetRuleEnabledAsync(
        Guid id,
        bool enabled,
        CancellationToken cancellationToken = default
    )
    {
        var rule = await _context.AlertRules.FirstOrDefaultAsync(
            r => r.Id == id,
            cancellationToken
        );

        if (rule == null)
            return false;

        rule.IsEnabled = enabled;
        rule.UpdatedAt = DateTime.UtcNow;

        var result = await _context.SaveChangesAsync(cancellationToken);
        return result > 0;
    }
}
