using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts;

/// <summary>
/// Service interface for device registry and management operations
/// </summary>
public interface IDeviceRegistryService
{
    /// <summary>
    /// Register a new device for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="request">Device registration request</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Registered device health entity</returns>
    Task<DeviceHealth> RegisterDeviceAsync(
        string userId,
        DeviceRegistrationRequest request,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Get all devices for a user
    /// </summary>
    /// <param name="userId">User identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of user's devices</returns>
    Task<List<DeviceHealth>> GetUserDevicesAsync(
        string userId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Update device health metrics
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="update">Device health update data</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task completion</returns>
    Task UpdateDeviceHealthAsync(
        string deviceId,
        DeviceHealthUpdate update,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Get device health information
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Device health entity or null if not found</returns>
    Task<DeviceHealth?> GetDeviceAsync(string deviceId, CancellationToken cancellationToken);

    /// <summary>
    /// Remove a device from the registry
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task completion</returns>
    Task RemoveDeviceAsync(string deviceId, CancellationToken cancellationToken);

    /// <summary>
    /// Update device settings
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="settings">Device settings update</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task completion</returns>
    Task UpdateDeviceSettingsAsync(
        string deviceId,
        DeviceSettingsUpdate settings,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Service interface for device health analysis and monitoring
/// </summary>
public interface IDeviceHealthAnalysisService
{
    /// <summary>
    /// Analyze device health and generate health score
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Device health analysis result</returns>
    Task<DeviceHealthAnalysis> AnalyzeDeviceHealthAsync(
        string deviceId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Process incoming device status message
    /// </summary>
    /// <param name="deviceStatusMessage">Device status message</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task completion</returns>
    Task ProcessDeviceStatusMessageAsync(
        DeviceHealthStatusMessage deviceStatusMessage,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Calculate health score based on device metrics
    /// </summary>
    /// <param name="device">Device health entity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Health score (0-100)</returns>
    Task<decimal> CalculateHealthScoreAsync(
        DeviceHealth device,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Predict maintenance needs based on usage patterns
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Maintenance prediction result</returns>
    Task<MaintenancePrediction> PredictMaintenanceNeedsAsync(
        string deviceId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Generate health report for a device
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="period">Report period in days</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Device health report</returns>
    Task<DeviceHealthReport> GenerateHealthReportAsync(
        string deviceId,
        int period,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Service interface for device alert engine and smart alerting
/// </summary>
public interface IDeviceAlertEngine
{
    /// <summary>
    /// Process device health and generate appropriate alerts
    /// </summary>
    /// <param name="device">Device health entity</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of generated device alerts</returns>
    Task<List<DeviceAlert>> ProcessDeviceAlertsAsync(
        DeviceHealth device,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Check if an alert should be sent based on cooldown and escalation rules
    /// </summary>
    /// <param name="deviceId">Device identifier</param>
    /// <param name="alertType">Type of alert</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>True if alert should be sent</returns>
    Task<bool> ShouldSendAlertAsync(
        string deviceId,
        DeviceAlertType alertType,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Send device alert through appropriate notification channels
    /// </summary>
    /// <param name="deviceAlert">Device alert to send</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task completion</returns>
    Task SendDeviceAlertAsync(DeviceAlert deviceAlert, CancellationToken cancellationToken);

    /// <summary>
    /// Acknowledge a device alert
    /// </summary>
    /// <param name="alertId">Alert identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Task completion</returns>
    Task AcknowledgeDeviceAlertAsync(Guid alertId, CancellationToken cancellationToken);
}

/// <summary>
/// Service interface for CGM-specific health monitoring
/// </summary>
public interface ICgmHealthService
{
    /// <summary>
    /// Monitor CGM sensor session and expiration
    /// </summary>
    /// <param name="deviceId">CGM device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>CGM health status</returns>
    Task<CgmHealthStatus> MonitorSensorSessionAsync(
        string deviceId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Assess CGM accuracy against BGM readings
    /// </summary>
    /// <param name="deviceId">CGM device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>CGM accuracy assessment</returns>
    Task<CgmAccuracyAssessment> AssessAccuracyAsync(
        string deviceId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Monitor CGM signal quality
    /// </summary>
    /// <param name="deviceId">CGM device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>CGM signal quality metrics</returns>
    Task<CgmSignalQuality> MonitorSignalQualityAsync(
        string deviceId,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Service interface for insulin pump-specific health monitoring
/// </summary>
public interface IInsulinPumpHealthService
{
    /// <summary>
    /// Monitor insulin reservoir level
    /// </summary>
    /// <param name="deviceId">Pump device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Insulin reservoir status</returns>
    Task<InsulinReservoirStatus> MonitorReservoirLevelAsync(
        string deviceId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Monitor infusion set age and condition
    /// </summary>
    /// <param name="deviceId">Pump device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Infusion set status</returns>
    Task<InfusionSetStatus> MonitorInfusionSetAsync(
        string deviceId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Detect pump occlusions and delivery issues
    /// </summary>
    /// <param name="deviceId">Pump device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Occlusion detection result</returns>
    Task<OcclusionDetectionResult> DetectOcclusionAsync(
        string deviceId,
        CancellationToken cancellationToken
    );
}

/// <summary>
/// Service interface for blood glucose meter-specific health monitoring
/// </summary>
public interface IBgmHealthService
{
    /// <summary>
    /// Track test strip inventory
    /// </summary>
    /// <param name="deviceId">BGM device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Test strip inventory status</returns>
    Task<TestStripInventoryStatus> TrackTestStripInventoryAsync(
        string deviceId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Monitor control solution testing requirements
    /// </summary>
    /// <param name="deviceId">BGM device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Control solution testing status</returns>
    Task<ControlSolutionStatus> MonitorControlSolutionTestingAsync(
        string deviceId,
        CancellationToken cancellationToken
    );

    /// <summary>
    /// Validate BGM accuracy and calibration
    /// </summary>
    /// <param name="deviceId">BGM device identifier</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>BGM accuracy validation result</returns>
    Task<BgmAccuracyValidation> ValidateAccuracyAsync(
        string deviceId,
        CancellationToken cancellationToken
    );
}
