using System.Text.Json.Serialization;

namespace Nocturne.Core.Models;

/// <summary>
/// Device type enumeration
/// </summary>
public enum DeviceType
{
    /// <summary>
    /// Continuous Glucose Monitor
    /// </summary>
    CGM,

    /// <summary>
    /// Insulin Pump
    /// </summary>
    InsulinPump,

    /// <summary>
    /// Blood Glucose Meter
    /// </summary>
    BGM,

    /// <summary>
    /// Unknown device type
    /// </summary>
    Unknown,
}

/// <summary>
/// Device status enumeration
/// </summary>
public enum DeviceStatusType
{
    /// <summary>
    /// Device is active and functioning normally
    /// </summary>
    Active,

    /// <summary>
    /// Device is inactive or not communicating
    /// </summary>
    Inactive,

    /// <summary>
    /// Device has warnings that need attention
    /// </summary>
    Warning,

    /// <summary>
    /// Device has errors that need immediate attention
    /// </summary>
    Error,

    /// <summary>
    /// Device is in maintenance mode
    /// </summary>
    Maintenance,
}

/// <summary>
/// Configuration options for device health monitoring and maintenance alerts
/// </summary>
public class DeviceHealthOptions
{
    /// <summary>
    /// Section name for configuration binding
    /// </summary>
    public const string SectionName = "DeviceHealth";

    /// <summary>
    /// Health check interval in minutes (default: 15 minutes)
    /// </summary>
    public int HealthCheckIntervalMinutes { get; set; } = 15;

    /// <summary>
    /// Data gap warning threshold in minutes (default: 30 minutes)
    /// </summary>
    public int DataGapWarningMinutes { get; set; } = 30;

    /// <summary>
    /// Battery warning threshold percentage (default: 20%)
    /// </summary>
    public int BatteryWarningThreshold { get; set; } = 20;

    /// <summary>
    /// Sensor expiration warning time in hours (default: 24 hours)
    /// </summary>
    public int SensorExpirationWarningHours { get; set; } = 24;

    /// <summary>
    /// Calibration reminder interval in hours (default: 12 hours)
    /// </summary>
    public int CalibrationReminderHours { get; set; } = 12;

    /// <summary>
    /// Maintenance alert cooldown period in hours (default: 4 hours)
    /// </summary>
    public int MaintenanceAlertCooldownHours { get; set; } = 4;

    /// <summary>
    /// Enable predictive alerts based on usage patterns (default: true)
    /// </summary>
    public bool EnablePredictiveAlerts { get; set; } = true;

    /// <summary>
    /// Enable device performance analytics (default: true)
    /// </summary>
    public bool EnablePerformanceAnalytics { get; set; } = true;

    /// <summary>
    /// Maximum number of devices per user (default: 10)
    /// </summary>
    public int MaxDevicesPerUser { get; set; } = 10;

    /// <summary>
    /// Device registration timeout in seconds (default: 30 seconds)
    /// </summary>
    public int DeviceRegistrationTimeoutSeconds { get; set; } = 30;

    /// <summary>
    /// Enable debug logging for device health monitoring (default: false)
    /// </summary>
    public bool EnableDebugLogging { get; set; } = false;
}

/// <summary>
/// Device registration request model
/// </summary>
public class DeviceRegistrationRequest
{
    /// <summary>
    /// Unique device identifier
    /// </summary>
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Type of device (CGM, InsulinPump, BGM, Unknown)
    /// </summary>
    [JsonPropertyName("deviceType")]
    public DeviceType DeviceType { get; set; }

    /// <summary>
    /// Human-readable device name
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Device manufacturer
    /// </summary>
    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Device model
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Device serial number
    /// </summary>
    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Initial battery level percentage (0-100)
    /// </summary>
    [JsonPropertyName("batteryLevel")]
    public decimal? BatteryLevel { get; set; }

    /// <summary>
    /// Sensor expiration date for CGM devices
    /// </summary>
    [JsonPropertyName("sensorExpiration")]
    public DateTime? SensorExpiration { get; set; }
}

/// <summary>
/// Device health update model
/// </summary>
public class DeviceHealthUpdate
{
    /// <summary>
    /// Current battery level percentage (0-100)
    /// </summary>
    [JsonPropertyName("batteryLevel")]
    public decimal? BatteryLevel { get; set; }

    /// <summary>
    /// When the sensor expires (for CGM devices)
    /// </summary>
    [JsonPropertyName("sensorExpiration")]
    public DateTime? SensorExpiration { get; set; }

    /// <summary>
    /// When the device was last calibrated
    /// </summary>
    [JsonPropertyName("lastCalibration")]
    public DateTime? LastCalibration { get; set; }

    /// <summary>
    /// Device status update
    /// </summary>
    [JsonPropertyName("status")]
    public DeviceStatusType? Status { get; set; }

    /// <summary>
    /// Last error message from the device
    /// </summary>
    [JsonPropertyName("lastErrorMessage")]
    public string? LastErrorMessage { get; set; }

    /// <summary>
    /// Additional device-specific data (JSON)
    /// </summary>
    [JsonPropertyName("deviceSpecificData")]
    public Dictionary<string, object>? DeviceSpecificData { get; set; }
}

/// <summary>
/// Device settings update model
/// </summary>
public class DeviceSettingsUpdate
{
    /// <summary>
    /// Battery warning threshold percentage
    /// </summary>
    [JsonPropertyName("batteryWarningThreshold")]
    public decimal? BatteryWarningThreshold { get; set; }

    /// <summary>
    /// Sensor expiration warning time in hours
    /// </summary>
    [JsonPropertyName("sensorExpirationWarningHours")]
    public int? SensorExpirationWarningHours { get; set; }

    /// <summary>
    /// Data gap warning threshold in minutes
    /// </summary>
    [JsonPropertyName("dataGapWarningMinutes")]
    public int? DataGapWarningMinutes { get; set; }

    /// <summary>
    /// Calibration reminder interval in hours
    /// </summary>
    [JsonPropertyName("calibrationReminderHours")]
    public int? CalibrationReminderHours { get; set; }
}

/// <summary>
/// Device health analysis result
/// </summary>
public class DeviceHealthAnalysis
{
    /// <summary>
    /// Device identifier
    /// </summary>
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Overall health score (0-100)
    /// </summary>
    [JsonPropertyName("healthScore")]
    public decimal HealthScore { get; set; }

    /// <summary>
    /// Health status category
    /// </summary>
    [JsonPropertyName("healthStatus")]
    public DeviceHealthStatus HealthStatus { get; set; }

    /// <summary>
    /// Analysis timestamp
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// List of identified issues
    /// </summary>
    [JsonPropertyName("issues")]
    public List<DeviceHealthIssue> Issues { get; set; } = new();

    /// <summary>
    /// Recommended actions
    /// </summary>
    [JsonPropertyName("recommendations")]
    public List<string> Recommendations { get; set; } = new();

    /// <summary>
    /// Next maintenance date recommendation
    /// </summary>
    [JsonPropertyName("nextMaintenanceDate")]
    public DateTime? NextMaintenanceDate { get; set; }
}

/// <summary>
/// Device health status enumeration
/// </summary>
public enum DeviceHealthStatus
{
    /// <summary>
    /// Device is in excellent health
    /// </summary>
    Excellent,

    /// <summary>
    /// Device is in good health
    /// </summary>
    Good,

    /// <summary>
    /// Device health is fair, some attention needed
    /// </summary>
    Fair,

    /// <summary>
    /// Device health is poor, maintenance required
    /// </summary>
    Poor,

    /// <summary>
    /// Device health is critical, immediate attention required
    /// </summary>
    Critical,
}

/// <summary>
/// Device health issue model
/// </summary>
public class DeviceHealthIssue
{
    /// <summary>
    /// Issue type
    /// </summary>
    [JsonPropertyName("type")]
    public DeviceIssueType Type { get; set; }

    /// <summary>
    /// Issue severity
    /// </summary>
    [JsonPropertyName("severity")]
    public DeviceIssueSeverity Severity { get; set; }

    /// <summary>
    /// Issue description
    /// </summary>
    [JsonPropertyName("description")]
    public string Description { get; set; } = string.Empty;

    /// <summary>
    /// When the issue was detected
    /// </summary>
    [JsonPropertyName("detectedAt")]
    public DateTime DetectedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Suggested resolution
    /// </summary>
    [JsonPropertyName("suggestedResolution")]
    public string? SuggestedResolution { get; set; }
}

/// <summary>
/// Device issue type enumeration
/// </summary>
public enum DeviceIssueType
{
    /// <summary>
    /// Low battery level
    /// </summary>
    LowBattery,

    /// <summary>
    /// Sensor expiring soon
    /// </summary>
    SensorExpiring,

    /// <summary>
    /// Calibration overdue
    /// </summary>
    CalibrationOverdue,

    /// <summary>
    /// Data gap detected
    /// </summary>
    DataGap,

    /// <summary>
    /// Communication error
    /// </summary>
    CommunicationError,

    /// <summary>
    /// Device error reported
    /// </summary>
    DeviceError,

    /// <summary>
    /// Performance degradation
    /// </summary>
    PerformanceDegradation,

    /// <summary>
    /// Maintenance required
    /// </summary>
    MaintenanceRequired,
}

/// <summary>
/// Device issue severity enumeration
/// </summary>
public enum DeviceIssueSeverity
{
    /// <summary>
    /// Low severity, informational
    /// </summary>
    Low,

    /// <summary>
    /// Medium severity, attention recommended
    /// </summary>
    Medium,

    /// <summary>
    /// High severity, action required
    /// </summary>
    High,

    /// <summary>
    /// Critical severity, immediate action required
    /// </summary>
    Critical,
}

/// <summary>
/// Device health status message for processing
/// </summary>
public class DeviceHealthStatusMessage
{
    /// <summary>
    /// Device identifier
    /// </summary>
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Message timestamp
    /// </summary>
    [JsonPropertyName("timestamp")]
    public DateTime Timestamp { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Battery level percentage
    /// </summary>
    [JsonPropertyName("batteryLevel")]
    public decimal? BatteryLevel { get; set; }

    /// <summary>
    /// Device status
    /// </summary>
    [JsonPropertyName("status")]
    public DeviceStatusType Status { get; set; }

    /// <summary>
    /// Error message if any
    /// </summary>
    [JsonPropertyName("errorMessage")]
    public string? ErrorMessage { get; set; }

    /// <summary>
    /// Additional device-specific data
    /// </summary>
    [JsonPropertyName("data")]
    public Dictionary<string, object>? Data { get; set; }
}

/// <summary>
/// Maintenance prediction result
/// </summary>
public class MaintenancePrediction
{
    /// <summary>
    /// Device identifier
    /// </summary>
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Predicted maintenance date
    /// </summary>
    [JsonPropertyName("predictedMaintenanceDate")]
    public DateTime PredictedMaintenanceDate { get; set; }

    /// <summary>
    /// Confidence level (0-100)
    /// </summary>
    [JsonPropertyName("confidenceLevel")]
    public decimal ConfidenceLevel { get; set; }

    /// <summary>
    /// Maintenance type prediction
    /// </summary>
    [JsonPropertyName("maintenanceType")]
    public MaintenanceType MaintenanceType { get; set; }

    /// <summary>
    /// Reasons for the prediction
    /// </summary>
    [JsonPropertyName("reasons")]
    public List<string> Reasons { get; set; } = new();
}

/// <summary>
/// Maintenance type enumeration
/// </summary>
public enum MaintenanceType
{
    /// <summary>
    /// Battery replacement
    /// </summary>
    BatteryReplacement,

    /// <summary>
    /// Sensor replacement
    /// </summary>
    SensorReplacement,

    /// <summary>
    /// Calibration required
    /// </summary>
    Calibration,

    /// <summary>
    /// General maintenance
    /// </summary>
    GeneralMaintenance,

    /// <summary>
    /// Device replacement
    /// </summary>
    DeviceReplacement,
}

/// <summary>
/// Device health report model
/// </summary>
public class DeviceHealthReport
{
    /// <summary>
    /// Device identifier
    /// </summary>
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Report period start date
    /// </summary>
    [JsonPropertyName("periodStart")]
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// Report period end date
    /// </summary>
    [JsonPropertyName("periodEnd")]
    public DateTime PeriodEnd { get; set; }

    /// <summary>
    /// Average health score during period
    /// </summary>
    [JsonPropertyName("averageHealthScore")]
    public decimal AverageHealthScore { get; set; }

    /// <summary>
    /// Device uptime percentage
    /// </summary>
    [JsonPropertyName("uptimePercentage")]
    public decimal UptimePercentage { get; set; }

    /// <summary>
    /// Total alerts generated
    /// </summary>
    [JsonPropertyName("totalAlerts")]
    public int TotalAlerts { get; set; }

    /// <summary>
    /// Health trend over period
    /// </summary>
    [JsonPropertyName("healthTrend")]
    public HealthTrend HealthTrend { get; set; }

    /// <summary>
    /// Key metrics for the period
    /// </summary>
    [JsonPropertyName("keyMetrics")]
    public Dictionary<string, object> KeyMetrics { get; set; } = new();
}

/// <summary>
/// Health trend enumeration
/// </summary>
public enum HealthTrend
{
    /// <summary>
    /// Health is improving
    /// </summary>
    Improving,

    /// <summary>
    /// Health is stable
    /// </summary>
    Stable,

    /// <summary>
    /// Health is declining
    /// </summary>
    Declining,

    /// <summary>
    /// Insufficient data for trend analysis
    /// </summary>
    Unknown,
}

/// <summary>
/// Device alert model
/// </summary>
public class DeviceAlert
{
    /// <summary>
    /// Alert identifier
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; } = Guid.CreateVersion7();

    /// <summary>
    /// Device identifier
    /// </summary>
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// User identifier
    /// </summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Alert type
    /// </summary>
    [JsonPropertyName("alertType")]
    public DeviceAlertType AlertType { get; set; }

    /// <summary>
    /// Alert severity
    /// </summary>
    [JsonPropertyName("severity")]
    public DeviceIssueSeverity Severity { get; set; }

    /// <summary>
    /// Alert title
    /// </summary>
    [JsonPropertyName("title")]
    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Alert message
    /// </summary>
    [JsonPropertyName("message")]
    public string Message { get; set; } = string.Empty;

    /// <summary>
    /// When the alert was triggered
    /// </summary>
    [JsonPropertyName("triggerTime")]
    public DateTime TriggerTime { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// Whether the alert has been acknowledged
    /// </summary>
    [JsonPropertyName("acknowledged")]
    public bool Acknowledged { get; set; }

    /// <summary>
    /// When the alert was acknowledged
    /// </summary>
    [JsonPropertyName("acknowledgedAt")]
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// Additional alert data
    /// </summary>
    [JsonPropertyName("data")]
    public Dictionary<string, object>? Data { get; set; }
}

/// <summary>
/// Device alert type enumeration
/// </summary>
public enum DeviceAlertType
{
    /// <summary>
    /// Battery warning alert
    /// </summary>
    BatteryWarning,

    /// <summary>
    /// Critical battery alert
    /// </summary>
    BatteryCritical,

    /// <summary>
    /// Sensor expiration warning
    /// </summary>
    SensorExpirationWarning,

    /// <summary>
    /// Sensor expired alert
    /// </summary>
    SensorExpired,

    /// <summary>
    /// Calibration reminder alert
    /// </summary>
    CalibrationReminder,

    /// <summary>
    /// Calibration overdue alert
    /// </summary>
    CalibrationOverdue,

    /// <summary>
    /// Data gap detected alert
    /// </summary>
    DataGapDetected,

    /// <summary>
    /// Communication failure alert
    /// </summary>
    CommunicationFailure,

    /// <summary>
    /// Device error alert
    /// </summary>
    DeviceError,

    /// <summary>
    /// Maintenance required alert
    /// </summary>
    MaintenanceRequired,
}

// Device-specific health status models

/// <summary>
/// CGM health status model
/// </summary>
public class CgmHealthStatus
{
    /// <summary>
    /// Current sensor session age in hours
    /// </summary>
    [JsonPropertyName("sensorAgeHours")]
    public int SensorAgeHours { get; set; }

    /// <summary>
    /// Hours until sensor expiration
    /// </summary>
    [JsonPropertyName("hoursUntilExpiration")]
    public int? HoursUntilExpiration { get; set; }

    /// <summary>
    /// Sensor accuracy percentage
    /// </summary>
    [JsonPropertyName("accuracyPercentage")]
    public decimal AccuracyPercentage { get; set; }

    /// <summary>
    /// Signal quality score (0-100)
    /// </summary>
    [JsonPropertyName("signalQuality")]
    public decimal SignalQuality { get; set; }

    /// <summary>
    /// Number of calibrations in current session
    /// </summary>
    [JsonPropertyName("calibrationCount")]
    public int CalibrationCount { get; set; }
}

/// <summary>
/// CGM accuracy assessment model
/// </summary>
public class CgmAccuracyAssessment
{
    /// <summary>
    /// Mean absolute relative difference (MARD) percentage
    /// </summary>
    [JsonPropertyName("mardPercentage")]
    public decimal MardPercentage { get; set; }

    /// <summary>
    /// Number of comparison readings
    /// </summary>
    [JsonPropertyName("comparisonCount")]
    public int ComparisonCount { get; set; }

    /// <summary>
    /// Assessment period start
    /// </summary>
    [JsonPropertyName("periodStart")]
    public DateTime PeriodStart { get; set; }

    /// <summary>
    /// Assessment period end
    /// </summary>
    [JsonPropertyName("periodEnd")]
    public DateTime PeriodEnd { get; set; }
}

/// <summary>
/// CGM signal quality model
/// </summary>
public class CgmSignalQuality
{
    /// <summary>
    /// Signal strength percentage
    /// </summary>
    [JsonPropertyName("signalStrength")]
    public decimal SignalStrength { get; set; }

    /// <summary>
    /// Noise level
    /// </summary>
    [JsonPropertyName("noiseLevel")]
    public decimal NoiseLevel { get; set; }

    /// <summary>
    /// Data reliability score
    /// </summary>
    [JsonPropertyName("reliabilityScore")]
    public decimal ReliabilityScore { get; set; }
}

/// <summary>
/// Insulin reservoir status model
/// </summary>
public class InsulinReservoirStatus
{
    /// <summary>
    /// Current insulin level in units
    /// </summary>
    [JsonPropertyName("currentLevel")]
    public decimal CurrentLevel { get; set; }

    /// <summary>
    /// Maximum reservoir capacity in units
    /// </summary>
    [JsonPropertyName("maxCapacity")]
    public decimal MaxCapacity { get; set; }

    /// <summary>
    /// Estimated days remaining based on usage
    /// </summary>
    [JsonPropertyName("daysRemaining")]
    public decimal DaysRemaining { get; set; }

    /// <summary>
    /// Warning threshold in units
    /// </summary>
    [JsonPropertyName("warningThreshold")]
    public decimal WarningThreshold { get; set; }
}

/// <summary>
/// Infusion set status model
/// </summary>
public class InfusionSetStatus
{
    /// <summary>
    /// Age of current infusion set in hours
    /// </summary>
    [JsonPropertyName("ageHours")]
    public int AgeHours { get; set; }

    /// <summary>
    /// Recommended replacement age in hours
    /// </summary>
    [JsonPropertyName("recommendedReplacementAge")]
    public int RecommendedReplacementAge { get; set; }

    /// <summary>
    /// Insertion site condition assessment
    /// </summary>
    [JsonPropertyName("siteCondition")]
    public InfusionSiteCondition SiteCondition { get; set; }
}

/// <summary>
/// Infusion site condition enumeration
/// </summary>
public enum InfusionSiteCondition
{
    /// <summary>
    /// Good condition
    /// </summary>
    Good,

    /// <summary>
    /// Acceptable condition
    /// </summary>
    Acceptable,

    /// <summary>
    /// Needs attention
    /// </summary>
    NeedsAttention,

    /// <summary>
    /// Replacement required
    /// </summary>
    ReplacementRequired,
}

/// <summary>
/// Occlusion detection result model
/// </summary>
public class OcclusionDetectionResult
{
    /// <summary>
    /// Whether occlusion is detected
    /// </summary>
    [JsonPropertyName("occlusionDetected")]
    public bool OcclusionDetected { get; set; }

    /// <summary>
    /// Confidence level of detection (0-100)
    /// </summary>
    [JsonPropertyName("confidenceLevel")]
    public decimal ConfidenceLevel { get; set; }

    /// <summary>
    /// Delivery pressure readings
    /// </summary>
    [JsonPropertyName("pressureReadings")]
    public List<decimal> PressureReadings { get; set; } = new();

    /// <summary>
    /// Suggested actions if occlusion detected
    /// </summary>
    [JsonPropertyName("suggestedActions")]
    public List<string> SuggestedActions { get; set; } = new();
}

/// <summary>
/// Test strip inventory status model
/// </summary>
public class TestStripInventoryStatus
{
    /// <summary>
    /// Current test strip count
    /// </summary>
    [JsonPropertyName("currentCount")]
    public int CurrentCount { get; set; }

    /// <summary>
    /// Warning threshold count
    /// </summary>
    [JsonPropertyName("warningThreshold")]
    public int WarningThreshold { get; set; }

    /// <summary>
    /// Estimated days remaining based on usage
    /// </summary>
    [JsonPropertyName("daysRemaining")]
    public decimal DaysRemaining { get; set; }

    /// <summary>
    /// Test strip expiration date
    /// </summary>
    [JsonPropertyName("expirationDate")]
    public DateTime? ExpirationDate { get; set; }
}

/// <summary>
/// Control solution status model
/// </summary>
public class ControlSolutionStatus
{
    /// <summary>
    /// Last control solution test date
    /// </summary>
    [JsonPropertyName("lastTestDate")]
    public DateTime? LastTestDate { get; set; }

    /// <summary>
    /// Recommended test frequency in days
    /// </summary>
    [JsonPropertyName("recommendedFrequencyDays")]
    public int RecommendedFrequencyDays { get; set; }

    /// <summary>
    /// Days since last test
    /// </summary>
    [JsonPropertyName("daysSinceLastTest")]
    public int? DaysSinceLastTest { get; set; }

    /// <summary>
    /// Whether test is overdue
    /// </summary>
    [JsonPropertyName("isOverdue")]
    public bool IsOverdue { get; set; }
}

/// <summary>
/// BGM accuracy validation result model
/// </summary>
public class BgmAccuracyValidation
{
    /// <summary>
    /// Accuracy assessment result
    /// </summary>
    [JsonPropertyName("accuracyResult")]
    public AccuracyResult AccuracyResult { get; set; }

    /// <summary>
    /// Control solution test results
    /// </summary>
    [JsonPropertyName("controlSolutionResults")]
    public List<ControlSolutionResult> ControlSolutionResults { get; set; } = new();

    /// <summary>
    /// Calibration status
    /// </summary>
    [JsonPropertyName("calibrationStatus")]
    public CalibrationStatus CalibrationStatus { get; set; }

    /// <summary>
    /// Recommended actions
    /// </summary>
    [JsonPropertyName("recommendedActions")]
    public List<string> RecommendedActions { get; set; } = new();
}

/// <summary>
/// Accuracy result enumeration
/// </summary>
public enum AccuracyResult
{
    /// <summary>
    /// Accuracy is within acceptable range
    /// </summary>
    Acceptable,

    /// <summary>
    /// Accuracy is questionable, monitoring needed
    /// </summary>
    Questionable,

    /// <summary>
    /// Accuracy is unacceptable, calibration/replacement needed
    /// </summary>
    Unacceptable,
}

/// <summary>
/// Control solution result model
/// </summary>
public class ControlSolutionResult
{
    /// <summary>
    /// Test date
    /// </summary>
    [JsonPropertyName("testDate")]
    public DateTime TestDate { get; set; }

    /// <summary>
    /// Control solution level (Low, Normal, High)
    /// </summary>
    [JsonPropertyName("level")]
    public string Level { get; set; } = string.Empty;

    /// <summary>
    /// Test result value
    /// </summary>
    [JsonPropertyName("result")]
    public decimal Result { get; set; }

    /// <summary>
    /// Expected range minimum
    /// </summary>
    [JsonPropertyName("rangeMin")]
    public decimal RangeMin { get; set; }

    /// <summary>
    /// Expected range maximum
    /// </summary>
    [JsonPropertyName("rangeMax")]
    public decimal RangeMax { get; set; }

    /// <summary>
    /// Whether result is within expected range
    /// </summary>
    [JsonPropertyName("inRange")]
    public bool InRange { get; set; }
}

/// <summary>
/// Calibration status enumeration
/// </summary>
public enum CalibrationStatus
{
    /// <summary>
    /// Device is properly calibrated
    /// </summary>
    Calibrated,

    /// <summary>
    /// Calibration is due soon
    /// </summary>
    DueSoon,

    /// <summary>
    /// Calibration is overdue
    /// </summary>
    Overdue,

    /// <summary>
    /// Calibration status unknown
    /// </summary>
    Unknown,
}

/// <summary>
/// Device health DTO for API operations
/// </summary>
public class DeviceHealth
{
    /// <summary>
    /// Primary key identifier
    /// </summary>
    [JsonPropertyName("id")]
    public Guid Id { get; set; }

    /// <summary>
    /// User identifier this device belongs to
    /// </summary>
    [JsonPropertyName("userId")]
    public string UserId { get; set; } = string.Empty;

    /// <summary>
    /// Unique device identifier
    /// </summary>
    [JsonPropertyName("deviceId")]
    public string DeviceId { get; set; } = string.Empty;

    /// <summary>
    /// Type of device (CGM, InsulinPump, BGM, Unknown)
    /// </summary>
    [JsonPropertyName("deviceType")]
    public DeviceType DeviceType { get; set; }

    /// <summary>
    /// Human-readable device name
    /// </summary>
    [JsonPropertyName("deviceName")]
    public string DeviceName { get; set; } = string.Empty;

    /// <summary>
    /// Device manufacturer
    /// </summary>
    [JsonPropertyName("manufacturer")]
    public string? Manufacturer { get; set; }

    /// <summary>
    /// Device model
    /// </summary>
    [JsonPropertyName("model")]
    public string? Model { get; set; }

    /// <summary>
    /// Device serial number
    /// </summary>
    [JsonPropertyName("serialNumber")]
    public string? SerialNumber { get; set; }

    /// <summary>
    /// Current battery level percentage (0-100)
    /// </summary>
    [JsonPropertyName("batteryLevel")]
    public decimal? BatteryLevel { get; set; }

    /// <summary>
    /// When the sensor expires (for CGM devices)
    /// </summary>
    [JsonPropertyName("sensorExpiration")]
    public DateTime? SensorExpiration { get; set; }

    /// <summary>
    /// When the device was last calibrated
    /// </summary>
    [JsonPropertyName("lastCalibration")]
    public DateTime? LastCalibration { get; set; }

    /// <summary>
    /// When data was last received from this device
    /// </summary>
    [JsonPropertyName("lastDataReceived")]
    public DateTime? LastDataReceived { get; set; }

    /// <summary>
    /// When the last maintenance alert was sent
    /// </summary>
    [JsonPropertyName("lastMaintenanceAlert")]
    public DateTime? LastMaintenanceAlert { get; set; }

    /// <summary>
    /// Battery warning threshold percentage
    /// </summary>
    [JsonPropertyName("batteryWarningThreshold")]
    public decimal BatteryWarningThreshold { get; set; } = 20.0m;

    /// <summary>
    /// Sensor expiration warning time in hours
    /// </summary>
    [JsonPropertyName("sensorExpirationWarningHours")]
    public int SensorExpirationWarningHours { get; set; } = 24;

    /// <summary>
    /// Data gap warning threshold in minutes
    /// </summary>
    [JsonPropertyName("dataGapWarningMinutes")]
    public int DataGapWarningMinutes { get; set; } = 30;

    /// <summary>
    /// Calibration reminder interval in hours
    /// </summary>
    [JsonPropertyName("calibrationReminderHours")]
    public int CalibrationReminderHours { get; set; } = 12;

    /// <summary>
    /// Current device status
    /// </summary>
    [JsonPropertyName("status")]
    public DeviceStatusType Status { get; set; }

    /// <summary>
    /// Last error message from the device
    /// </summary>
    [JsonPropertyName("lastErrorMessage")]
    public string? LastErrorMessage { get; set; }

    /// <summary>
    /// When the device status was last updated
    /// </summary>
    [JsonPropertyName("lastStatusUpdate")]
    public DateTime? LastStatusUpdate { get; set; }

    /// <summary>
    /// When this device health record was created
    /// </summary>
    [JsonPropertyName("createdAt")]
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    /// <summary>
    /// When this device health record was last updated
    /// </summary>
    [JsonPropertyName("updatedAt")]
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}
