using System.Text.Json.Serialization;

namespace Nocturne.Core.Models;

/// <summary>
/// Basic glucose statistics
/// </summary>
public class BasicGlucoseStats
{
    /// <summary>
    /// Total number of glucose readings
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Mean glucose value, rounded to one decimal place
    /// </summary>
    public double Mean { get; set; }

    /// <summary>
    /// Median glucose value
    /// </summary>
    public double Median { get; set; }

    /// <summary>
    /// Minimum glucose value recorded
    /// </summary>
    public double Min { get; set; }

    /// <summary>
    /// Maximum glucose value recorded
    /// </summary>
    public double Max { get; set; }

    /// <summary>
    /// Standard deviation of glucose values, rounded to one decimal place
    /// </summary>
    public double StandardDeviation { get; set; }

    /// <summary>
    /// Percentiles of glucose values
    /// </summary>
    public GlucosePercentiles Percentiles { get; set; } = new();
}

/// <summary>
/// Glucose percentile values
/// </summary>
public class GlucosePercentiles
{
    /// <summary>
    /// 5th percentile
    /// </summary>
    public double P5 { get; set; }

    /// <summary>
    /// 10th percentile
    /// </summary>
    public double P10 { get; set; }

    /// <summary>
    /// 25th percentile (first quartile)
    /// </summary>
    public double P25 { get; set; }

    /// <summary>
    /// 50th percentile (median)
    /// </summary>
    public double P50 { get; set; }

    /// <summary>
    /// 75th percentile (third quartile)
    /// </summary>
    public double P75 { get; set; }

    /// <summary>
    /// 90th percentile
    /// </summary>
    public double P90 { get; set; }

    /// <summary>
    /// 95th percentile
    /// </summary>
    public double P95 { get; set; }
}

/// <summary>
/// Glycemic variability metrics
/// </summary>
public class GlycemicVariability
{
    /// <summary>
    /// Traditional measure of dispersion, standardized for mean; Measures short-term, within-day variability
    /// </summary>
    public double CoefficientOfVariation { get; set; }

    /// <summary>
    /// Traditional measure of dispersion; Measures short-term, within-day variability
    /// </summary>
    public double StandardDeviation { get; set; }

    /// <summary>
    /// Average of all glycemic excursions (except excursion having value less than 1 SD from mean glucose) in a 24 h time period; Captures short-term, within-day variability
    /// </summary>
    public double MeanAmplitudeGlycemicExcursions { get; set; }

    /// <summary>
    /// Standard deviation of summated difference between current observation and previous observation; Captures short-term, within-day variability
    /// </summary>
    public double ContinuousOverlappingNetGlycemicAction { get; set; }

    /// <summary>
    /// Average Daily Risk Range
    /// </summary>
    public double AverageDailyRiskRange { get; set; }

    /// <summary>
    /// Lability Index
    /// </summary>
    public double LabilityIndex { get; set; }

    /// <summary>
    /// J-Index
    /// </summary>
    public double JIndex { get; set; }

    /// <summary>
    /// High Blood Glucose Index - risk index for hyperglycemia
    /// </summary>
    public double HighBloodGlucoseIndex { get; set; }

    /// <summary>
    /// Low Blood Glucose Index - risk index for hypoglycemia
    /// </summary>
    public double LowBloodGlucoseIndex { get; set; }

    /// <summary>
    /// Glycemic Variability Index - measures glucose line distance traveled; 1.0-1.2 low, 1.2-1.5 modest, greater than 1.5 high variability
    /// </summary>
    public double GlycemicVariabilityIndex { get; set; }

    /// <summary>
    /// Patient Glycemic Status - combines GVI, mean glucose, and time in range; less than or equal to 35 excellent (non-diabetic), 35-100 good, 100-150 poor, greater than 150 very poor
    /// </summary>
    public double PatientGlycemicStatus { get; set; }

    /// <summary>
    /// Estimated A1C from average glucose
    /// </summary>
    public double EstimatedA1c { get; set; }
}

/// <summary>
/// Glycemic thresholds for analysis
/// </summary>
public class GlycemicThresholds
{
    /// <summary>
    /// Threshold for severe low glucose (default: 54 mg/dL)
    /// </summary>
    public double SevereLow { get; set; } = 54;

    /// <summary>
    /// Threshold for low glucose (default: 70 mg/dL)
    /// </summary>
    public double Low { get; set; } = 70;

    /// <summary>
    /// Target range bottom threshold (default: 70 mg/dL)
    /// </summary>
    public double TargetBottom { get; set; } = 70;

    /// <summary>
    /// Target range top threshold (default: 180 mg/dL)
    /// </summary>
    public double TargetTop { get; set; } = 180;

    /// <summary>
    /// Tight target range bottom threshold (default: 70 mg/dL)
    /// </summary>
    public double TightTargetBottom { get; set; } = 70;

    /// <summary>
    /// Tight target range top threshold (default: 140 mg/dL)
    /// </summary>
    public double TightTargetTop { get; set; } = 140;

    /// <summary>
    /// Threshold for high glucose (default: 180 mg/dL)
    /// </summary>
    public double High { get; set; } = 180;

    /// <summary>
    /// Threshold for severe high glucose (default: 250 mg/dL)
    /// </summary>
    public double SevereHigh { get; set; } = 250;
}

/// <summary>
/// Time in range metrics
/// </summary>
public class TimeInRangeMetrics
{
    /// <summary>
    /// Percentages of time in each range
    /// </summary>
    public TimeInRangePercentages Percentages { get; set; } = new();

    /// <summary>
    /// Durations in each range (in minutes)
    /// </summary>
    public TimeInRangeDurations Durations { get; set; } = new();

    /// <summary>
    /// Number of episodes in each range
    /// </summary>
    public TimeInRangeEpisodes Episodes { get; set; } = new();
}

/// <summary>
/// Time in range percentages
/// </summary>
public class TimeInRangePercentages
{
    /// <summary>
    /// Percentage of time in severe low range (less than 54 mg/dL)
    /// </summary>
    public double SevereLow { get; set; }

    /// <summary>
    /// Percentage of time in low range (54-70 mg/dL)
    /// </summary>
    public double Low { get; set; }

    /// <summary>
    /// Percentage of time in target range (70-180 mg/dL)
    /// </summary>
    public double Target { get; set; }

    /// <summary>
    /// Percentage of time in tight target range (70-140 mg/dL)
    /// </summary>
    public double TightTarget { get; set; }

    /// <summary>
    /// Percentage of time in high range (180-250 mg/dL)
    /// </summary>
    public double High { get; set; }

    /// <summary>
    /// Percentage of time in severe high range (greater than 250 mg/dL)
    /// </summary>
    public double SevereHigh { get; set; }
}

/// <summary>
/// Time in range durations (in minutes)
/// </summary>
public class TimeInRangeDurations
{
    /// <summary>
    /// Duration in severe low range (minutes)
    /// </summary>
    public double SevereLow { get; set; }

    /// <summary>
    /// Duration in low range (minutes)
    /// </summary>
    public double Low { get; set; }

    /// <summary>
    /// Duration in target range (minutes)
    /// </summary>
    public double Target { get; set; }

    /// <summary>
    /// Duration in tight target range (minutes)
    /// </summary>
    public double TightTarget { get; set; }

    /// <summary>
    /// Duration in high range (minutes)
    /// </summary>
    public double High { get; set; }

    /// <summary>
    /// Duration in severe high range (minutes)
    /// </summary>
    public double SevereHigh { get; set; }
}

/// <summary>
/// Time in range episodes
/// </summary>
public class TimeInRangeEpisodes
{
    /// <summary>
    /// Number of severe low episodes
    /// </summary>
    public int SevereLow { get; set; }

    /// <summary>
    /// Number of low episodes
    /// </summary>
    public int Low { get; set; }

    /// <summary>
    /// Number of high episodes
    /// </summary>
    public int High { get; set; }

    /// <summary>
    /// Number of severe high episodes
    /// </summary>
    public int SevereHigh { get; set; }
}

/// <summary>
/// Glucose distribution data point
/// </summary>
public class DistributionDataPoint
{
    /// <summary>
    /// Range description (e.g., "70-80")
    /// </summary>
    public string Range { get; set; } = string.Empty;

    /// <summary>
    /// Number of readings in this range
    /// </summary>
    public int Count { get; set; }

    /// <summary>
    /// Percentage of total readings in this range
    /// </summary>
    public double Percent { get; set; }
}

/// <summary>
/// Distribution bin configuration
/// </summary>
public class DistributionBin
{
    /// <summary>
    /// Range description
    /// </summary>
    public string Range { get; set; } = string.Empty;

    /// <summary>
    /// Minimum value for this bin
    /// </summary>
    public double Min { get; set; }

    /// <summary>
    /// Maximum value for this bin
    /// </summary>
    public double Max { get; set; }
}

/// <summary>
/// Averaged statistics for a specific hour
/// </summary>
public class HourlyAveragedStats : BasicGlucoseStats
{
    /// <summary>
    /// Hour of the day (0-23)
    /// </summary>
    public int Hour { get; set; }
}

/// <summary>
/// Treatment summary for a collection of treatments
/// </summary>
public class TreatmentSummary
{
    /// <summary>
    /// Aggregated treatment totals
    /// </summary>
    public TreatmentTotals Totals { get; set; } = new();

    /// <summary>
    /// Total number of treatment entries
    /// </summary>
    public int TreatmentCount { get; set; }
}

/// <summary>
/// Treatment totals
/// </summary>
public class TreatmentTotals
{
    /// <summary>
    /// Food-related totals
    /// </summary>
    public FoodTotals Food { get; set; } = new();

    /// <summary>
    /// Insulin-related totals
    /// </summary>
    public InsulinTotals Insulin { get; set; } = new();
}

/// <summary>
/// Food totals
/// </summary>
public class FoodTotals
{
    /// <summary>
    /// Total carbohydrates in grams
    /// </summary>
    public double Carbs { get; set; }

    /// <summary>
    /// Total protein in grams
    /// </summary>
    public double Protein { get; set; }

    /// <summary>
    /// Total fat in grams
    /// </summary>
    public double Fat { get; set; }
}

/// <summary>
/// Insulin totals
/// </summary>
public class InsulinTotals
{
    /// <summary>
    /// Total bolus insulin in units
    /// </summary>
    public double Bolus { get; set; }

    /// <summary>
    /// Total basal insulin in units
    /// </summary>
    public double Basal { get; set; }
}

/// <summary>
/// Overall averages across multiple days
/// </summary>
public class OverallAverages
{
    /// <summary>
    /// Average total daily insulin
    /// </summary>
    public double AvgTotalDaily { get; set; }

    /// <summary>
    /// Average daily bolus insulin
    /// </summary>
    public double AvgBolus { get; set; }

    /// <summary>
    /// Average daily basal insulin
    /// </summary>
    public double AvgBasal { get; set; }

    /// <summary>
    /// Percentage of total insulin that is bolus
    /// </summary>
    public double BolusPercentage { get; set; }

    /// <summary>
    /// Percentage of total insulin that is basal
    /// </summary>
    public double BasalPercentage { get; set; }

    /// <summary>
    /// Average daily carbohydrates
    /// </summary>
    public double AvgCarbs { get; set; }

    /// <summary>
    /// Average daily protein
    /// </summary>
    public double AvgProtein { get; set; }

    /// <summary>
    /// Average daily fat
    /// </summary>
    public double AvgFat { get; set; }

    /// <summary>
    /// Average time in range percentage
    /// </summary>
    public double AvgTimeInRange { get; set; }

    /// <summary>
    /// Average tight time in range percentage
    /// </summary>
    public double AvgTightTimeInRange { get; set; }
}

/// <summary>
/// Day data containing treatments and metrics
/// </summary>
public class DayData
{
    /// <summary>
    /// Date in ISO format (YYYY-MM-DD)
    /// </summary>
    public string Date { get; set; } = string.Empty;

    /// <summary>
    /// Treatments for this day
    /// </summary>
    public IEnumerable<Treatment> Treatments { get; set; } = Enumerable.Empty<Treatment>();

    /// <summary>
    /// Summary of treatments for this day
    /// </summary>
    public TreatmentSummary TreatmentSummary { get; set; } = new();

    /// <summary>
    /// Time in range metrics for this day
    /// </summary>
    public TimeInRangeMetrics TimeInRanges { get; set; } = new();
}

/// <summary>
/// Extended analysis configuration
/// </summary>
public class ExtendedAnalysisConfig
{
    /// <summary>
    /// Glycemic thresholds to use for analysis
    /// </summary>
    public GlycemicThresholds Thresholds { get; set; } = new();

    /// <summary>
    /// Type of continuous glucose monitor sensor
    /// </summary>
    public string SensorType { get; set; } = "GENERIC_5MIN";

    /// <summary>
    /// Whether to include looping-specific metrics
    /// </summary>
    public bool IncludeLoopingMetrics { get; set; } = false;

    /// <summary>
    /// Units for glucose values (mg/dl or mmol/l)
    /// </summary>
    public string Units { get; set; } = "mg/dl";
}

/// <summary>
/// Data quality metrics
/// </summary>
public class DataQuality
{
    /// <summary>
    /// Total expected readings
    /// </summary>
    public int TotalReadings { get; set; }

    /// <summary>
    /// Number of missing readings
    /// </summary>
    public int MissingReadings { get; set; }

    /// <summary>
    /// Percentage of data completeness (0-100)
    /// </summary>
    public double DataCompleteness { get; set; }

    /// <summary>
    /// Percentage of time CGM was active (0-100)
    /// </summary>
    public double CgmActivePercent { get; set; }

    /// <summary>
    /// Analysis of data gaps
    /// </summary>
    public GapAnalysis GapAnalysis { get; set; } = new();

    /// <summary>
    /// Noise level in the data (0-1, where 0 is no noise)
    /// </summary>
    public double NoiseLevel { get; set; }

    /// <summary>
    /// Number of calibration events
    /// </summary>
    public int CalibrationEvents { get; set; }

    /// <summary>
    /// Number of sensor warmup periods
    /// </summary>
    public int SensorWarmups { get; set; }
}

/// <summary>
/// Gap analysis in data
/// </summary>
public class GapAnalysis
{
    /// <summary>
    /// Collection of identified data gaps
    /// </summary>
    public IEnumerable<DataGap> Gaps { get; set; } = Enumerable.Empty<DataGap>();

    /// <summary>
    /// Duration of the longest gap in minutes
    /// </summary>
    public double LongestGap { get; set; }

    /// <summary>
    /// Average gap duration in minutes
    /// </summary>
    public double AverageGap { get; set; }
}

/// <summary>
/// Data gap information
/// </summary>
public class DataGap
{
    /// <summary>
    /// Start time of the gap (milliseconds since epoch)
    /// </summary>
    public long Start { get; set; }

    /// <summary>
    /// End time of the gap (milliseconds since epoch)
    /// </summary>
    public long End { get; set; }

    /// <summary>
    /// Duration of the gap in minutes
    /// </summary>
    public double Duration { get; set; }
}

/// <summary>
/// Comprehensive glucose analytics result
/// </summary>
public class GlucoseAnalytics
{
    /// <summary>
    /// Basic statistical metrics
    /// </summary>
    public BasicGlucoseStats BasicStats { get; set; } = new();

    /// <summary>
    /// Time in range analysis
    /// </summary>
    public TimeInRangeMetrics TimeInRange { get; set; } = new();

    /// <summary>
    /// Glycemic variability metrics
    /// </summary>
    public GlycemicVariability GlycemicVariability { get; set; } = new();

    /// <summary>
    /// Data quality assessment
    /// </summary>
    public DataQuality DataQuality { get; set; } = new();

    /// <summary>
    /// Time period of the analysis
    /// </summary>
    public AnalysisTime Time { get; set; } = new();
}

/// <summary>
/// Analysis time information
/// </summary>
public class AnalysisTime
{
    /// <summary>
    /// Analysis start time (milliseconds since epoch)
    /// </summary>
    public long Start { get; set; }

    /// <summary>
    /// Analysis end time (milliseconds since epoch)
    /// </summary>
    public long End { get; set; }

    /// <summary>
    /// Time when the analysis was performed (milliseconds since epoch)
    /// </summary>
    public long TimeOfAnalysis { get; set; }
}

/// <summary>
/// Multi-period statistics response containing statistics for different time periods
/// </summary>
public class MultiPeriodStatistics
{
    /// <summary>
    /// Statistics for the last day (24 hours)
    /// </summary>
    public PeriodStatistics LastDay { get; set; } = new();

    /// <summary>
    /// Statistics for the last 3 days (72 hours)
    /// </summary>
    public PeriodStatistics Last3Days { get; set; } = new();

    /// <summary>
    /// Statistics for the last week (7 days)
    /// </summary>
    public PeriodStatistics LastWeek { get; set; } = new();

    /// <summary>
    /// Statistics for the last month (30 days)
    /// </summary>
    public PeriodStatistics LastMonth { get; set; } = new();

    /// <summary>
    /// Statistics for the last 90 days
    /// </summary>
    public PeriodStatistics Last90Days { get; set; } = new();

    /// <summary>
    /// When the statistics were last updated
    /// </summary>
    public DateTime LastUpdated { get; set; }
}

/// <summary>
/// Statistics for a specific time period
/// </summary>
public class PeriodStatistics
{
    /// <summary>
    /// Number of days in the period
    /// </summary>
    public int PeriodDays { get; set; }

    /// <summary>
    /// Start date of the period
    /// </summary>
    public DateTime StartDate { get; set; }

    /// <summary>
    /// End date of the period
    /// </summary>
    public DateTime EndDate { get; set; }

    /// <summary>
    /// Comprehensive glucose analytics for this period
    /// </summary>
    public GlucoseAnalytics? Analytics { get; set; }

    /// <summary>
    /// Treatment summary for this period
    /// </summary>
    public TreatmentSummary? TreatmentSummary { get; set; }

    /// <summary>
    /// Indicates if there was sufficient data for meaningful statistics
    /// </summary>
    public bool HasSufficientData { get; set; }

    /// <summary>
    /// Number of glucose entries in this period
    /// </summary>
    public int EntryCount { get; set; }

    /// <summary>
    /// Number of treatments in this period
    /// </summary>
    public int TreatmentCount { get; set; }
}

/// <summary>
/// Averaged statistics for a specific hour of the day
/// </summary>
public class AveragedStats : BasicGlucoseStats
{
    /// <summary>
    /// Hour of the day (0-23)
    /// </summary>
    public int Hour { get; set; }
}
