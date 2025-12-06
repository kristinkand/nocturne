using Nocturne.API.Controllers.V4;
using Nocturne.Infrastructure.Data.Abstractions;
using Nocturne.Core.Oref;
using OrefModels = Nocturne.Core.Oref.Models;

namespace Nocturne.API.Services;

/// <summary>
/// Implementation of the prediction service using oref algorithms.
/// Fetches current glucose, treatments, and profile data to calculate predictions.
/// </summary>
public class PredictionService : IPredictionService
{
    private readonly IPostgreSqlService _postgresService;
    private readonly ILogger<PredictionService> _logger;

    public PredictionService(
        IPostgreSqlService postgresService,
        ILogger<PredictionService> logger)
    {
        _postgresService = postgresService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<GlucosePredictionResponse> GetPredictionsAsync(
        string? profileId = null,
        CancellationToken cancellationToken = default)
    {
        var now = DateTimeOffset.UtcNow;

        // Check if oref library is available
        if (!OrefService.IsAvailable())
        {
            _logger.LogWarning("Oref library is not available - returning fallback prediction");
            return await GetFallbackPredictionsAsync(now, cancellationToken);
        }

        // Fetch recent glucose readings (last 10 entries for delta calculation)
        var glucoseEntries = await _postgresService.GetEntriesAsync(
            type: "sgv",
            count: 10,
            skip: 0,
            cancellationToken);

        if (!glucoseEntries.Any())
        {
            throw new InvalidOperationException("No glucose readings available for predictions");
        }

        // Convert to oref glucose readings
        var orefGlucose = glucoseEntries
            .Where(e => e.Sgv.HasValue && e.Sgv > 0)
            .OrderByDescending(e => e.Mills)
            .Select(e => new OrefModels.GlucoseReading
            {
                Sgv = e.Sgv ?? 0,
                Date = e.Mills,
                Direction = e.Direction
            })
            .ToList();

        if (!orefGlucose.Any())
        {
            throw new InvalidOperationException("No valid glucose readings available");
        }

        // Calculate glucose status (delta, avgdelta)
        var glucoseStatus = OrefService.CalculateGlucoseStatus(orefGlucose);
        if (glucoseStatus == null)
        {
            _logger.LogWarning("Failed to calculate glucose status - using fallback");
            return await GetFallbackPredictionsAsync(now, cancellationToken);
        }

        // Fetch recent treatments (last 100 for IOB calculation)
        var treatments = await _postgresService.GetTreatmentsAsync(
            count: 100,
            skip: 0,
            cancellationToken);

        // Convert to oref treatments
        var orefTreatments = treatments
            .Select(t => new OrefModels.OrefTreatment
            {
                EventType = t.EventType ?? "",
                Mills = t.Mills,
                Insulin = t.Insulin,
                Carbs = t.Carbs,
                Rate = t.Rate,
                Duration = (int?)(t.Duration ?? 0),
            })
            .ToList();

        // Get or create default profile
        var profile = await GetProfileAsync(profileId, cancellationToken);

        // Calculate IOB
        var iobData = OrefService.CalculateIob(profile, orefTreatments, now);
        if (iobData == null)
        {
            iobData = new OrefModels.IobData { Iob = 0, Activity = 0 };
        }

        // Calculate COB
        var cobResult = OrefService.CalculateCob(profile, orefGlucose, orefTreatments, now);
        var cob = cobResult?.Cob ?? 0;

        // Current temp basal (simplified - no active temp)
        var currentTemp = new OrefModels.CurrentTemp { Rate = profile.CurrentBasal, Duration = 0 };

        // Get predictions
        var predictions = OrefService.GetPredictions(
            profile,
            glucoseStatus,
            iobData,
            currentTemp,
            autosensRatio: 1.0,
            cob: cob);

        return new GlucosePredictionResponse
        {
            Timestamp = now,
            CurrentBg = glucoseStatus.Glucose,
            Delta = glucoseStatus.Delta,
            EventualBg = predictions?.EventualBg ?? glucoseStatus.Glucose,
            Iob = predictions?.Iob ?? iobData.Iob,
            Cob = predictions?.Cob ?? cob,
            SensitivityRatio = predictions?.SensitivityRatio,
            IntervalMinutes = 5,
            Predictions = new PredictionCurves
            {
                Default = predictions?.PredictedBg,
                IobOnly = predictions?.PredBgsIob,
                Uam = predictions?.PredBgsUam,
                Cob = predictions?.PredBgsCob,
                ZeroTemp = predictions?.PredBgsZt
            }
        };
    }

    /// <summary>
    /// Get or create a default oref profile.
    /// </summary>
    private async Task<OrefModels.OrefProfile> GetProfileAsync(string? profileId, CancellationToken cancellationToken)
    {
        // Try to fetch profile from database
        try
        {
            var profiles = await _postgresService.GetProfilesAsync(1, 0, cancellationToken);
            var dbProfile = profiles.FirstOrDefault();

            if (dbProfile?.Store != null && dbProfile.Store.Count > 0)
            {
                var activeStore = dbProfile.Store.Values.FirstOrDefault();
                if (activeStore != null)
                {
                    return new OrefModels.OrefProfile
                    {
                        Dia = activeStore.Dia,
                        CurrentBasal = activeStore.Basal?.FirstOrDefault()?.Value ?? 1.0,
                        Sens = activeStore.Sens?.FirstOrDefault()?.Value ?? 50.0,
                        CarbRatio = activeStore.CarbRatio?.FirstOrDefault()?.Value ?? 10.0,
                        MinBg = activeStore.TargetLow?.FirstOrDefault()?.Value ?? 100.0,
                        MaxBg = activeStore.TargetHigh?.FirstOrDefault()?.Value ?? 120.0,
                        MaxIob = 10.0,
                        MaxBasal = 4.0,
                        MaxDailyBasal = 2.0
                    };
                }
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to fetch profile, using defaults");
        }

        // Return default profile
        return new OrefModels.OrefProfile
        {
            Dia = 3.0,
            CurrentBasal = 1.0,
            Sens = 50.0,
            CarbRatio = 10.0,
            MinBg = 100.0,
            MaxBg = 120.0,
            MaxIob = 10.0,
            MaxBasal = 4.0,
            MaxDailyBasal = 2.0
        };
    }

    /// <summary>
    /// Get fallback predictions when oref is not available.
    /// Uses simple linear extrapolation based on current delta.
    /// </summary>
    private async Task<GlucosePredictionResponse> GetFallbackPredictionsAsync(
        DateTimeOffset now,
        CancellationToken cancellationToken)
    {
        // Get current entry
        var currentEntry = await _postgresService.GetCurrentEntryAsync(cancellationToken);

        if (currentEntry?.Sgv == null)
        {
            throw new InvalidOperationException("No glucose readings available");
        }

        var currentBg = currentEntry.Sgv.Value;
        var delta = currentEntry.Delta ?? 0;

        // Simple linear prediction (48 points = 4 hours)
        var predictions = new List<double>();
        for (int i = 0; i < 48; i++)
        {
            var minutes = i * 5;
            var decayFactor = Math.Exp(-minutes / 60.0); // Delta decays over time
            var predicted = currentBg + (delta * i * decayFactor);
            predictions.Add(Math.Max(39, Math.Min(400, predicted))); // Clamp to valid range
        }

        return new GlucosePredictionResponse
        {
            Timestamp = now,
            CurrentBg = currentBg,
            Delta = delta,
            EventualBg = predictions.LastOrDefault(),
            Iob = 0,
            Cob = 0,
            IntervalMinutes = 5,
            Predictions = new PredictionCurves
            {
                Default = predictions
            }
        };
    }
}
