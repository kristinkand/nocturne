using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services;

/// <summary>
/// AR2 forecasting service with 1:1 legacy JavaScript compatibility
/// Implements the exact algorithm from ClientApp/lib/plugins/ar2.js
/// </summary>
public class Ar2Service : IAr2Service
{
    private readonly ILogger<Ar2Service> _logger;

    // Legacy AR2 constants - exact values from ar2.js
    private const double BG_REF = 140.0; // Central tendency
    private const int BG_MIN = 36; // Not 39, but why? (legacy comment preserved)
    private const int BG_MAX = 400;
    private const double WARN_THRESHOLD = 0.05;
    private const double URGENT_THRESHOLD = 0.10;
    private const string AR2_COLOR = "cyan";

    // AR coefficients from legacy code
    private static readonly double[] AR = { -0.723, 1.716 };

    // Cone step factors for forecast cone generation (from legacy)
    private static readonly double[] CONE_STEPS =
    {
        0.020,
        0.041,
        0.061,
        0.081,
        0.099,
        0.116,
        0.132,
        0.146,
        0.159,
        0.171,
        0.182,
        0.192,
        0.201,
    };

    public Ar2Service(ILogger<Ar2Service> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Calculate AR2 forecast with exact legacy algorithm
    /// </summary>
    public async Task<Ar2Properties> CalculateForecastAsync(
        DData ddata,
        Dictionary<string, object> bgNowProperties,
        Dictionary<string, object> deltaProperties,
        Dictionary<string, object> settings,
        CancellationToken cancellationToken = default
    )
    {
        await Task.CompletedTask; // For async interface compatibility

        var result = new Ar2Properties();

        if (!CanForecast(bgNowProperties, deltaProperties))
        {
            result.Forecast = new Ar2Forecast
            {
                Predicted = new List<ForecastPoint>(),
                AvgLoss = 0,
            };
            return result;
        }

        // Generate forecast
        result.Forecast = GenerateForecast(bgNowProperties, deltaProperties);

        // Check for alarm conditions
        CheckForecast(result, settings);

        // Generate display line
        if (result.Forecast.Predicted.Count >= 3)
        {
            var predicted15Min = result.Forecast.Predicted[2];
            result.DisplayLine = $"BG 15m: {predicted15Min.Mgdl} mg/dl";
        }

        return result;
    }

    /// <summary>
    /// Generate forecast cone for visualization
    /// </summary>
    public async Task<List<ForecastPoint>> GenerateForecastConeAsync(
        DData ddata,
        Dictionary<string, object> bgNowProperties,
        Dictionary<string, object> deltaProperties,
        double coneFactor = 2.0,
        CancellationToken cancellationToken = default
    )
    {
        await Task.CompletedTask; // For async interface compatibility

        if (!CanForecast(bgNowProperties, deltaProperties))
        {
            return new List<ForecastPoint>();
        }

        var state = InitializeAr2State(bgNowProperties, deltaProperties);
        var conePoints = new List<ForecastPoint>();

        // Generate cone points using exact legacy algorithm
        foreach (var step in CONE_STEPS)
        {
            state = IncrementAr2State(state);

            // Add negative cone point if coneFactor > 0
            if (coneFactor > 0)
            {
                conePoints.Add(
                    GenerateAr2Point(
                        state,
                        new Ar2PointOptions
                        {
                            Offset = 2000,
                            ConeFactor = -coneFactor,
                            Step = step,
                        }
                    )
                );
            }

            // Add positive cone point
            conePoints.Add(
                GenerateAr2Point(
                    state,
                    new Ar2PointOptions
                    {
                        Offset = 4000,
                        ConeFactor = coneFactor,
                        Step = step,
                    }
                )
            );
        }

        return conePoints;
    }

    /// <summary>
    /// Check if forecasting is possible - exact legacy conditions
    /// </summary>
    public bool CanForecast(
        Dictionary<string, object> bgNowProperties,
        Dictionary<string, object> deltaProperties
    )
    {
        // Get BGNow mean value
        if (
            !bgNowProperties.TryGetValue("mean", out var bgNowMeanObj)
            || !IsNumeric(bgNowMeanObj, out var bgNowMean)
        )
        {
            return false;
        }

        // Get Delta mean5MinsAgo value
        if (
            !deltaProperties.TryGetValue("mean5MinsAgo", out var mean5MinsAgoObj)
            || !IsNumeric(mean5MinsAgoObj, out var mean5MinsAgo)
        )
        {
            return false;
        }

        // Exact legacy conditions
        return bgNowMean >= BG_MIN
            && !double.IsNaN(mean5MinsAgo)
            && !double.IsInfinity(mean5MinsAgo);
    }

    /// <summary>
    /// Generate forecast using exact legacy AR2 algorithm
    /// </summary>
    private Ar2Forecast GenerateForecast(
        Dictionary<string, object> bgNowProperties,
        Dictionary<string, object> deltaProperties
    )
    {
        var state = InitializeAr2State(bgNowProperties, deltaProperties);

        // Generate exactly 6 points as in legacy code
        for (int i = 0; i < 6; i++)
        {
            state = IncrementAr2State(state);
            state.Points.Add(GenerateAr2Point(state, new Ar2PointOptions { Offset = 2000 }));
        }

        // Calculate average loss exactly as in legacy
        var avgLoss = 0.0;
        var size = Math.Min(state.Points.Count - 1, 6);

        for (int j = 0; j <= size; j++)
        {
            avgLoss += (1.0 / size) * Math.Pow(Log10(state.Points[j].Mgdl / 120.0), 2);
        }

        return new Ar2Forecast { Predicted = state.Points, AvgLoss = avgLoss };
    }

    /// <summary>
    /// Initialize AR2 state with exact legacy algorithm
    /// </summary>
    private Ar2State InitializeAr2State(
        Dictionary<string, object> bgNowProperties,
        Dictionary<string, object> deltaProperties
    )
    {
        // Get values with exact legacy property access
        var bgNowMills = GetNumericValue(bgNowProperties, "mills", 0);
        var bgNowMean = GetNumericValue(bgNowProperties, "mean", 0);
        var mean5MinsAgo = GetNumericValue(deltaProperties, "mean5MinsAgo", 0);

        return new Ar2State
        {
            ForecastTime = (long)bgNowMills,
            Points = new List<ForecastPoint>(),
            Prev = Math.Log(mean5MinsAgo / BG_REF),
            Curr = Math.Log(bgNowMean / BG_REF),
        };
    }

    /// <summary>
    /// Increment AR2 state with exact legacy algorithm
    /// </summary>
    private Ar2State IncrementAr2State(Ar2State state)
    {
        return new Ar2State
        {
            ForecastTime = state.ForecastTime + (5 * 60 * 1000), // 5 minutes in ms
            Points = state.Points,
            Prev = state.Curr,
            Curr = AR[0] * state.Prev + AR[1] * state.Curr,
        };
    }

    /// <summary>
    /// Generate AR2 point with exact legacy calculation
    /// </summary>
    private ForecastPoint GenerateAr2Point(Ar2State state, Ar2PointOptions options)
    {
        var step = options.Step;
        var coneFactor = options.ConeFactor;
        var offset = options.Offset;

        var mgdl = (int)Math.Round(BG_REF * Math.Exp(state.Curr + coneFactor * step));

        return new ForecastPoint
        {
            Mills = state.ForecastTime + offset,
            Mgdl = Math.Max(BG_MIN, Math.Min(BG_MAX, mgdl)),
            Color = AR2_COLOR,
        };
    }

    /// <summary>
    /// Check forecast for alarm conditions - exact legacy logic
    /// </summary>
    private void CheckForecast(Ar2Properties properties, Dictionary<string, object> settings)
    {
        if (properties.Forecast == null)
            return;

        // Set alarm level based on avgLoss thresholds
        if (properties.Forecast.AvgLoss > URGENT_THRESHOLD)
        {
            properties.Level = "urgent";
        }
        else if (properties.Forecast.AvgLoss > WARN_THRESHOLD)
        {
            properties.Level = "warn";
        }

        // Determine event type based on 20-minute prediction (4th point)
        if (!string.IsNullOrEmpty(properties.Level) && properties.Forecast.Predicted.Count >= 4)
        {
            var in20mins = properties.Forecast.Predicted[3].Mgdl;

            // Get thresholds from settings
            var bgTargetTop = GetNumericValue(settings, "bgTargetTop", 180);
            var bgTargetBottom = GetNumericValue(settings, "bgTargetBottom", 80);
            var alarmHigh = GetBooleanValue(settings, "alarmHigh", true);
            var alarmLow = GetBooleanValue(settings, "alarmLow", true);

            if (alarmHigh && in20mins > bgTargetTop)
            {
                properties.EventName = "high";
            }
            else if (alarmLow && in20mins < bgTargetBottom)
            {
                properties.EventName = "low";
            }
        }
    }

    /// <summary>
    /// Log base 10 calculation - exact legacy implementation
    /// </summary>
    private static double Log10(double value)
    {
        return Math.Log(value) / Math.Log(10);
    }

    /// <summary>
    /// Helper to safely get numeric values from properties
    /// </summary>
    private static double GetNumericValue(
        Dictionary<string, object> properties,
        string key,
        double defaultValue
    )
    {
        if (properties.TryGetValue(key, out var value) && IsNumeric(value, out var numericValue))
        {
            return numericValue;
        }
        return defaultValue;
    }

    /// <summary>
    /// Helper to safely get boolean values from properties
    /// </summary>
    private static bool GetBooleanValue(
        Dictionary<string, object> properties,
        string key,
        bool defaultValue
    )
    {
        if (properties.TryGetValue(key, out var value))
        {
            if (value is bool boolValue)
                return boolValue;
            if (bool.TryParse(value?.ToString(), out var parsedBool))
                return parsedBool;
        }
        return defaultValue;
    }

    /// <summary>
    /// Helper to check if a value is numeric and convert it
    /// </summary>
    private static bool IsNumeric(object? value, out double result)
    {
        result = 0;

        if (value == null)
            return false;

        if (value is double d)
        {
            result = d;
            return true;
        }

        if (value is int i)
        {
            result = i;
            return true;
        }

        if (value is long l)
        {
            result = l;
            return true;
        }

        if (value is float f)
        {
            result = f;
            return true;
        }

        return double.TryParse(value.ToString(), out result);
    }
}

/// <summary>
/// Options for generating AR2 points
/// </summary>
public class Ar2PointOptions
{
    public double Step { get; set; } = 0;
    public double ConeFactor { get; set; } = 0;
    public long Offset { get; set; } = 0;
}
