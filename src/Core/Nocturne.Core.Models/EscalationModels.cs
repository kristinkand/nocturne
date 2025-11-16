namespace Nocturne.Core.Models;

/// <summary>
/// Escalation plan configuration
/// </summary>
public class EscalationPlan
{
    /// <summary>
    /// List of escalation steps
    /// </summary>
    public List<EscalationStep> Steps { get; set; } = new();

    /// <summary>
    /// Maximum number of escalations before stopping
    /// </summary>
    public int MaxEscalations { get; set; } = 3;

    /// <summary>
    /// Initial delay before first escalation
    /// </summary>
    public TimeSpan InitialDelay { get; set; } = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Interval between escalation levels
    /// </summary>
    public TimeSpan EscalationInterval { get; set; } = TimeSpan.FromMinutes(15);
}

/// <summary>
/// Individual escalation step configuration
/// </summary>
public class EscalationStep
{
    /// <summary>
    /// Escalation level (1, 2, 3, etc.)
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// Notification channels to use at this level
    /// </summary>
    public List<string> Channels { get; set; } = new();

    /// <summary>
    /// Delay before this escalation level
    /// </summary>
    public TimeSpan Delay { get; set; }

    /// <summary>
    /// Whether acknowledgment is required at this level
    /// </summary>
    public bool RequireAcknowledgment { get; set; }
}

/// <summary>
/// Escalation attempt record
/// </summary>
public class EscalationAttempt
{
    /// <summary>
    /// Escalation level attempted
    /// </summary>
    public int Level { get; set; }

    /// <summary>
    /// When the escalation was attempted
    /// </summary>
    public DateTime AttemptTime { get; set; }

    /// <summary>
    /// Channels used for this attempt
    /// </summary>
    public List<string> ChannelsUsed { get; set; } = new();

    /// <summary>
    /// Whether the escalation was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// Failure reason if unsuccessful
    /// </summary>
    public string? FailureReason { get; set; }
}

/// <summary>
/// Snooze recommendation for alerts
/// </summary>
public class SnoozeRecommendation
{
    /// <summary>
    /// Recommended snooze duration in minutes
    /// </summary>
    public int RecommendedMinutes { get; set; }

    /// <summary>
    /// Reason for the recommendation
    /// </summary>
    public string Reason { get; set; } = string.Empty;

    /// <summary>
    /// Alternative snooze duration options
    /// </summary>
    public List<int> AlternativeOptions { get; set; } = new();
}

/// <summary>
/// Result of a snooze operation
/// </summary>
public class SnoozeResult
{
    /// <summary>
    /// Whether the snooze was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// When the snooze expires
    /// </summary>
    public DateTime? SnoozeUntil { get; set; }

    /// <summary>
    /// Error message if unsuccessful
    /// </summary>
    public string? ErrorMessage { get; set; }
}

/// <summary>
/// Result of an acknowledgment operation
/// </summary>
public class AcknowledgmentResult
{
    /// <summary>
    /// Whether the acknowledgment was successful
    /// </summary>
    public bool Success { get; set; }

    /// <summary>
    /// When the alert was acknowledged
    /// </summary>
    public DateTime? AcknowledgedAt { get; set; }

    /// <summary>
    /// Error message if unsuccessful
    /// </summary>
    public string? ErrorMessage { get; set; }
}
