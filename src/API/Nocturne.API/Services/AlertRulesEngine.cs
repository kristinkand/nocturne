using System.Text.Json;
using Microsoft.Extensions.Options;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Entities;
using Nocturne.Infrastructure.Data.Repositories;

namespace Nocturne.API.Services;

/// <summary>
/// Alert rules engine implementation that evaluates glucose thresholds and generates alerts
/// </summary>
public class AlertRulesEngine : IAlertRulesEngine
{
    private readonly AlertRuleRepository _alertRuleRepository;
    private readonly AlertHistoryRepository _alertHistoryRepository;
    private readonly NotificationPreferencesRepository _notificationPreferencesRepository;
    private readonly AlertMonitoringOptions _options;
    private readonly ILogger<AlertRulesEngine> _logger;

    public AlertRulesEngine(
        AlertRuleRepository alertRuleRepository,
        AlertHistoryRepository alertHistoryRepository,
        NotificationPreferencesRepository notificationPreferencesRepository,
        IOptions<AlertMonitoringOptions> options,
        ILogger<AlertRulesEngine> logger
    )
    {
        _alertRuleRepository = alertRuleRepository;
        _alertHistoryRepository = alertHistoryRepository;
        _notificationPreferencesRepository = notificationPreferencesRepository;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<AlertEvent>> EvaluateGlucoseData(
        Entry glucoseReading,
        string userId,
        CancellationToken cancellationToken
    )
    {
        var alertEvents = new List<AlertEvent>();

        try
        {
            // Get active rules for the user
            var activeRules = await GetActiveRulesForUser(userId, cancellationToken);

            if (activeRules.Length == 0)
            {
                _logger.LogDebug("No active alert rules found for user {UserId}", userId);
                return alertEvents;
            }

            // Check if user is in quiet hours
            var isInQuietHours = await IsUserInQuietHours(
                userId,
                glucoseReading.Date,
                cancellationToken
            );
            if (isInQuietHours)
            {
                _logger.LogDebug(
                    "User {UserId} is in quiet hours, skipping alert evaluation",
                    userId
                );
                return alertEvents;
            }

            // Check if we've hit the maximum active alerts for this user
            var activeAlertCount = await _alertHistoryRepository.GetActiveAlertCountForUserAsync(
                userId,
                cancellationToken
            );
            if (activeAlertCount >= _options.MaxActiveAlertsPerUser)
            {
                _logger.LogWarning(
                    "User {UserId} has reached maximum active alerts ({Count}), skipping new alert evaluation",
                    userId,
                    activeAlertCount
                );
                return alertEvents;
            }

            // Evaluate each rule
            foreach (var rule in activeRules)
            {
                try
                {
                    // Check time-based conditions
                    if (!EvaluateTimeBasedConditions(rule, glucoseReading.Date ?? DateTime.UtcNow))
                    {
                        _logger.LogDebug(
                            "Time-based conditions not met for rule {RuleId} for user {UserId}",
                            rule.Id,
                            userId
                        );
                        continue;
                    }

                    // Check alert conditions
                    if (await IsAlertConditionMet(glucoseReading, rule, cancellationToken))
                    {
                        var alertEvent = await CreateAlertEvent(
                            glucoseReading,
                            rule,
                            cancellationToken
                        );
                        if (alertEvent != null)
                        {
                            alertEvents.Add(alertEvent);
                            _logger.LogInformation(
                                "Generated {AlertType} alert for user {UserId} with glucose {GlucoseValue} mg/dL",
                                alertEvent.AlertType,
                                userId,
                                alertEvent.GlucoseValue
                            );
                        }
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogError(
                        ex,
                        "Error evaluating rule {RuleId} for user {UserId}",
                        rule.Id,
                        userId
                    );
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error evaluating glucose data for user {UserId}", userId);
        }

        return alertEvents;
    }

    /// <inheritdoc />
    public async Task<AlertRuleEntity[]> GetActiveRulesForUser(
        string userId,
        CancellationToken cancellationToken
    )
    {
        return await _alertRuleRepository.GetActiveRulesForUserAsync(userId, cancellationToken);
    }

    /// <inheritdoc />
    public async Task<bool> IsAlertConditionMet(
        Entry glucoseReading,
        AlertRuleEntity rule,
        CancellationToken cancellationToken
    )
    {
        var glucoseValue = (decimal)(glucoseReading.Sgv ?? glucoseReading.Mgdl);

        // Check each threshold type
        var alertTypes = new[]
        {
            (AlertType.UrgentLow, rule.UrgentLowThreshold),
            (AlertType.UrgentHigh, rule.UrgentHighThreshold),
            (AlertType.Low, rule.LowThreshold),
            (AlertType.High, rule.HighThreshold),
        };

        foreach (var (alertType, threshold) in alertTypes)
        {
            if (!threshold.HasValue)
                continue;

            var isConditionMet = alertType switch
            {
                AlertType.Low or AlertType.UrgentLow => glucoseValue <= threshold.Value,
                AlertType.High or AlertType.UrgentHigh => glucoseValue >= threshold.Value,
                _ => false,
            };

            if (isConditionMet)
            {
                // Check for existing active alert to prevent spam
                var existingAlert = await _alertHistoryRepository.GetActiveAlertForRuleAndTypeAsync(
                    rule.UserId,
                    rule.Id,
                    alertType.ToString(),
                    cancellationToken
                );

                if (existingAlert != null)
                {
                    // Check if enough time has passed for re-alerting (cooldown)
                    var timeSinceLastAlert = DateTime.UtcNow - existingAlert.TriggerTime;
                    if (timeSinceLastAlert.TotalMinutes < _options.AlertCooldownMinutes)
                    {
                        _logger.LogDebug(
                            "Alert cooldown active for {AlertType} alert for user {UserId}",
                            alertType,
                            rule.UserId
                        );
                        continue;
                    }
                }

                // Apply hysteresis to prevent oscillating alerts
                if (
                    existingAlert != null
                    && ShouldApplyHysteresis(glucoseValue, threshold.Value, alertType)
                )
                {
                    _logger.LogDebug(
                        "Hysteresis prevents {AlertType} alert for user {UserId} (glucose: {GlucoseValue}, threshold: {Threshold})",
                        alertType,
                        rule.UserId,
                        glucoseValue,
                        threshold.Value
                    );
                    continue;
                }

                return true;
            }
        }

        return false;
    }

    /// <inheritdoc />
    public async Task<bool> IsUserInQuietHours(
        string userId,
        DateTime? checkTime = null,
        CancellationToken cancellationToken = default
    )
    {
        return await _notificationPreferencesRepository.IsUserInQuietHoursAsync(
            userId,
            checkTime,
            cancellationToken
        );
    }

    /// <inheritdoc />
    public bool EvaluateTimeBasedConditions(AlertRuleEntity rule, DateTime checkTime)
    {
        try
        {
            // Check active hours
            if (!string.IsNullOrEmpty(rule.ActiveHours))
            {
                var activeHours = JsonSerializer.Deserialize<ActiveHoursConfig>(rule.ActiveHours);
                if (activeHours != null && !IsWithinActiveHours(checkTime.TimeOfDay, activeHours))
                {
                    return false;
                }
            }

            // Check days of week
            if (!string.IsNullOrEmpty(rule.DaysOfWeek))
            {
                var daysOfWeek = JsonSerializer.Deserialize<int[]>(rule.DaysOfWeek);
                if (daysOfWeek != null && !daysOfWeek.Contains((int)checkTime.DayOfWeek))
                {
                    return false;
                }
            }

            return true;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(
                ex,
                "Error evaluating time-based conditions for rule {RuleId}, allowing alert",
                rule.Id
            );
            return true; // Fail safe - allow alert if we can't parse conditions
        }
    }

    private async Task<AlertEvent?> CreateAlertEvent(
        Entry glucoseReading,
        AlertRuleEntity rule,
        CancellationToken cancellationToken
    )
    {
        var glucoseValue = (decimal)(glucoseReading.Sgv ?? glucoseReading.Mgdl);

        // Determine alert type and threshold
        var (alertType, threshold) = DetermineAlertTypeAndThreshold(glucoseValue, rule);
        if (!threshold.HasValue)
            return null;

        // Check for duplicate active alert
        var existingAlert = await _alertHistoryRepository.GetActiveAlertForRuleAndTypeAsync(
            rule.UserId,
            rule.Id,
            alertType.ToString(),
            cancellationToken
        );

        if (existingAlert != null)
        {
            var timeSinceLastAlert = DateTime.UtcNow - existingAlert.TriggerTime;
            if (timeSinceLastAlert.TotalMinutes < _options.AlertCooldownMinutes)
            {
                return null; // Skip due to cooldown
            }
        }

        return new AlertEvent
        {
            UserId = rule.UserId,
            AlertRuleId = rule.Id,
            AlertType = alertType,
            GlucoseValue = glucoseValue,
            Threshold = threshold.Value,
            TriggerTime = glucoseReading.Date ?? DateTime.UtcNow,
            Rule = rule,
            Context = new Dictionary<string, object>
            {
                { "EntryId", glucoseReading.Id ?? string.Empty },
                { "Direction", glucoseReading.Direction ?? string.Empty },
                { "Delta", glucoseReading.Delta ?? 0 },
            },
        };
    }

    private (AlertType alertType, decimal? threshold) DetermineAlertTypeAndThreshold(
        decimal glucoseValue,
        AlertRuleEntity rule
    )
    {
        // Check urgent thresholds first (highest priority)
        if (rule.UrgentLowThreshold.HasValue && glucoseValue <= rule.UrgentLowThreshold.Value)
        {
            return (AlertType.UrgentLow, rule.UrgentLowThreshold.Value);
        }

        if (rule.UrgentHighThreshold.HasValue && glucoseValue >= rule.UrgentHighThreshold.Value)
        {
            return (AlertType.UrgentHigh, rule.UrgentHighThreshold.Value);
        }

        // Check standard thresholds
        if (rule.LowThreshold.HasValue && glucoseValue <= rule.LowThreshold.Value)
        {
            return (AlertType.Low, rule.LowThreshold.Value);
        }

        if (rule.HighThreshold.HasValue && glucoseValue >= rule.HighThreshold.Value)
        {
            return (AlertType.High, rule.HighThreshold.Value);
        }

        return (AlertType.Low, null); // No threshold met
    }

    private bool ShouldApplyHysteresis(decimal currentValue, decimal threshold, AlertType alertType)
    {
        var hysteresisAmount = threshold * (decimal)_options.HysteresisPercentage;

        return alertType switch
        {
            AlertType.Low or AlertType.UrgentLow => currentValue > (threshold + hysteresisAmount),
            AlertType.High or AlertType.UrgentHigh => currentValue < (threshold - hysteresisAmount),
            _ => false,
        };
    }

    private bool IsWithinActiveHours(TimeSpan currentTime, ActiveHoursConfig activeHours)
    {
        var start = TimeSpan
            .FromHours(activeHours.StartHour)
            .Add(TimeSpan.FromMinutes(activeHours.StartMinute));
        var end = TimeSpan
            .FromHours(activeHours.EndHour)
            .Add(TimeSpan.FromMinutes(activeHours.EndMinute));

        // Handle active hours that span midnight
        if (start <= end)
        {
            return currentTime >= start && currentTime <= end;
        }
        else
        {
            return currentTime >= start || currentTime <= end;
        }
    }

    /// <summary>
    /// Configuration for active hours
    /// </summary>
    private class ActiveHoursConfig
    {
        public int StartHour { get; set; }
        public int StartMinute { get; set; }
        public int EndHour { get; set; }
        public int EndMinute { get; set; }
    }
}
