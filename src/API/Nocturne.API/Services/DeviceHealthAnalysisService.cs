using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data;

namespace Nocturne.API.Services;

/// <summary>
/// Service for device health analysis and monitoring
/// </summary>
public class DeviceHealthAnalysisService : IDeviceHealthAnalysisService
{
    private readonly NocturneDbContext _dbContext;
    private readonly ILogger<DeviceHealthAnalysisService> _logger;
    private readonly DeviceHealthOptions _options;
    private readonly IDeviceRegistryService _deviceRegistryService;

    /// <summary>
    /// Initializes a new instance of the DeviceHealthAnalysisService
    /// </summary>
    /// <param name="dbContext">Database context</param>
    /// <param name="logger">Logger</param>
    /// <param name="options">Device health options</param>
    /// <param name="deviceRegistryService">Device registry service</param>
    public DeviceHealthAnalysisService(
        NocturneDbContext dbContext,
        ILogger<DeviceHealthAnalysisService> logger,
        IOptions<DeviceHealthOptions> options,
        IDeviceRegistryService deviceRegistryService
    )
    {
        _dbContext = dbContext;
        _logger = logger;
        _options = options.Value;
        _deviceRegistryService = deviceRegistryService;
    }

    /// <summary>
    /// Analyze device health and generate health score
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Device health analysis result</returns>
    public async Task<DeviceHealthAnalysis> AnalyzeDeviceHealthAsync(
        string deviceId,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));

        _logger.LogDebug("Analyzing device health for device {DeviceId}", deviceId);

        var device = await _deviceRegistryService.GetDeviceAsync(deviceId, cancellationToken);
        if (device == null)
        {
            throw new InvalidOperationException($"Device {deviceId} not found");
        }

        var healthScore = await CalculateHealthScoreAsync(device, cancellationToken);
        var issues = await IdentifyHealthIssuesAsync(device, cancellationToken);
        var recommendations = GenerateRecommendations(device, issues);
        var nextMaintenanceDate = CalculateNextMaintenanceDate(device, issues);

        var analysis = new DeviceHealthAnalysis
        {
            DeviceId = deviceId,
            HealthScore = healthScore,
            HealthStatus = DetermineHealthStatus(healthScore),
            Timestamp = DateTime.UtcNow,
            Issues = issues,
            Recommendations = recommendations,
            NextMaintenanceDate = nextMaintenanceDate,
        };

        _logger.LogDebug(
            "Device health analysis completed for device {DeviceId} with health score {HealthScore}",
            deviceId,
            healthScore
        );

        return analysis;
    }

    /// <summary>
    /// Process incoming device status message
    /// </summary>
    /// <param name="deviceStatusMessage">Device status message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task completion</returns>
    public async Task ProcessDeviceStatusMessageAsync(
        DeviceHealthStatusMessage deviceStatusMessage,
        CancellationToken cancellationToken
    )
    {
        if (deviceStatusMessage == null)
            throw new ArgumentNullException(nameof(deviceStatusMessage));

        if (string.IsNullOrWhiteSpace(deviceStatusMessage.DeviceId))
            throw new ArgumentException(
                "Device ID cannot be null or empty",
                nameof(deviceStatusMessage)
            );

        _logger.LogDebug(
            "Processing device status message for device {DeviceId}",
            deviceStatusMessage.DeviceId
        );

        var update = new DeviceHealthUpdate
        {
            BatteryLevel = deviceStatusMessage.BatteryLevel,
            Status = deviceStatusMessage.Status,
            LastErrorMessage = deviceStatusMessage.ErrorMessage,
        };

        // Process device-specific data if available
        if (deviceStatusMessage.Data != null)
        {
            ProcessDeviceSpecificData(deviceStatusMessage.Data, update);
        }

        await _deviceRegistryService.UpdateDeviceHealthAsync(
            deviceStatusMessage.DeviceId,
            update,
            cancellationToken
        );

        _logger.LogDebug(
            "Successfully processed device status message for device {DeviceId}",
            deviceStatusMessage.DeviceId
        );
    }

    /// <summary>
    /// Calculate health score based on device metrics
    /// </summary>
    /// <param name="device">Device health entity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health score (0-100)</returns>
    public async Task<decimal> CalculateHealthScoreAsync(
        DeviceHealth device,
        CancellationToken cancellationToken
    )
    {
        if (device == null)
            throw new ArgumentNullException(nameof(device));

        _logger.LogDebug("Calculating health score for device {DeviceId}", device.DeviceId);

        decimal totalScore = 0;
        int factorCount = 0;

        // Battery level factor (25% of total score)
        if (device.BatteryLevel.HasValue)
        {
            var batteryScore = CalculateBatteryScore(device.BatteryLevel.Value);
            totalScore += batteryScore * 0.25m;
            factorCount++;
        }

        // Data freshness factor (25% of total score)
        var dataFreshnessScore = CalculateDataFreshnessScore(device.LastDataReceived);
        totalScore += dataFreshnessScore * 0.25m;
        factorCount++;

        // Device status factor (20% of total score)
        var statusScore = CalculateStatusScore(device.Status);
        totalScore += statusScore * 0.20m;
        factorCount++;

        // Device-specific factors (30% of total score)
        var deviceSpecificScore = await CalculateDeviceSpecificScoreAsync(
            device,
            cancellationToken
        );
        totalScore += deviceSpecificScore * 0.30m;
        factorCount++;

        var finalScore = factorCount > 0 ? totalScore : 0;

        _logger.LogDebug(
            "Calculated health score {HealthScore} for device {DeviceId}",
            finalScore,
            device.DeviceId
        );

        return Math.Round(finalScore, 2);
    }

    /// <summary>
    /// Predict maintenance needs based on usage patterns
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Maintenance prediction result</returns>
    public async Task<MaintenancePrediction> PredictMaintenanceNeedsAsync(
        string deviceId,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));

        _logger.LogDebug("Predicting maintenance needs for device {DeviceId}", deviceId);

        var device = await _deviceRegistryService.GetDeviceAsync(deviceId, cancellationToken);
        if (device == null)
        {
            throw new InvalidOperationException($"Device {deviceId} not found");
        }

        var prediction = new MaintenancePrediction
        {
            DeviceId = deviceId,
            PredictedMaintenanceDate = DateTime.UtcNow.AddDays(30), // Default prediction
            ConfidenceLevel = 50, // Default confidence
            MaintenanceType = MaintenanceType.GeneralMaintenance,
            Reasons = new List<string>(),
        };

        // Predict based on device type and current metrics
        switch (device.DeviceType)
        {
            case DeviceType.CGM:
                prediction = PredictCgmMaintenance(device, prediction);
                break;
            case DeviceType.InsulinPump:
                prediction = PredictInsulinPumpMaintenance(device, prediction);
                break;
            case DeviceType.BGM:
                prediction = PredictBgmMaintenance(device, prediction);
                break;
        }

        _logger.LogDebug(
            "Predicted maintenance for device {DeviceId}: {MaintenanceType} on {Date}",
            deviceId,
            prediction.MaintenanceType,
            prediction.PredictedMaintenanceDate
        );

        return prediction;
    }

    /// <summary>
    /// Generate health report for a device
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="period">Report period in days</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Device health report</returns>
    public async Task<DeviceHealthReport> GenerateHealthReportAsync(
        string deviceId,
        int period,
        CancellationToken cancellationToken
    )
    {
        if (string.IsNullOrWhiteSpace(deviceId))
            throw new ArgumentException("Device ID cannot be null or empty", nameof(deviceId));

        if (period <= 0)
            throw new ArgumentException("Period must be positive", nameof(period));

        _logger.LogDebug(
            "Generating health report for device {DeviceId} for {Period} days",
            deviceId,
            period
        );

        var device = await _deviceRegistryService.GetDeviceAsync(deviceId, cancellationToken);
        if (device == null)
        {
            throw new InvalidOperationException($"Device {deviceId} not found");
        }

        var periodStart = DateTime.UtcNow.AddDays(-period);
        var periodEnd = DateTime.UtcNow;

        var report = new DeviceHealthReport
        {
            DeviceId = deviceId,
            PeriodStart = periodStart,
            PeriodEnd = periodEnd,
            AverageHealthScore = await CalculateHealthScoreAsync(device, cancellationToken),
            UptimePercentage = CalculateUptimePercentage(device, periodStart, periodEnd),
            TotalAlerts = 0, // Would be calculated from alert history
            HealthTrend = HealthTrend.Stable, // Would be calculated from historical data
            KeyMetrics = GenerateKeyMetrics(device),
        };

        _logger.LogDebug("Generated health report for device {DeviceId}", deviceId);

        return report;
    }

    #region Private Helper Methods

    /// <summary>
    /// Identify health issues for a device
    /// </summary>
    private Task<List<DeviceHealthIssue>> IdentifyHealthIssuesAsync(
        DeviceHealth device,
        CancellationToken cancellationToken
    )
    {
        var issues = new List<DeviceHealthIssue>();

        // Check battery level
        if (
            device.BatteryLevel.HasValue
            && device.BatteryLevel.Value <= device.BatteryWarningThreshold
        )
        {
            var severity =
                device.BatteryLevel.Value <= 10
                    ? DeviceIssueSeverity.Critical
                    : DeviceIssueSeverity.High;
            issues.Add(
                new DeviceHealthIssue
                {
                    Type = DeviceIssueType.LowBattery,
                    Severity = severity,
                    Description = $"Battery level is {device.BatteryLevel.Value}%",
                    SuggestedResolution = "Replace or charge the device battery",
                }
            );
        }

        // Check sensor expiration (for CGM devices)
        if (device.DeviceType == DeviceType.CGM && device.SensorExpiration.HasValue)
        {
            var hoursUntilExpiration = (device.SensorExpiration.Value - DateTime.UtcNow).TotalHours;
            if (hoursUntilExpiration <= device.SensorExpirationWarningHours)
            {
                var severity =
                    hoursUntilExpiration <= 0
                        ? DeviceIssueSeverity.Critical
                        : DeviceIssueSeverity.Medium;
                issues.Add(
                    new DeviceHealthIssue
                    {
                        Type = DeviceIssueType.SensorExpiring,
                        Severity = severity,
                        Description =
                            hoursUntilExpiration <= 0
                                ? "Sensor has expired"
                                : $"Sensor expires in {hoursUntilExpiration:F1} hours",
                        SuggestedResolution = "Replace the CGM sensor",
                    }
                );
            }
        }

        // Check data gap
        if (device.LastDataReceived.HasValue)
        {
            var minutesSinceLastData = (
                DateTime.UtcNow - device.LastDataReceived.Value
            ).TotalMinutes;
            if (minutesSinceLastData > device.DataGapWarningMinutes)
            {
                var severity =
                    minutesSinceLastData > 120
                        ? DeviceIssueSeverity.High
                        : DeviceIssueSeverity.Medium;
                issues.Add(
                    new DeviceHealthIssue
                    {
                        Type = DeviceIssueType.DataGap,
                        Severity = severity,
                        Description = $"No data received for {minutesSinceLastData:F0} minutes",
                        SuggestedResolution = "Check device connectivity and power",
                    }
                );
            }
        }

        // Check calibration (for devices that require it)
        if (
            device.LastCalibration.HasValue
            && (device.DeviceType == DeviceType.CGM || device.DeviceType == DeviceType.BGM)
        )
        {
            var hoursSinceCalibration = (DateTime.UtcNow - device.LastCalibration.Value).TotalHours;
            if (hoursSinceCalibration > device.CalibrationReminderHours)
            {
                var severity =
                    hoursSinceCalibration > device.CalibrationReminderHours * 2
                        ? DeviceIssueSeverity.High
                        : DeviceIssueSeverity.Medium;
                issues.Add(
                    new DeviceHealthIssue
                    {
                        Type = DeviceIssueType.CalibrationOverdue,
                        Severity = severity,
                        Description = $"Last calibration was {hoursSinceCalibration:F1} hours ago",
                        SuggestedResolution = "Perform device calibration",
                    }
                );
            }
        }

        // Check device status
        if (device.Status == DeviceStatusType.Error || device.Status == DeviceStatusType.Warning)
        {
            var severity =
                device.Status == DeviceStatusType.Error
                    ? DeviceIssueSeverity.Critical
                    : DeviceIssueSeverity.Medium;
            issues.Add(
                new DeviceHealthIssue
                {
                    Type = DeviceIssueType.DeviceError,
                    Severity = severity,
                    Description = device.LastErrorMessage ?? $"Device status is {device.Status}",
                    SuggestedResolution = "Check device manual or contact support",
                }
            );
        }

        return Task.FromResult(issues);
    }

    /// <summary>
    /// Generate recommendations based on device and issues
    /// </summary>
    private static List<string> GenerateRecommendations(
        DeviceHealth device,
        List<DeviceHealthIssue> issues
    )
    {
        var recommendations = new List<string>();

        foreach (var issue in issues)
        {
            if (!string.IsNullOrWhiteSpace(issue.SuggestedResolution))
            {
                recommendations.Add(issue.SuggestedResolution);
            }
        }

        // Add general recommendations based on device type
        switch (device.DeviceType)
        {
            case DeviceType.CGM:
                if (!issues.Any(i => i.Type == DeviceIssueType.CalibrationOverdue))
                {
                    recommendations.Add("Regular calibration helps maintain accuracy");
                }
                break;
            case DeviceType.InsulinPump:
                recommendations.Add("Regular infusion set changes prevent occlusions");
                break;
            case DeviceType.BGM:
                recommendations.Add("Store test strips in a cool, dry place");
                break;
        }

        return recommendations.Distinct().ToList();
    }

    /// <summary>
    /// Calculate next maintenance date based on device and issues
    /// </summary>
    private static DateTime? CalculateNextMaintenanceDate(
        DeviceHealth device,
        List<DeviceHealthIssue> issues
    )
    {
        var criticalIssues = issues.Where(i => i.Severity == DeviceIssueSeverity.Critical).ToList();
        if (criticalIssues.Any())
        {
            return DateTime.UtcNow; // Immediate maintenance needed
        }

        var highIssues = issues.Where(i => i.Severity == DeviceIssueSeverity.High).ToList();
        if (highIssues.Any())
        {
            return DateTime.UtcNow.AddDays(1); // Maintenance needed soon
        }

        // Default maintenance schedule based on device type
        return device.DeviceType switch
        {
            DeviceType.CGM => DateTime.UtcNow.AddDays(7), // Weekly sensor changes
            DeviceType.InsulinPump => DateTime.UtcNow.AddDays(3), // Infusion set changes
            DeviceType.BGM => DateTime.UtcNow.AddDays(30), // Monthly check
            _ => DateTime.UtcNow.AddDays(14), // Bi-weekly default
        };
    }

    /// <summary>
    /// Calculate battery score (0-100)
    /// </summary>
    private static decimal CalculateBatteryScore(decimal batteryLevel)
    {
        if (batteryLevel >= 80)
            return 100;
        if (batteryLevel >= 50)
            return 80;
        if (batteryLevel >= 20)
            return 60;
        if (batteryLevel >= 10)
            return 40;
        return 20;
    }

    /// <summary>
    /// Calculate data freshness score (0-100)
    /// </summary>
    private decimal CalculateDataFreshnessScore(DateTime? lastDataReceived)
    {
        if (!lastDataReceived.HasValue)
            return 0;

        var minutesSinceLastData = (DateTime.UtcNow - lastDataReceived.Value).TotalMinutes;

        if (minutesSinceLastData <= 5)
            return 100;
        if (minutesSinceLastData <= 15)
            return 80;
        if (minutesSinceLastData <= 30)
            return 60;
        if (minutesSinceLastData <= 60)
            return 40;
        if (minutesSinceLastData <= 120)
            return 20;
        return 0;
    }

    /// <summary>
    /// Calculate status score (0-100)
    /// </summary>
    private static decimal CalculateStatusScore(DeviceStatusType status)
    {
        return status switch
        {
            DeviceStatusType.Active => 100,
            DeviceStatusType.Warning => 60,
            DeviceStatusType.Maintenance => 40,
            DeviceStatusType.Inactive => 20,
            DeviceStatusType.Error => 0,
            _ => 50,
        };
    }

    /// <summary>
    /// Calculate device-specific score (0-100)
    /// </summary>
    private Task<decimal> CalculateDeviceSpecificScoreAsync(
        DeviceHealth device,
        CancellationToken cancellationToken
    )
    {
        // This would be implemented based on device-specific metrics
        // For now, return a default score based on device type
        var score = device.DeviceType switch
        {
            DeviceType.CGM => CalculateCgmSpecificScore(device),
            DeviceType.InsulinPump => CalculateInsulinPumpSpecificScore(device),
            DeviceType.BGM => CalculateBgmSpecificScore(device),
            _ => 50,
        };

        return Task.FromResult(score);
    }

    /// <summary>
    /// Calculate CGM-specific score
    /// </summary>
    private static decimal CalculateCgmSpecificScore(DeviceHealth device)
    {
        decimal score = 100;

        // Check sensor expiration
        if (device.SensorExpiration.HasValue)
        {
            var hoursUntilExpiration = (device.SensorExpiration.Value - DateTime.UtcNow).TotalHours;
            if (hoursUntilExpiration <= 0)
                score -= 50;
            else if (hoursUntilExpiration <= 24)
                score -= 30;
            else if (hoursUntilExpiration <= 48)
                score -= 15;
        }

        // Check calibration
        if (device.LastCalibration.HasValue)
        {
            var hoursSinceCalibration = (DateTime.UtcNow - device.LastCalibration.Value).TotalHours;
            if (hoursSinceCalibration > 24)
                score -= 20;
            else if (hoursSinceCalibration > 12)
                score -= 10;
        }

        return Math.Max(0, score);
    }

    /// <summary>
    /// Calculate insulin pump-specific score
    /// </summary>
    private static decimal CalculateInsulinPumpSpecificScore(DeviceHealth device)
    {
        decimal score = 100;

        // This would check insulin reservoir levels, infusion set age, etc.
        // For now, return a default score
        return score;
    }

    /// <summary>
    /// Calculate BGM-specific score
    /// </summary>
    private static decimal CalculateBgmSpecificScore(DeviceHealth device)
    {
        decimal score = 100;

        // This would check test strip inventory, control solution testing, etc.
        // For now, return a default score
        return score;
    }

    /// <summary>
    /// Determine health status based on score
    /// </summary>
    private static DeviceHealthStatus DetermineHealthStatus(decimal healthScore)
    {
        return healthScore switch
        {
            >= 90 => DeviceHealthStatus.Excellent,
            >= 75 => DeviceHealthStatus.Good,
            >= 50 => DeviceHealthStatus.Fair,
            >= 25 => DeviceHealthStatus.Poor,
            _ => DeviceHealthStatus.Critical,
        };
    }

    /// <summary>
    /// Process device-specific data from status message
    /// </summary>
    private static void ProcessDeviceSpecificData(
        Dictionary<string, object> data,
        DeviceHealthUpdate update
    )
    {
        // Process common device-specific data
        if (
            data.TryGetValue("sensorExpiration", out var sensorExp)
            && DateTime.TryParse(sensorExp.ToString(), out var expiration)
        )
        {
            update.SensorExpiration = expiration;
        }

        if (
            data.TryGetValue("lastCalibration", out var lastCal)
            && DateTime.TryParse(lastCal.ToString(), out var calibration)
        )
        {
            update.LastCalibration = calibration;
        }
    }

    /// <summary>
    /// Predict CGM maintenance needs
    /// </summary>
    private static MaintenancePrediction PredictCgmMaintenance(
        DeviceHealth device,
        MaintenancePrediction prediction
    )
    {
        if (device.SensorExpiration.HasValue)
        {
            var daysUntilExpiration = (device.SensorExpiration.Value - DateTime.UtcNow).TotalDays;
            if (daysUntilExpiration <= 1)
            {
                prediction.PredictedMaintenanceDate = device.SensorExpiration.Value;
                prediction.MaintenanceType = MaintenanceType.SensorReplacement;
                prediction.ConfidenceLevel = 95;
                prediction.Reasons.Add("Sensor expiration approaching");
            }
        }

        return prediction;
    }

    /// <summary>
    /// Predict insulin pump maintenance needs
    /// </summary>
    private static MaintenancePrediction PredictInsulinPumpMaintenance(
        DeviceHealth device,
        MaintenancePrediction prediction
    )
    {
        // This would analyze insulin reservoir levels, infusion set age, etc.
        return prediction;
    }

    /// <summary>
    /// Predict BGM maintenance needs
    /// </summary>
    private static MaintenancePrediction PredictBgmMaintenance(
        DeviceHealth device,
        MaintenancePrediction prediction
    )
    {
        // This would analyze test strip inventory, control solution testing, etc.
        return prediction;
    }

    /// <summary>
    /// Calculate uptime percentage for the period
    /// </summary>
    private static decimal CalculateUptimePercentage(
        DeviceHealth device,
        DateTime periodStart,
        DateTime periodEnd
    )
    {
        // This would be calculated from historical data
        // For now, return a default based on device status
        return device.Status switch
        {
            DeviceStatusType.Active => 98.5m,
            DeviceStatusType.Warning => 85.0m,
            DeviceStatusType.Maintenance => 75.0m,
            DeviceStatusType.Inactive => 25.0m,
            DeviceStatusType.Error => 10.0m,
            _ => 50.0m,
        };
    }

    /// <summary>
    /// Generate key metrics for the device
    /// </summary>
    private static Dictionary<string, object> GenerateKeyMetrics(DeviceHealth device)
    {
        var metrics = new Dictionary<string, object>
        {
            ["deviceType"] = device.DeviceType.ToString(),
            ["status"] = device.Status.ToString(),
            ["batteryLevel"] = device.BatteryLevel ?? 0,
            ["daysSinceRegistration"] = (DateTime.UtcNow - device.CreatedAt).TotalDays,
        };

        if (device.LastDataReceived.HasValue)
        {
            metrics["minutesSinceLastData"] = (
                DateTime.UtcNow - device.LastDataReceived.Value
            ).TotalMinutes;
        }

        if (device.SensorExpiration.HasValue)
        {
            metrics["hoursUntilSensorExpiration"] = (
                device.SensorExpiration.Value - DateTime.UtcNow
            ).TotalHours;
        }

        return metrics;
    }

    #endregion
}
