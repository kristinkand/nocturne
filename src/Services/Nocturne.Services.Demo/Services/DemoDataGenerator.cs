using Microsoft.Extensions.Options;
using Nocturne.Core.Models;
using Nocturne.Services.Demo.Configuration;

namespace Nocturne.Services.Demo.Services;

/// <summary>
/// Interface for generating demo glucose and treatment data.
/// </summary>
public interface IDemoDataGenerator
{
    /// <summary>
    /// Whether the generator is currently running.
    /// </summary>
    bool IsRunning { get; }

    /// <summary>
    /// Gets the current configuration.
    /// </summary>
    DemoModeConfiguration GetConfiguration();

    /// <summary>
    /// Generates a single glucose entry for the current time.
    /// </summary>
    Entry GenerateCurrentEntry();

    /// <summary>
    /// Generates historical data for the configured time period.
    /// </summary>
    (List<Entry> Entries, List<Treatment> Treatments) GenerateHistoricalData();
}

/// <summary>
/// Generates realistic demo CGM and treatment data using pharmacokinetic models.
/// </summary>
public class DemoDataGenerator : IDemoDataGenerator
{
    private readonly ILogger<DemoDataGenerator> _logger;
    private readonly DemoModeConfiguration _config;
    private readonly Random _random = new();
    private double _currentGlucose;
    private readonly object _lock = new();

    private enum DayScenario
    {
        Normal,
        HighDay,
        LowDay,
        Exercise,
        SickDay,
        StressDay,
        PoorSleep,
    }

    public bool IsRunning { get; internal set; }

    public DemoDataGenerator(
        IOptions<DemoModeConfiguration> config,
        ILogger<DemoDataGenerator> logger
    )
    {
        _logger = logger;
        _config = config.Value;
        _currentGlucose = _config.InitialGlucose;
    }

    public DemoModeConfiguration GetConfiguration() => _config;

    public Entry GenerateCurrentEntry()
    {
        lock (_lock)
        {
            var change = GenerateRandomWalk();
            _currentGlucose = Math.Max(
                _config.MinGlucose,
                Math.Min(_config.MaxGlucose, _currentGlucose + change)
            );

            var now = DateTime.UtcNow;
            var mills = new DateTimeOffset(now).ToUnixTimeMilliseconds();
            var direction = CalculateDirection(change);

            return new Entry
            {
                Type = "sgv",
                Device = _config.Device,
                Mills = mills,
                Date = now,
                DateString = now.ToString("o"),
                Mgdl = Math.Round(_currentGlucose, 0),
                Sgv = Math.Round(_currentGlucose, 0),
                Direction = direction.ToString(),
                Delta = Math.Round(change, 1),
                IsDemo = true,
                Filtered = Math.Round(_currentGlucose + (_random.NextDouble() - 0.5) * 2, 0),
                Unfiltered = Math.Round(_currentGlucose + (_random.NextDouble() - 0.5) * 5, 0),
                Rssi = _random.Next(0, 101),
                Noise = _random.Next(0, 5),
                CreatedAt = now.ToString("o"),
                ModifiedAt = now,
            };
        }
    }

    public (List<Entry> Entries, List<Treatment> Treatments) GenerateHistoricalData()
    {
        var endDate = DateTime.UtcNow;
        var startDate = endDate.AddDays(-_config.HistoryDays);

        var entries = new List<Entry>();
        var treatments = new List<Treatment>();

        _logger.LogInformation(
            "Generating historical demo data from {StartDate} to {EndDate}",
            startDate,
            endDate
        );

        var currentDay = startDate.Date;
        double? previousDayEndingGlucose = null;
        double previousDayMomentum = 0;

        while (currentDay <= endDate.Date)
        {
            var dayScenario = SelectDayScenario(currentDay);
            var (dayEntries, dayTreatments, endingGlucose, endingMomentum) = GenerateDayData(
                currentDay,
                dayScenario,
                previousDayEndingGlucose,
                previousDayMomentum
            );

            entries.AddRange(dayEntries);
            treatments.AddRange(dayTreatments);

            previousDayEndingGlucose = endingGlucose;
            previousDayMomentum = endingMomentum;
            currentDay = currentDay.AddDays(1);
        }

        _logger.LogInformation(
            "Generated {EntryCount} entries and {TreatmentCount} treatments",
            entries.Count,
            treatments.Count
        );

        return (entries, treatments);
    }

    private DayScenario SelectDayScenario(DateTime date)
    {
        var roll = _random.Next(100);
        var isWeekend = date.DayOfWeek == DayOfWeek.Saturday || date.DayOfWeek == DayOfWeek.Sunday;

        // T1D management is hard - fewer "normal" days, more challenging days
        if (isWeekend)
        {
            return roll switch
            {
                < 25 => DayScenario.Normal,
                < 45 => DayScenario.HighDay,
                < 60 => DayScenario.Exercise,
                < 75 => DayScenario.PoorSleep,
                < 88 => DayScenario.LowDay,
                < 95 => DayScenario.StressDay,
                _ => DayScenario.SickDay,
            };
        }

        return roll switch
        {
            < 35 => DayScenario.Normal, // Only ~35% truly "normal" days
            < 55 => DayScenario.HighDay, // High days are common
            < 70 => DayScenario.LowDay,
            < 80 => DayScenario.Exercise,
            < 90 => DayScenario.StressDay,
            < 96 => DayScenario.PoorSleep,
            _ => DayScenario.SickDay,
        };
    }

    private (
        List<Entry> Entries,
        List<Treatment> Treatments,
        double EndingGlucose,
        double EndingMomentum
    ) GenerateDayData(
        DateTime date,
        DayScenario scenario,
        double? previousDayEndingGlucose = null,
        double previousDayMomentum = 0
    )
    {
        var entries = new List<Entry>();
        var treatments = new List<Treatment>();

        var scenarioParams = GetScenarioParameters(scenario);

        // Start from previous day's ending glucose if available, otherwise use fasting glucose
        // This ensures smooth transitions between days
        double glucose;
        if (previousDayEndingGlucose.HasValue)
        {
            // Blend the previous day's ending glucose with the scenario's expected fasting glucose
            // This creates a gradual overnight drift toward the day's expected pattern
            var targetFasting = scenarioParams.FastingGlucose + (_random.NextDouble() - 0.5) * 20;
            glucose = previousDayEndingGlucose.Value;

            // Calculate how much we expect to drift overnight (6 hours from midnight to ~6am)
            // We'll apply this drift gradually during the overnight hours
        }
        else
        {
            glucose = scenarioParams.FastingGlucose + (_random.NextDouble() - 0.5) * 20;
        }

        var currentTime = date;
        var endTime = date.AddDays(1);

        var mealPlan = GenerateMealPlan(date, scenario);
        var basalAdjustments = GenerateBasalAdjustments(date, scenario);

        var insulinEvents = new List<(DateTime Time, double Units)>();
        var carbEvents = new List<(DateTime Time, double Carbs, double GlycemicIndex)>();

        // Pre-populate insulin and carb events from meal plan
        foreach (var meal in mealPlan)
        {
            carbEvents.Add((meal.MealTime, meal.Carbs, meal.GlycemicIndex));

            var bolusTime = meal.MealTime.AddMinutes(meal.BolusOffsetMinutes);
            var bolus = CalculateMealBolus(meal.Carbs, glucose, scenarioParams);
            insulinEvents.Add((bolusTime, bolus));

            treatments.Add(CreateCarbTreatment(meal.MealTime, meal.Carbs, meal.FoodType));
            treatments.Add(
                CreateBolusTreatment(
                    bolusTime,
                    bolus,
                    meal.FoodType == "Snack" ? "Snack Bolus" : "Meal Bolus"
                )
            );
        }

        // Use previous day's momentum for continuity if available
        double glucoseMomentum = previousDayMomentum * 0.5; // Decay momentum slightly at day boundary
        double lastGlucose = glucose;

        while (currentTime < endTime)
        {
            var basalAdj = basalAdjustments.FirstOrDefault(b =>
                Math.Abs((b.Time - currentTime).TotalMinutes) < 2.5
            );
            if (basalAdj.Rate > 0 || basalAdj.Duration > 0)
            {
                treatments.Add(
                    CreateTempBasalTreatment(currentTime, basalAdj.Rate, basalAdj.Duration)
                );
            }

            glucose = SimulateGlucosePhysiological(
                glucose,
                currentTime,
                insulinEvents,
                carbEvents,
                scenarioParams,
                scenario,
                ref glucoseMomentum
            );

            // Allow wider range - T1D can go very high and very low
            glucose = Math.Max(35, Math.Min(450, glucose));

            // Handle low glucose correction - but not always caught in time
            if (glucose < 65 && _random.NextDouble() < 0.5)
            {
                // Sometimes overcorrect lows, sometimes undercorrect
                var correctionCarbs = _random.Next(8, 35);
                treatments.Add(CreateCarbCorrectionTreatment(currentTime, correctionCarbs));
                carbEvents.Add((currentTime, correctionCarbs, 1.6)); // Fast carbs for lows
            }

            // Handle high glucose correction - less aggressive, people don't always correct
            if (glucose > 220 && _random.NextDouble() < 0.25)
            {
                var correctionBolus = Math.Round(
                    (glucose - 130) / scenarioParams.CorrectionFactor,
                    1
                );
                // Sometimes under-dose corrections
                correctionBolus *= 0.6 + _random.NextDouble() * 0.6;
                if (correctionBolus >= 0.5)
                {
                    treatments.Add(CreateCorrectionBolusTreatment(currentTime, correctionBolus));
                    insulinEvents.Add((currentTime, correctionBolus));
                }
            }

            var delta = glucose - lastGlucose;
            entries.Add(CreateEntry(currentTime, glucose, delta));
            lastGlucose = glucose;

            currentTime = currentTime.AddMinutes(5);

            // Clean up old events
            insulinEvents.RemoveAll(e =>
                (currentTime - e.Time).TotalMinutes > _config.InsulinDurationMinutes + 30
            );
            carbEvents.RemoveAll(e =>
                (currentTime - e.Time).TotalMinutes > _config.CarbAbsorptionDurationMinutes + 30
            );
        }

        treatments.AddRange(GenerateScheduledBasal(date, scenarioParams));

        return (entries, treatments, glucose, glucoseMomentum);
    }

    private double SimulateGlucosePhysiological(
        double currentGlucose,
        DateTime time,
        List<(DateTime Time, double Units)> insulinEvents,
        List<(DateTime Time, double Carbs, double GlycemicIndex)> carbEvents,
        ScenarioParameters @params,
        DayScenario scenario,
        ref double momentum
    )
    {
        var glucose = currentGlucose;
        var hour = time.Hour + time.Minute / 60.0;

        // Calculate insulin effect - more aggressive, realistic drops
        var insulinEffect = CalculateInsulinEffect(time, insulinEvents, @params);

        // Calculate carb effect - sharper spikes that peak faster
        var carbEffect = CalculateCarbAbsorptionEffect(time, carbEvents, @params);

        // Basal effect - background insulin lowering glucose slightly each interval
        // Without basal, glucose would rise ~1-2 mg/dL per 5 min from liver glucose output
        var liverGlucoseOutput = 0.8 + _random.NextDouble() * 0.4; // ~1 mg/dL/5min average
        var basalCoverage = @params.BasalMultiplier * 0.9; // Basal mostly covers liver output
        var netBasalEffect = liverGlucoseOutput - basalCoverage;

        // Dawn phenomenon - stronger effect, liver dumps glucose 4-8am
        var dawnEffect = 0.0;
        if (hour >= 4 && hour < 8)
        {
            var dawnIntensity = Math.Sin((hour - 4) * Math.PI / 4); // Peaks around 6am
            dawnEffect = @params.DawnPhenomenonStrength * 3.0 * dawnIntensity;
        }

        // Exercise effects - can drop glucose 50-100 mg/dL over 2 hours
        var exerciseEffect = 0.0;
        if (@params.HasExercise)
        {
            if (hour >= 16 && hour < 17)
                exerciseEffect = -2.5; // During exercise - rapid drop
            else if (hour >= 17 && hour < 18)
                exerciseEffect = -1.8;
            else if (hour >= 18 && hour < 22)
                exerciseEffect = -0.8; // Post-exercise sensitivity
            else if (hour >= 22 || hour < 6)
                exerciseEffect = -0.3; // Overnight sensitivity increase
        }

        // Per-reading insulin absorption variability
        var insulinVariability = 0.8 + _random.NextDouble() * 0.4; // 80-120%

        // Net glucose change this interval
        var netChange =
            carbEffect
            - (insulinEffect * insulinVariability)
            + netBasalEffect
            + dawnEffect
            + exerciseEffect;

        // CGM noise and lag
        var noise = (_random.NextDouble() - 0.5) * 3.0;

        var targetChange = netChange + noise;

        // Minimal smoothing - real glucose moves sharply after meals
        // Only smooth to prevent unrealistic jumps, not to dampen real movement
        momentum = momentum * 0.1 + targetChange * 0.9;

        // Real CGM can show up to 4-5 mg/dL/min during rapid rises/falls
        // That's 20-25 mg/dL per 5-minute interval
        const double maxChangePerInterval = 20.0;
        momentum = Math.Clamp(momentum, -maxChangePerInterval, maxChangePerInterval);

        glucose += momentum;

        // Occasional CGM artifacts - compression lows, signal drops
        if (_random.NextDouble() < 0.005)
            glucose += (_random.NextDouble() - 0.5) * 25;

        // Scenario-specific modifiers
        if (scenario == DayScenario.SickDay)
            glucose += (_random.NextDouble() - 0.2) * 2.5; // Trend upward when sick
        else if (scenario == DayScenario.StressDay && _random.NextDouble() < 0.1)
            glucose += _random.Next(3, 12); // Stress hormones spike glucose

        return glucose;
    }

    /// <summary>
    /// Calculates the glucose-lowering effect of insulin at the current time.
    /// Uses a pharmacokinetic model where insulin peaks around 75 min and lasts ~4 hours.
    /// Returns mg/dL drop per 5-minute interval.
    /// </summary>
    private double CalculateInsulinEffect(
        DateTime currentTime,
        List<(DateTime Time, double Units)> insulinEvents,
        ScenarioParameters @params
    )
    {
        double totalEffect = 0;
        var peakTime = _config.InsulinPeakMinutes;
        var dia = _config.InsulinDurationMinutes;

        foreach (var (eventTime, units) in insulinEvents)
        {
            var minutesSince = (currentTime - eventTime).TotalMinutes;
            if (minutesSince < 0 || minutesSince > dia)
                continue;

            // Exponential model for insulin activity curve
            // Peaks at peakTime, then decays
            var tau = peakTime / 1.4;
            var normalizer = 1 / (1 - tau / dia + (1 + tau / dia) * Math.Exp(-dia / tau));
            var activity =
                normalizer * (minutesSince / (tau * tau)) * Math.Exp(-minutesSince / tau);

            // Each unit drops glucose by ISF over the DIA
            // At peak activity, ~40-50% of the drop happens in the hour around peak
            // ISF of 50 means 1 unit drops glucose 50 mg/dL total
            var isfPerInterval =
                _config.InsulinSensitivityFactor * @params.InsulinSensitivityMultiplier;
            var effectPerUnit = activity * isfPerInterval * (5.0 / 60.0) * 8.0; // Scaled for 5-min intervals

            totalEffect += effectPerUnit * units;
        }

        return totalEffect;
    }

    /// <summary>
    /// Calculates glucose rise from carb absorption.
    /// Carbs cause rapid spikes - 50g of fast carbs can raise glucose 100+ mg/dL in 30-60 min.
    /// Returns mg/dL rise per 5-minute interval.
    /// </summary>
    private double CalculateCarbAbsorptionEffect(
        DateTime currentTime,
        List<(DateTime Time, double Carbs, double GlycemicIndex)> carbEvents,
        ScenarioParameters @params
    )
    {
        double totalEffect = 0;
        var basePeakTime = _config.CarbAbsorptionPeakMinutes;
        var baseDuration = _config.CarbAbsorptionDurationMinutes;

        foreach (var (eventTime, carbs, gi) in carbEvents)
        {
            var minutesSince = (currentTime - eventTime).TotalMinutes;

            // Adjust timing based on glycemic index
            // High GI (1.5+) = faster absorption, peaks in 20-30 min
            // Low GI (0.5) = slower, peaks in 60-90 min
            var peakTime = basePeakTime / gi;
            var duration = baseDuration / gi;

            if (minutesSince < 0 || minutesSince > duration * 1.5)
                continue;

            // Gamma-like absorption curve - fast rise, slower tail
            var k = 2.5; // Shape parameter - higher = sharper peak
            var normalizedTime = minutesSince / peakTime;

            double absorptionRate;
            if (normalizedTime > 0)
            {
                // Gamma distribution PDF approximation
                absorptionRate =
                    Math.Pow(normalizedTime, k - 1) * Math.Exp(-normalizedTime * (k - 1) / 1.5);
                absorptionRate = Math.Max(0, absorptionRate);
            }
            else
            {
                absorptionRate = 0;
            }

            // Carb-to-glucose conversion
            // Rule of thumb: 1g carb raises BG by 3-5 mg/dL for a 150lb person without insulin
            // Spread this over the absorption duration, concentrated around peak
            var carbSensitivity = 4.0 / @params.InsulinSensitivityMultiplier; // mg/dL per gram
            var totalRise = carbs * carbSensitivity * gi; // Total expected rise from this meal
            var risePerInterval = absorptionRate * (totalRise / (duration / 5.0)) * 2.5; // Concentrated effect

            totalEffect += risePerInterval;
        }

        return totalEffect;
    }

    private ScenarioParameters GetScenarioParameters(DayScenario scenario)
    {
        // Add random daily variation - even "normal" days vary
        var dailyVariation = 0.8 + _random.NextDouble() * 0.4; // 80-120% effectiveness

        return scenario switch
        {
            DayScenario.Normal => new ScenarioParameters
            {
                FastingGlucose = 95 + _random.Next(-15, 35),
                CarbRatio = _config.CarbRatio * dailyVariation,
                CorrectionFactor = _config.CorrectionFactor * dailyVariation,
                BasalMultiplier = 0.9 + _random.NextDouble() * 0.2,
                InsulinSensitivityMultiplier = dailyVariation,
                DawnPhenomenonStrength = 0.2 + _random.NextDouble() * 0.3,
            },
            DayScenario.HighDay => new ScenarioParameters
            {
                FastingGlucose = 130 + _random.Next(0, 50),
                CarbRatio = _config.CarbRatio * 0.7 * dailyVariation,
                CorrectionFactor = _config.CorrectionFactor * 0.7,
                BasalMultiplier = 1.2 + _random.NextDouble() * 0.3,
                InsulinSensitivityMultiplier = 0.6 + _random.NextDouble() * 0.2,
                DawnPhenomenonStrength = 0.5 + _random.NextDouble() * 0.3,
            },
            DayScenario.LowDay => new ScenarioParameters
            {
                FastingGlucose = 75 + _random.Next(-10, 15),
                CarbRatio = _config.CarbRatio * 1.3 * dailyVariation,
                CorrectionFactor = _config.CorrectionFactor * 1.4,
                BasalMultiplier = 0.6 + _random.NextDouble() * 0.2,
                InsulinSensitivityMultiplier = 1.4 + _random.NextDouble() * 0.3,
                DawnPhenomenonStrength = 0.1,
            },
            DayScenario.Exercise => new ScenarioParameters
            {
                FastingGlucose = 90 + _random.Next(-10, 20),
                CarbRatio = _config.CarbRatio * 1.3,
                CorrectionFactor = _config.CorrectionFactor * 1.5,
                BasalMultiplier = 0.5 + _random.NextDouble() * 0.2,
                InsulinSensitivityMultiplier = 1.5 + _random.NextDouble() * 0.4,
                DawnPhenomenonStrength = 0.15,
                HasExercise = true,
            },
            DayScenario.SickDay => new ScenarioParameters
            {
                FastingGlucose = 150 + _random.Next(0, 60),
                CarbRatio = _config.CarbRatio * 0.55,
                CorrectionFactor = _config.CorrectionFactor * 0.5,
                BasalMultiplier = 1.4 + _random.NextDouble() * 0.3,
                InsulinSensitivityMultiplier = 0.45 + _random.NextDouble() * 0.15,
                DawnPhenomenonStrength = 0.6,
            },
            DayScenario.StressDay => new ScenarioParameters
            {
                FastingGlucose = 115 + _random.Next(0, 40),
                CarbRatio = _config.CarbRatio * 0.75,
                CorrectionFactor = _config.CorrectionFactor * 0.75,
                BasalMultiplier = 1.15 + _random.NextDouble() * 0.2,
                InsulinSensitivityMultiplier = 0.7 + _random.NextDouble() * 0.15,
                DawnPhenomenonStrength = 0.45,
            },
            DayScenario.PoorSleep => new ScenarioParameters
            {
                FastingGlucose = 120 + _random.Next(-10, 35),
                CarbRatio = _config.CarbRatio * 0.85,
                CorrectionFactor = _config.CorrectionFactor * 0.8,
                BasalMultiplier = 1.1 + _random.NextDouble() * 0.2,
                InsulinSensitivityMultiplier = 0.8 + _random.NextDouble() * 0.15,
                DawnPhenomenonStrength = 0.55,
            },
            _ => new ScenarioParameters
            {
                FastingGlucose = 100 + _random.Next(-15, 30),
                CarbRatio = _config.CarbRatio,
                CorrectionFactor = _config.CorrectionFactor,
                BasalMultiplier = 1.0,
                InsulinSensitivityMultiplier = 1.0,
                DawnPhenomenonStrength = 0.3,
            },
        };
    }

    private record MealEvent(
        DateTime MealTime,
        double Carbs,
        string FoodType,
        int BolusOffsetMinutes,
        double GlycemicIndex
    );

    private List<MealEvent> GenerateMealPlan(DateTime date, DayScenario scenario)
    {
        var meals = new List<MealEvent>();

        // Breakfast - often rushed, sometimes skipped, sometimes huge
        if (_random.NextDouble() > 0.1) // 10% chance of skipping
        {
            var breakfastHour = 6 + _random.Next(0, 4);
            var breakfastMinute = _random.Next(0, 12) * 5;
            var breakfastCarbs =
                scenario == DayScenario.LowDay ? _random.Next(15, 35)
                : scenario == DayScenario.HighDay ? _random.Next(60, 100)
                : _random.Next(25, 75);

            // Bolus timing - huge source of variability!
            // Negative = pre-bolus, Positive = late bolus
            int bolusOffset;
            var timingRoll = _random.NextDouble();
            if (timingRoll < 0.15)
                bolusOffset = _random.Next(-20, -5); // Pre-bolused (good practice but can cause lows)
            else if (timingRoll < 0.45)
                bolusOffset = _random.Next(0, 10); // Roughly on time
            else if (timingRoll < 0.75)
                bolusOffset = _random.Next(15, 40); // Late bolus - causes spike
            else if (timingRoll < 0.90)
                bolusOffset = _random.Next(40, 90); // Very late - major spike then crash
            else
                bolusOffset = _random.Next(90, 180); // Forgot, bolused way later

            meals.Add(
                new MealEvent(
                    date.AddHours(breakfastHour).AddMinutes(breakfastMinute),
                    breakfastCarbs,
                    "Breakfast",
                    bolusOffset,
                    0.6 + _random.NextDouble() * 1.0 // Wide GI variation (0.6-1.6)
                )
            );
        }

        // Lunch
        var lunchHour = 11 + _random.Next(0, 3);
        var lunchMinute = _random.Next(0, 12) * 5;
        var lunchCarbs =
            scenario == DayScenario.LowDay ? _random.Next(25, 50)
            : scenario == DayScenario.HighDay ? _random.Next(70, 130)
            : _random.Next(35, 90);

        // Lunch bolusing - eating out, distracted, guessing
        int lunchBolusOffset;
        var lunchTimingRoll = _random.NextDouble();
        if (lunchTimingRoll < 0.10)
            lunchBolusOffset = _random.Next(-15, 0);
        else if (lunchTimingRoll < 0.35)
            lunchBolusOffset = _random.Next(0, 15);
        else if (lunchTimingRoll < 0.65)
            lunchBolusOffset = _random.Next(15, 45);
        else if (lunchTimingRoll < 0.85)
            lunchBolusOffset = _random.Next(45, 90);
        else
            lunchBolusOffset = _random.Next(90, 150);

        meals.Add(
            new MealEvent(
                date.AddHours(lunchHour).AddMinutes(lunchMinute),
                lunchCarbs,
                "Lunch",
                lunchBolusOffset,
                0.5 + _random.NextDouble() * 1.2 // Restaurant food varies wildly
            )
        );

        // Dinner - highly variable, often complex foods
        var dinnerHour = 17 + _random.Next(0, 4);
        var dinnerMinute = _random.Next(0, 12) * 5;
        var dinnerCarbs =
            scenario == DayScenario.LowDay ? _random.Next(30, 60)
            : scenario == DayScenario.HighDay ? _random.Next(80, 150)
            : _random.Next(45, 120);

        // Dinner timing - pizza/pasta causes extended highs, pre-bolusing can cause lows
        int dinnerBolusOffset;
        var dinnerTimingRoll = _random.NextDouble();
        if (dinnerTimingRoll < 0.20)
            dinnerBolusOffset = _random.Next(-25, -5); // Pre-bolused
        else if (dinnerTimingRoll < 0.45)
            dinnerBolusOffset = _random.Next(0, 20);
        else if (dinnerTimingRoll < 0.70)
            dinnerBolusOffset = _random.Next(20, 50);
        else
            dinnerBolusOffset = _random.Next(50, 120); // Distracted, forgot

        meals.Add(
            new MealEvent(
                date.AddHours(dinnerHour).AddMinutes(dinnerMinute),
                dinnerCarbs,
                "Dinner",
                dinnerBolusOffset,
                0.4 + _random.NextDouble() * 1.4 // Very wide GI (0.4-1.8) - pizza is slow, rice is fast
            )
        );

        // Snacks - often un-bolused or poorly bolused
        if (_random.NextDouble() < 0.6)
        {
            var snackBolus =
                _random.NextDouble() < 0.4 ? _random.Next(30, 120) : _random.Next(0, 20);
            meals.Add(
                new MealEvent(
                    date.AddHours(10 + _random.NextDouble() * 1.5),
                    _random.Next(15, 40),
                    "Snack",
                    snackBolus, // Often very late or no bolus for snacks
                    1.2 + _random.NextDouble() * 0.5
                )
            );
        }

        if (_random.NextDouble() < 0.6)
        {
            var snackBolus =
                _random.NextDouble() < 0.5 ? _random.Next(20, 90) : _random.Next(0, 15);
            meals.Add(
                new MealEvent(
                    date.AddHours(15 + _random.NextDouble() * 1.5),
                    _random.Next(15, 45),
                    "Snack",
                    snackBolus,
                    1.1 + _random.NextDouble() * 0.6
                )
            );
        }

        // Late night snacking
        if (_random.NextDouble() < 0.4)
        {
            meals.Add(
                new MealEvent(
                    date.AddHours(21 + _random.NextDouble() * 2),
                    _random.Next(15, 50),
                    "Snack",
                    _random.Next(5, 45), // Late night = less likely to bolus properly
                    1.0 + _random.NextDouble() * 0.5
                )
            );
        }

        // Random unplanned eating
        if (_random.NextDouble() < 0.3)
        {
            var randomHour = 8 + _random.Next(0, 12);
            meals.Add(
                new MealEvent(
                    date.AddHours(randomHour + _random.NextDouble()),
                    _random.Next(10, 35),
                    "Snack",
                    _random.Next(15, 60), // Impulsive eating = late or no bolus
                    1.4 // Usually high GI impulsive foods
                )
            );
        }

        return meals;
    }

    private List<(DateTime Time, double Rate, int Duration)> GenerateBasalAdjustments(
        DateTime date,
        DayScenario scenario
    )
    {
        var adjustments = new List<(DateTime Time, double Rate, int Duration)>();

        if (scenario == DayScenario.Exercise)
        {
            var exerciseHour = _random.Next(16, 20);
            adjustments.Add((date.AddHours(exerciseHour - 1), _config.BasalRate * 0.5, 120));
        }

        if (scenario == DayScenario.LowDay && _random.NextDouble() < 0.5)
        {
            var lowHour = _random.Next(10, 16);
            adjustments.Add((date.AddHours(lowHour), _config.BasalRate * 0.6, 60));
        }

        if (scenario == DayScenario.HighDay && _random.NextDouble() < 0.5)
        {
            var highHour = _random.Next(10, 18);
            adjustments.Add((date.AddHours(highHour), _config.BasalRate * 1.3, 120));
        }

        return adjustments;
    }

    private List<Treatment> GenerateScheduledBasal(DateTime date, ScenarioParameters @params)
    {
        var basalTreatments = new List<Treatment>();

        for (var hour = 0; hour < 24; hour++)
        {
            var baseRate = _config.BasalRate * @params.BasalMultiplier;
            var circadianMultiplier = hour switch
            {
                >= 3 and < 8 => 1.0
                    + (@params.DawnPhenomenonStrength * (1 - Math.Abs(hour - 5.5) / 2.5)),
                >= 12 and < 14 => 1.1,
                >= 22 or < 3 => 0.9,
                _ => 1.0,
            };

            var rate = Math.Round(baseRate * circadianMultiplier, 2);
            var time = date.AddHours(hour);
            var mills = new DateTimeOffset(time).ToUnixTimeMilliseconds();

            basalTreatments.Add(
                new Treatment
                {
                    EventType = "Temp Basal",
                    Rate = rate,
                    Duration = 60,
                    Mills = mills,
                    Created_at = time.ToString("o"),
                    EnteredBy = "demo-pump",
                    IsDemo = true,
                }
            );
        }

        return basalTreatments;
    }

    private double CalculateMealBolus(
        double carbs,
        double currentGlucose,
        ScenarioParameters @params
    )
    {
        // Carb counting is VERY inaccurate in real life
        // People routinely underestimate by 30-50% or overestimate significantly
        var carbCountingError = _random.NextDouble();
        double estimatedCarbs;
        if (carbCountingError < 0.3)
            estimatedCarbs = carbs * (0.5 + _random.NextDouble() * 0.3); // 50-80% - underestimate
        else if (carbCountingError < 0.7)
            estimatedCarbs = carbs * (0.8 + _random.NextDouble() * 0.4); // 80-120% - roughly right
        else
            estimatedCarbs = carbs * (1.2 + _random.NextDouble() * 0.5); // 120-170% - overestimate

        var carbBolus = estimatedCarbs / @params.CarbRatio;

        // Sometimes forget to add correction, sometimes over-correct
        var correctionBolus = 0.0;
        if (currentGlucose > 140 && _random.NextDouble() < 0.7) // 30% forget correction
        {
            correctionBolus = (currentGlucose - 120) / @params.CorrectionFactor;
            // Variable correction aggressiveness
            correctionBolus *= 0.5 + _random.NextDouble(); // 50-150% of calculated
        }

        // Sometimes people just round or guess
        var totalBolus = carbBolus + correctionBolus;

        // Occasional major errors
        if (_random.NextDouble() < 0.05)
            totalBolus *= 0.3 + _random.NextDouble() * 0.4; // Forgot most of bolus (30-70%)
        else if (_random.NextDouble() < 0.03)
            totalBolus *= 1.5 + _random.NextDouble() * 0.5; // Accidentally double-bolused or stacked

        return Math.Max(0, Math.Round(totalBolus, 1));
    }

    private Entry CreateEntry(DateTime time, double glucose, double? delta)
    {
        var mills = new DateTimeOffset(time).ToUnixTimeMilliseconds();
        var direction = CalculateDirection(delta ?? 0);

        return new Entry
        {
            Type = "sgv",
            Device = _config.Device,
            Mills = mills,
            Date = time,
            DateString = time.ToString("o"),
            Mgdl = Math.Round(glucose, 0),
            Sgv = Math.Round(glucose, 0),
            Direction = direction.ToString(),
            Delta = delta.HasValue ? Math.Round(delta.Value, 1) : null,
            IsDemo = true,
            Filtered = Math.Round(glucose + (_random.NextDouble() - 0.5) * 2, 0),
            Unfiltered = Math.Round(glucose + (_random.NextDouble() - 0.5) * 5, 0),
            Rssi = _random.Next(0, 101),
            Noise = _random.Next(0, 3),
            CreatedAt = time.ToString("o"),
            ModifiedAt = time,
        };
    }

    private Treatment CreateCarbTreatment(DateTime time, double carbs, string foodType)
    {
        return new Treatment
        {
            EventType = "Carbs",
            Carbs = carbs,
            FoodType = foodType,
            Mills = new DateTimeOffset(time).ToUnixTimeMilliseconds(),
            Created_at = time.ToString("o"),
            EnteredBy = "demo-user",
            IsDemo = true,
        };
    }

    private Treatment CreateBolusTreatment(DateTime time, double insulin, string eventType)
    {
        return new Treatment
        {
            EventType = eventType,
            Insulin = insulin,
            Mills = new DateTimeOffset(time).ToUnixTimeMilliseconds(),
            Created_at = time.ToString("o"),
            EnteredBy = "demo-user",
            IsDemo = true,
        };
    }

    private Treatment CreateCorrectionBolusTreatment(DateTime time, double insulin)
    {
        return new Treatment
        {
            EventType = "Correction Bolus",
            Insulin = insulin,
            Mills = new DateTimeOffset(time).ToUnixTimeMilliseconds(),
            Created_at = time.ToString("o"),
            EnteredBy = "demo-user",
            IsDemo = true,
        };
    }

    private Treatment CreateCarbCorrectionTreatment(DateTime time, double carbs)
    {
        return new Treatment
        {
            EventType = "Carb Correction",
            Carbs = carbs,
            Mills = new DateTimeOffset(time).ToUnixTimeMilliseconds(),
            Created_at = time.ToString("o"),
            EnteredBy = "demo-user",
            Notes = "Low treatment",
            IsDemo = true,
        };
    }

    private Treatment CreateTempBasalTreatment(DateTime time, double rate, int duration)
    {
        return new Treatment
        {
            EventType = "Temp Basal",
            Rate = rate,
            Duration = duration,
            Mills = new DateTimeOffset(time).ToUnixTimeMilliseconds(),
            Created_at = time.ToString("o"),
            EnteredBy = "demo-pump",
            IsDemo = true,
        };
    }

    private double GenerateRandomWalk(double variance = 0)
    {
        var v = variance > 0 ? variance : _config.WalkVariance;
        var u1 = _random.NextDouble();
        var u2 = _random.NextDouble();
        var z0 = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);
        return z0 * v;
    }

    private Direction CalculateDirection(double change)
    {
        return change switch
        {
            > 10 => Direction.DoubleUp,
            > 5 => Direction.SingleUp,
            > 2 => Direction.FortyFiveUp,
            > -2 => Direction.Flat,
            > -5 => Direction.FortyFiveDown,
            > -10 => Direction.SingleDown,
            _ => Direction.DoubleDown,
        };
    }

    private class ScenarioParameters
    {
        public double FastingGlucose { get; set; }
        public double CarbRatio { get; set; }
        public double CorrectionFactor { get; set; }
        public double BasalMultiplier { get; set; }
        public double InsulinSensitivityMultiplier { get; set; }
        public double DawnPhenomenonStrength { get; set; }
        public bool HasExercise { get; set; }
    }
}
