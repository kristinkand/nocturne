using FluentAssertions;
using Nocturne.API.Services;
using Nocturne.Core.Models;

namespace Nocturne.API.Tests.Services;

/// <summary>
/// Comprehensive unit tests for the StatisticsService
/// Ensures 1:1 functionality parity with TypeScript utilities and covers all edge cases
/// </summary>
[Parity]
public class StatisticsServiceTests
{
    private readonly StatisticsService _statisticsService;

    public StatisticsServiceTests()
    {
        _statisticsService = new StatisticsService();
    }

    #region Basic Statistics Tests

    [Fact]
    public void CalculateBasicStats_WithValidGlucoseValues_ShouldReturnCorrectStatistics()
    {
        // Arrange
        var glucoseValues = new double[] { 70, 80, 90, 100, 110, 120, 130, 140, 150, 160 };

        // Act
        var result = _statisticsService.CalculateBasicStats(glucoseValues);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(10);
        result.Mean.Should().Be(115.0);
        result.Median.Should().Be(115.0);
        result.Min.Should().Be(70);
        result.Max.Should().Be(160);
        result.StandardDeviation.Should().BeApproximately(30.3, 0.1);
    }

    [Fact]
    public void CalculateBasicStats_WithEmptyValues_ShouldReturnZeroedStatistics()
    {
        // Arrange
        var glucoseValues = new double[] { };

        // Act
        var result = _statisticsService.CalculateBasicStats(glucoseValues);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(0);
        result.Mean.Should().Be(0);
        result.Median.Should().Be(0);
        result.Min.Should().Be(0);
        result.Max.Should().Be(0);
        result.StandardDeviation.Should().Be(0);
    }

    [Fact]
    public void CalculateBasicStats_WithInvalidValues_ShouldFilterOutInvalidReadings()
    {
        // Arrange
        var glucoseValues = new double[] { -10, 0, 50, 100, 150, 700, 800 };

        // Act
        var result = _statisticsService.CalculateBasicStats(glucoseValues);

        // Assert
        result.Should().NotBeNull();
        result.Count.Should().Be(3); // Only 50, 100, 150 are valid
        result.Mean.Should().Be(100.0);
    }

    [Fact]
    public void CalculateMean_WithValidValues_ShouldReturnRoundedMean()
    {
        // Arrange
        var values = new double[] { 100.1, 100.2, 100.3 };

        // Act
        var result = _statisticsService.CalculateMean(values);

        // Assert
        result.Should().Be(100.2);
    }

    [Fact]
    public void CalculatePercentile_WithSortedValues_ShouldReturnCorrectPercentile()
    {
        // Arrange
        var sortedValues = new double[] { 10, 20, 30, 40, 50, 60, 70, 80, 90, 100 };

        // Act
        var p25 = _statisticsService.CalculatePercentile(sortedValues, 25);
        var p50 = _statisticsService.CalculatePercentile(sortedValues, 50);
        var p75 = _statisticsService.CalculatePercentile(sortedValues, 75);

        // Assert
        p25.Should().BeApproximately(32.5, 0.1);
        p50.Should().BeApproximately(55, 0.1);
        p75.Should().BeApproximately(77.5, 0.1);
    }

    [Fact]
    public void ExtractGlucoseValues_WithMixedEntries_ShouldExtractValidValues()
    {
        // Arrange
        var entries = new[]
        {
            new Entry { Sgv = 100 },
            new Entry { Mgdl = 120 },
            new Entry { Sgv = null, Mgdl = 0 },
            new Entry { Sgv = 0 },
            new Entry { Sgv = 700 }, // Should be filtered out
            new Entry { Mgdl = 80 },
        };

        // Act
        var result = _statisticsService.ExtractGlucoseValues(entries).ToList();

        // Assert
        result.Should().HaveCount(3);
        result.Should().Contain(new[] { 100.0, 120.0, 80.0 });
    }

    #endregion

    #region Glycemic Variability Tests

    [Fact]
    public void CalculateGlycemicVariability_WithValidData_ShouldReturnCompleteMetrics()
    {
        // Arrange
        var values = new double[] { 70, 100, 130, 160, 190, 140, 110, 80 };
        var entries = values.Select(
            (v, i) =>
                new Entry
                {
                    Sgv = v,
                    Mills = DateTimeOffset.UtcNow.AddMinutes(i * 5).ToUnixTimeMilliseconds(),
                }
        );

        // Act
        var result = _statisticsService.CalculateGlycemicVariability(values, entries);

        // Assert
        result.Should().NotBeNull();
        result.CoefficientOfVariation.Should().BeGreaterThan(0);
        result.StandardDeviation.Should().BeGreaterThan(0);
        result.EstimatedA1c.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateGlycemicVariability_WithInsufficientData_ShouldThrowException()
    {
        // Arrange
        var values = new double[] { 100 };
        var entries = new[] { new Entry { Sgv = 100 } };

        // Act & Assert
        Action act = () => _statisticsService.CalculateGlycemicVariability(values, entries);
        act.Should()
            .Throw<ArgumentException>()
            .WithMessage("Not enough data points to calculate glycemic variability metrics");
    }

    [Fact]
    public void CalculateEstimatedA1C_WithValidAverageGlucose_ShouldReturnCorrectA1C()
    {
        // Arrange
        var averageGlucose = 154.0; // Should result in ~7.0% A1C

        // Act
        var result = _statisticsService.CalculateEstimatedA1C(averageGlucose);

        // Assert
        result.Should().BeApproximately(7.0, 0.1);
    }

    [Fact]
    public void CalculateEstimatedA1C_WithZeroGlucose_ShouldReturnZero()
    {
        // Arrange
        var averageGlucose = 0.0;

        // Act
        var result = _statisticsService.CalculateEstimatedA1C(averageGlucose);

        // Assert
        result.Should().Be(0);
    }

    [Fact]
    public void CalculateMAGE_WithValidValues_ShouldReturnPositiveValue()
    {
        // Arrange
        var values = new double[] { 100, 150, 120, 180, 90, 160, 110 };

        // Act
        var result = _statisticsService.CalculateMAGE(values);

        // Assert
        result.Should().BeGreaterThan(0);
    }

    [Fact]
    public void CalculateMAGE_WithInsufficientData_ShouldReturnZero()
    {
        // Arrange
        var values = new double[] { 100, 110 };

        // Act
        var result = _statisticsService.CalculateMAGE(values);

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region Time in Range Tests

    [Fact]
    public void CalculateTimeInRange_WithValidEntries_ShouldReturnCorrectPercentages()
    {
        // Arrange
        var entries = new[]
        {
            new Entry { Sgv = 50 }, // Severe low
            new Entry { Sgv = 65 }, // Low
            new Entry { Sgv = 100 }, // Target
            new Entry { Sgv = 150 }, // Target
            new Entry { Sgv = 200 }, // High
            new Entry { Sgv = 300 }, // Severe high
        };

        // Act
        var result = _statisticsService.CalculateTimeInRange(entries);

        // Assert
        result.Should().NotBeNull();
        result.Percentages.SevereLow.Should().BeApproximately(16.67, 0.1);
        result.Percentages.Low.Should().BeApproximately(16.67, 0.1);
        result.Percentages.Target.Should().BeApproximately(33.33, 0.1);
        result.Percentages.High.Should().BeApproximately(16.67, 0.1);
        result.Percentages.SevereHigh.Should().BeApproximately(16.67, 0.1);
    }

    [Fact]
    public void CalculateTimeInRange_WithCustomThresholds_ShouldUseCustomValues()
    {
        // Arrange
        var entries = new[]
        {
            new Entry { Sgv = 100 },
            new Entry { Sgv = 120 },
            new Entry { Sgv = 140 },
        };
        var customThresholds = new GlycemicThresholds { TargetBottom = 90, TargetTop = 130 };

        // Act
        var result = _statisticsService.CalculateTimeInRange(entries, customThresholds);

        // Assert
        result.Should().NotBeNull();
        result.Percentages.Target.Should().BeApproximately(66.67, 0.1);
    }

    [Fact]
    public void CalculateTimeInRange_WithEmptyEntries_ShouldReturnZeroMetrics()
    {
        // Arrange
        var entries = new Entry[] { };

        // Act
        var result = _statisticsService.CalculateTimeInRange(entries);

        // Assert
        result.Should().NotBeNull();
        result.Percentages.Target.Should().Be(0);
        result.Durations.Target.Should().Be(0);
    }

    #endregion

    #region Glucose Distribution Tests

    [Fact]
    public void CalculateGlucoseDistribution_WithValidEntries_ShouldReturnDistribution()
    {
        // Arrange
        var entries = new[]
        {
            new Entry { Sgv = 75 }, // 70-80 range
            new Entry { Sgv = 95 }, // 90-100 range
            new Entry { Sgv = 125 }, // 120-130 range
            new Entry { Sgv = 175 }, // 150-180 range
        };

        // Act
        var result = _statisticsService.CalculateGlucoseDistribution(entries).ToList();

        // Assert
        result.Should().NotBeEmpty();
        result.Should().HaveCount(4);
        result.Sum(r => r.Percent).Should().Be(100.0);
        result.All(r => r.Count == 1).Should().BeTrue();
        result.All(r => r.Percent == 25.0).Should().BeTrue();
    }

    [Fact]
    public void CalculateAveragedStats_WithValidEntries_ShouldReturn24HourStats()
    {
        // Arrange
        var entries = Enumerable
            .Range(0, 24)
            .Select(hour => new Entry
            {
                Sgv = 100 + hour * 2, // Gradually increasing glucose
                Date = DateTime.Today.AddHours(hour),
            });

        // Act
        var result = _statisticsService.CalculateAveragedStats(entries).ToList();

        // Assert
        result.Should().HaveCount(24);
        result.All(r => r.Hour >= 0 && r.Hour < 24).Should().BeTrue();
        result.Where(r => r.Count > 0).Should().HaveCount(24);
    }

    [Fact]
    public void CalculateAveragedStats_WithEmptyEntries_ShouldReturnEmpty24HourStats()
    {
        // Arrange
        var entries = new Entry[] { };

        // Act
        var result = _statisticsService.CalculateAveragedStats(entries).ToList();

        // Assert
        result.Should().HaveCount(24);
        result.All(r => r.Count == 0).Should().BeTrue();
        result.All(r => r.Mean == 0).Should().BeTrue();
    }

    #endregion

    #region Treatment Statistics Tests

    [Fact]
    public void CalculateTreatmentSummary_WithValidTreatments_ShouldReturnSummary()
    {
        // Arrange
        var treatments = new[]
        {
            new Treatment
            {
                EventType = "Meal Bolus",
                Insulin = 5.0,
                Carbs = 45,
            },
            new Treatment { EventType = "Correction Bolus", Insulin = 2.0 },
            new Treatment { EventType = "Temp Basal", Insulin = 1.5 },
            new Treatment
            {
                Carbs = 15,
                Protein = 10,
                Fat = 5,
            },
        };

        // Act
        var result = _statisticsService.CalculateTreatmentSummary(treatments);

        // Assert
        result.Should().NotBeNull();
        result.TreatmentCount.Should().Be(4);
        result.Totals.Insulin.Bolus.Should().Be(7.0);
        result.Totals.Insulin.Basal.Should().Be(1.5);
        result.Totals.Food.Carbs.Should().Be(60);
        result.Totals.Food.Protein.Should().Be(10);
        result.Totals.Food.Fat.Should().Be(5);
    }

    [Fact]
    public void IsBolusTreatment_WithBolusEventType_ShouldReturnTrue()
    {
        // Arrange
        var bolusTreatments = new[]
        {
            new Treatment { EventType = "Meal Bolus" },
            new Treatment { EventType = "Correction Bolus" },
            new Treatment { EventType = "Snack Bolus" },
            new Treatment { EventType = "Bolus Wizard" },
            new Treatment { EventType = "Combo Bolus" },
        };

        // Act & Assert
        foreach (var treatment in bolusTreatments)
        {
            _statisticsService.IsBolusTreatment(treatment).Should().BeTrue();
        }
    }

    [Fact]
    public void IsBolusTreatment_WithNonBolusEventType_ShouldReturnFalse()
    {
        // Arrange
        var nonBolusTreatments = new[]
        {
            new Treatment { EventType = "Temp Basal" },
            new Treatment { EventType = "BG Check" },
            new Treatment { EventType = "Carb Correction" },
            new Treatment { EventType = "Note" },
        };

        // Act & Assert
        foreach (var treatment in nonBolusTreatments)
        {
            _statisticsService.IsBolusTreatment(treatment).Should().BeFalse();
        }
    }

    [Fact]
    public void GetTotalInsulin_WithValidSummary_ShouldReturnSum()
    {
        // Arrange
        var summary = new TreatmentSummary
        {
            Totals = new TreatmentTotals
            {
                Insulin = new InsulinTotals { Bolus = 10.0, Basal = 5.0 },
            },
        };

        // Act
        var result = _statisticsService.GetTotalInsulin(summary);

        // Assert
        result.Should().Be(15.0);
    }

    [Fact]
    public void GetBolusPercentage_WithValidSummary_ShouldReturnCorrectPercentage()
    {
        // Arrange
        var summary = new TreatmentSummary
        {
            Totals = new TreatmentTotals
            {
                Insulin = new InsulinTotals { Bolus = 8.0, Basal = 2.0 },
            },
        };

        // Act
        var result = _statisticsService.GetBolusPercentage(summary);

        // Assert
        result.Should().Be(80.0);
    }

    #endregion

    #region Formatting Tests

    [Fact]
    public void FormatInsulinDisplay_WithVariousValues_ShouldFormatCorrectly()
    {
        // Arrange & Act & Assert
        _statisticsService.FormatInsulinDisplay(0).Should().Be("0");
        _statisticsService.FormatInsulinDisplay(0.05).Should().Be(".05");
        _statisticsService.FormatInsulinDisplay(0.5).Should().Be(".50");
        _statisticsService.FormatInsulinDisplay(1.0).Should().Be("1.00");
        _statisticsService.FormatInsulinDisplay(5.25).Should().Be("5.25");
    }

    [Fact]
    public void FormatCarbDisplay_WithVariousValues_ShouldFormatCorrectly()
    {
        // Arrange & Act & Assert
        _statisticsService.FormatCarbDisplay(0).Should().Be("0");
        _statisticsService.FormatCarbDisplay(0.5).Should().Be(".5");
        _statisticsService.FormatCarbDisplay(1.0).Should().Be("1.0");
        _statisticsService.FormatCarbDisplay(15.5).Should().Be("15.5");
    }

    [Fact]
    public void FormatPercentageDisplay_WithValidValue_ShouldFormatToOneDecimal()
    {
        // Arrange & Act & Assert
        _statisticsService.FormatPercentageDisplay(50.12345).Should().Be("50.1");
        _statisticsService.FormatPercentageDisplay(100.0).Should().Be("100.0");
    }

    [Fact]
    public void RoundInsulinToPumpPrecision_WithVariousValues_ShouldRoundCorrectly()
    {
        // Arrange & Act & Assert
        _statisticsService.RoundInsulinToPumpPrecision(0.03).Should().Be(0.05);
        _statisticsService.RoundInsulinToPumpPrecision(0.07).Should().Be(0.05);
        _statisticsService.RoundInsulinToPumpPrecision(0.08).Should().Be(0.10);
        _statisticsService.RoundInsulinToPumpPrecision(1.23).Should().Be(1.25);
    }

    #endregion

    #region Unit Conversion Tests

    [Fact]
    public void MgdlToMMOL_WithValidValues_ShouldConvertCorrectly()
    {
        // Arrange & Act & Assert
        _statisticsService.MgdlToMMOL(99).Should().BeApproximately(5.5, 0.1);
        _statisticsService.MgdlToMMOL(180).Should().BeApproximately(10.0, 0.1);
    }

    [Fact]
    public void MmolToMGDL_WithValidValues_ShouldConvertCorrectly()
    {
        // Arrange & Act & Assert
        _statisticsService.MmolToMGDL(5.5).Should().BeApproximately(99, 1);
        _statisticsService.MmolToMGDL(10.0).Should().BeApproximately(180, 1);
    }

    [Fact]
    public void MgdlToMMOLString_WithValidValue_ShouldReturnFormattedString()
    {
        // Arrange & Act & Assert
        _statisticsService.MgdlToMMOLString(99).Should().Be("5.5");
        _statisticsService.MgdlToMMOLString(180).Should().Be("10.0");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void ValidateTreatmentData_WithValidTreatment_ShouldReturnTrue()
    {
        // Arrange
        var validTreatment = new Treatment
        {
            Id = "test123",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Insulin = 5.0,
            Carbs = 45,
        };

        // Act
        var result = _statisticsService.ValidateTreatmentData(validTreatment);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void ValidateTreatmentData_WithInvalidTreatment_ShouldReturnFalse()
    {
        // Arrange
        var invalidTreatment = new Treatment
        {
            Id = "", // Invalid empty ID
            Timestamp = null, // Invalid null timestamp
        };

        // Act
        var result = _statisticsService.ValidateTreatmentData(invalidTreatment);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void ValidateTreatmentData_WithNegativeValues_ShouldReturnFalse()
    {
        // Arrange
        var invalidTreatment = new Treatment
        {
            Id = "test123",
            Timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            Insulin = -5.0, // Invalid negative insulin
        };

        // Act
        var result = _statisticsService.ValidateTreatmentData(invalidTreatment);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void CleanTreatmentData_WithMixedTreatments_ShouldFilterValidOnes()
    {
        // Arrange
        var treatments = new[]
        {
            new Treatment
            {
                Id = "valid1",
                Timestamp = 1000,
                Insulin = 5.0,
            },
            new Treatment
            {
                Id = "",
                Timestamp = 2000,
                Insulin = 3.0,
            }, // Invalid ID
            new Treatment
            {
                Id = "valid2",
                Timestamp = 3000,
                Carbs = 15,
            },
            new Treatment
            {
                Id = "invalid",
                Timestamp = null,
                Insulin = 2.0,
            }, // Invalid timestamp
        };

        // Act
        var result = _statisticsService.CleanTreatmentData(treatments).ToList();

        // Assert
        result.Should().HaveCount(2);
        result.Should().Contain(t => t.Id == "valid1");
        result.Should().Contain(t => t.Id == "valid2");
    }

    #endregion

    #region Comprehensive Analytics Tests

    [Fact]
    public void AnalyzeGlucoseData_WithValidData_ShouldReturnCompleteAnalytics()
    {
        // Arrange
        var entries = Enumerable
            .Range(0, 100)
            .Select(i => new Entry
            {
                Sgv = 100 + (i % 50 - 25), // Glucose values ranging from 75-125
                Mills = DateTimeOffset.UtcNow.AddMinutes(i * 5).ToUnixTimeMilliseconds(),
            });

        var treatments = new[]
        {
            new Treatment
            {
                EventType = "Meal Bolus",
                Insulin = 5.0,
                Carbs = 45,
            },
        };

        // Act
        var result = _statisticsService.AnalyzeGlucoseData(entries, treatments);

        // Assert
        result.Should().NotBeNull();
        result.BasicStats.Should().NotBeNull();
        result.BasicStats.Count.Should().BeGreaterThan(0);
        result.TimeInRange.Should().NotBeNull();
        result.GlycemicVariability.Should().NotBeNull();
        result.DataQuality.Should().NotBeNull();
        result.Time.Should().NotBeNull();
        result.Time.TimeOfAnalysis.Should().BeGreaterThan(0);
    }

    [Fact]
    public void AnalyzeGlucoseData_WithEmptyData_ShouldReturnEmptyAnalytics()
    {
        // Arrange
        var entries = new Entry[] { };
        var treatments = new Treatment[] { };

        // Act
        var result = _statisticsService.AnalyzeGlucoseData(entries, treatments);

        // Assert
        result.Should().NotBeNull();
        result.BasicStats.Count.Should().Be(0);
        result.TimeInRange.Percentages.Target.Should().Be(0);
    }

    #endregion
}
