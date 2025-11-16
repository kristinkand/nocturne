using System;
using Nocturne.Core.Models;

namespace Nocturne.Connectors.Core.Models;

/// <summary>
/// Extension methods for converting between external API formats and shared Core models
/// Simplified after model consolidation - only handles necessary external API conversions
/// </summary>
public static class ModelExtensions
{
    /// <summary>
    /// Convert Treatment to the shared Treatment model
    /// </summary>
    public static Treatment ToTreatment(this Treatment nightscoutTreatment)
    {
        return new Treatment
        {
            Id = nightscoutTreatment.Id,
            Mills = nightscoutTreatment.Mills,
            CreatedAt = nightscoutTreatment.CreatedAt,
            EventType = nightscoutTreatment.EventType,
            Reason = nightscoutTreatment.Reason,
            Glucose = nightscoutTreatment.Glucose,
            GlucoseType = nightscoutTreatment.GlucoseType,
            Carbs = nightscoutTreatment.Carbs,
            Insulin = nightscoutTreatment.Insulin,
            Notes = nightscoutTreatment.Notes,
            Duration = nightscoutTreatment.Duration,
            Source = nightscoutTreatment.Source,
        };
    }
}
