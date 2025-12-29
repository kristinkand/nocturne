using System.Collections.Generic;

namespace Nocturne.API.Models;

public class ConnectorStatusDto
{
    public required string Id { get; set; }
    public required string Name { get; set; }
    public required string Status { get; set; }
    public string? Description { get; set; }
    public long TotalEntries { get; set; }
    public DateTime? LastEntryTime { get; set; }
    public int EntriesLast24Hours { get; set; }
    public string State { get; set; } = "Idle";
    public string? StateMessage { get; set; }
    public bool IsHealthy { get; set; }

    /// <summary>
    /// Breakdown of total items processed by data type
    /// Keys are data type names (e.g., "Glucose", "Treatments", "Food")
    /// </summary>
    public Dictionary<string, long>? TotalItemsBreakdown { get; set; }

    /// <summary>
    /// Breakdown of items processed in the last 24 hours by data type
    /// Keys are data type names (e.g., "Glucose", "Treatments", "Food")
    /// </summary>
    public Dictionary<string, int>? ItemsLast24HoursBreakdown { get; set; }
}

