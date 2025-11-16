using Nocturne.Core.Models;

namespace Nocturne.Core.Contracts;

/// <summary>
/// Interface for comprehensive glucose and treatment statistics calculations
/// </summary>
public interface IStatisticsService
{
    // Basic Statistics
    BasicGlucoseStats CalculateBasicStats(IEnumerable<double> glucoseValues);
    double CalculateMean(IEnumerable<double> values);
    double CalculatePercentile(IEnumerable<double> sortedValues, double percentile);
    IEnumerable<double> ExtractGlucoseValues(IEnumerable<Entry> entries);

    // Glycemic Variability
    GlycemicVariability CalculateGlycemicVariability(
        IEnumerable<double> values,
        IEnumerable<Entry> entries
    );
    double CalculateEstimatedA1C(double averageGlucose);
    double CalculateMAGE(IEnumerable<double> values);
    double CalculateCONGA(IEnumerable<double> values, int hours = 2);
    double CalculateADRR(IEnumerable<double> values);
    double CalculateLabilityIndex(IEnumerable<Entry> entries);
    double CalculateJIndex(IEnumerable<double> values, double mean);
    double CalculateHBGI(IEnumerable<double> values);
    double CalculateLBGI(IEnumerable<double> values);
    double CalculateGVI(IEnumerable<double> values, IEnumerable<Entry> entries);
    double CalculatePGS(IEnumerable<double> values, double gvi, double meanGlucose);

    // Time in Range
    TimeInRangeMetrics CalculateTimeInRange(
        IEnumerable<Entry> entries,
        GlycemicThresholds? thresholds = null
    );

    // Glucose Distribution
    IEnumerable<DistributionDataPoint> CalculateGlucoseDistribution(
        IEnumerable<Entry> entries,
        IEnumerable<DistributionBin>? bins = null
    );
    IEnumerable<DistributionDataPoint> CalculateGlucoseDistributionFromValues(
        IEnumerable<double> glucoseValues,
        IEnumerable<DistributionBin>? bins = null
    );
    string CalculateEstimatedHbA1C(IEnumerable<double> values);
    IEnumerable<AveragedStats> CalculateAveragedStats(IEnumerable<Entry> entries);

    // Treatment Statistics
    TreatmentSummary CalculateTreatmentSummary(IEnumerable<Treatment> treatments);
    OverallAverages? CalculateOverallAverages(IEnumerable<DayData> dailyDataPoints);
    double GetTotalInsulin(TreatmentSummary treatmentSummary);
    double GetBolusPercentage(TreatmentSummary treatmentSummary);
    double GetBasalPercentage(TreatmentSummary treatmentSummary);
    bool IsBolusTreatment(Treatment treatment);

    // Formatting Utilities
    string FormatInsulinDisplay(double value);
    string FormatCarbDisplay(double value);
    string FormatPercentageDisplay(double value);
    double RoundInsulinToPumpPrecision(double value, double step = 0.05);

    // Validation
    bool ValidateTreatmentData(Treatment treatment);
    IEnumerable<Treatment> CleanTreatmentData(IEnumerable<Treatment> treatments);

    // Unit Conversions
    double MgdlToMMOL(double mgdl);
    double MmolToMGDL(double mmol);
    string MgdlToMMOLString(double mgdl);

    // Comprehensive Analytics
    GlucoseAnalytics AnalyzeGlucoseData(
        IEnumerable<Entry> entries,
        IEnumerable<Treatment> treatments,
        ExtendedAnalysisConfig? config = null
    );
}
