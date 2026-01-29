using Nocturne.Connectors.MyLife.Constants;
using Nocturne.Connectors.MyLife.Mappers.Handlers;
using Nocturne.Connectors.MyLife.Models;
using Nocturne.Core.Models;

namespace Nocturne.Connectors.MyLife.Mappers.Mappers;

internal sealed class MyLifeTreatmentMapper
{
    private static readonly IReadOnlyList<IMyLifeTreatmentHandler> Handlers =
    [
        new ManualBgTreatmentHandler(),
        new TotalDailyDoseTreatmentHandler(),
        new TempBasalTreatmentHandler(),
        new BolusTreatmentHandler(),
        new AlertTreatmentHandler(),
        new CarbCorrectionTreatmentHandler(),
        new BasalRateTreatmentHandler(),
        new ProfileSwitchTreatmentHandler(),
        new IndicationTreatmentHandler(),
        new PrimingTreatmentHandler(),
        new BasalAmountTreatmentHandler(),
        new SimpleMappedTreatmentHandler(
            new Dictionary<int, string>
            {
                { MyLifeEventTypeIds.PodActivated, MyLifeTreatmentTypes.PodActivated },
                { MyLifeEventTypeIds.PodDeactivated, MyLifeTreatmentTypes.PodDeactivated },
                { MyLifeEventTypeIds.PumpSuspend, MyLifeTreatmentTypes.PumpSuspend },
                { MyLifeEventTypeIds.PumpResume, MyLifeTreatmentTypes.PumpResume },
                { MyLifeEventTypeIds.DateChanged, MyLifeTreatmentTypes.DateChanged },
                { MyLifeEventTypeIds.TimeChanged, MyLifeTreatmentTypes.TimeChanged },
                { MyLifeEventTypeIds.SiteChange, MyLifeTreatmentTypes.SiteChange },
                { MyLifeEventTypeIds.Rewind, MyLifeTreatmentTypes.Rewind },
                { MyLifeEventTypeIds.BolusMaxChanged, MyLifeTreatmentTypes.BolusMaxChanged },
                { MyLifeEventTypeIds.BasalMaxChanged, MyLifeTreatmentTypes.BasalMaxChanged },
            }
        ),
    ];

    internal static IEnumerable<Treatment> MapTreatments(
        IEnumerable<MyLifeEvent> events,
        bool enableManualBgSync,
        bool enableMealCarbConsolidation,
        bool enableTempBasalConsolidation,
        int tempBasalConsolidationWindowMinutes
    )
    {
        var context = MyLifeTreatmentContext.Create(
            events,
            enableManualBgSync,
            enableMealCarbConsolidation,
            enableTempBasalConsolidation,
            tempBasalConsolidationWindowMinutes
        );
        var list = new List<Treatment>();
        foreach (var ev in events)
        {
            if (ev.Deleted)
            {
                continue;
            }

            foreach (var handler in Handlers)
            {
                if (!handler.CanHandle(ev))
                {
                    continue;
                }

                list.AddRange(handler.Handle(ev, context));

                break;
            }
        }

        // Post-process basal rate records to calculate durations from consecutive records
        CalculateBasalDurations(list);

        return list;
    }

    /// <summary>
    /// Calculate durations for basal rate records based on the time to the next basal record.
    /// Basal rate records from MyLife represent point-in-time snapshots of the basal rate.
    /// To calculate insulin delivered, we need the duration each rate was active.
    /// </summary>
    private static void CalculateBasalDurations(List<Treatment> treatments)
    {
        // Get all basal rate records that don't have a duration set
        // These are "Basal" type (not "Temp Basal" which already has duration from the pump)
        var basalRecords = treatments
            .Where(t =>
                t.EventType == MyLifeTreatmentTypes.Basal
                && (!t.Duration.HasValue || t.Duration == 0)
                && t.Rate.HasValue
                && t.Mills > 0
            )
            .OrderBy(t => t.Mills)
            .ToList();

        if (basalRecords.Count == 0)
        {
            return;
        }

        // Calculate duration from each record to the next
        for (int i = 0; i < basalRecords.Count; i++)
        {
            var current = basalRecords[i];

            if (i < basalRecords.Count - 1)
            {
                var next = basalRecords[i + 1];
                var durationMs = next.Mills - current.Mills;
                var durationMinutes = durationMs / 60000.0;

                // Normal basal rate changes happen frequently with loop systems
                current.Duration = Math.Min(durationMinutes, 24 * 60); // Cap at 24 hours

                // Calculate the insulin delivered during this period
                // Rate is U/hr, Duration is in minutes
                if (current.Rate.HasValue && current.Duration > 0)
                {
                    current.Insulin = (current.Rate.Value * current.Duration.Value) / 60.0;
                }
            }
            else
            {
                // Last record - we don't know when it ends, so don't set a duration
                // This prevents over-counting insulin for the most recent record
                current.Duration = 0;
            }
        }
    }
}
