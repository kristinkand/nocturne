using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;

namespace Nocturne.API.Services;

/// <summary>
/// Interface for the alert rules engine that evaluates glucose data against user-defined thresholds
/// </summary>
public interface IAlertRulesEngine
{
    /// <summary>
    /// Evaluates glucose data against all active rules for a user and generates alert events
    /// </summary>
    /// <param name="glucoseReading">Glucose reading to evaluate</param>
    /// <param name="userId">User identifier to get rules for</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of alert events generated from rule evaluation</returns>
    Task<List<AlertEvent>> EvaluateGlucoseData(
        Entry glucoseReading,
        string userId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Gets all active alert rules for a specific user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Array of active alert rules</returns>
    Task<AlertRuleEntity[]> GetActiveRulesForUser(
        string userId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Checks if an alert condition is met for a glucose reading against a specific rule
    /// </summary>
    /// <param name="glucoseReading">Glucose reading to evaluate</param>
    /// <param name="rule">Alert rule to check against</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if alert condition is met</returns>
    Task<bool> IsAlertConditionMet(
        Entry glucoseReading,
        AlertRuleEntity rule,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Checks if a user is currently in their quiet hours
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="checkTime">Time to check (defaults to current time)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if user is in quiet hours</returns>
    Task<bool> IsUserInQuietHours(
        string userId,
        DateTime? checkTime = null,
        CancellationToken cancellationToken = default
    );

    /// <summary>
    /// Evaluates time-based conditions for an alert rule
    /// </summary>
    /// <param name="rule">Alert rule to evaluate</param>
    /// <param name="checkTime">Time to check against rule conditions</param>
    /// <returns>True if time-based conditions are met</returns>
    bool EvaluateTimeBasedConditions(AlertRuleEntity rule, DateTime checkTime);
}
