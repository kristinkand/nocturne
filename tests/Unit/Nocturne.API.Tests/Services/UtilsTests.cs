using System.Globalization;
using Xunit;

namespace Nocturne.API.Tests.Services;

/// <summary>
/// Tests for utility functions with 1:1 legacy compatibility
/// Based on legacy utils.test.js
/// </summary>
public class UtilsTests
{
    [Fact]
    public void ToFixed_ShouldFormatNumbersCorrectly()
    {
        // Arrange & Act & Assert
        Assert.Equal("5.50", Utils.ToFixed(5.499999999));
    }

    [Fact]
    public void ToRoundedStr_ShouldFormatNumbersWithVariousPrecision()
    {
        // Arrange & Act & Assert
        Assert.Equal("3.34", Utils.ToRoundedStr(3.345, 2)); // Default rounding behavior
        Assert.Equal("5", Utils.ToRoundedStr(5.499999999, 0));
        Assert.Equal("5.5", Utils.ToRoundedStr(5.499999999, 1));
        Assert.Equal("5.5", Utils.ToRoundedStr(5.499999999, 3));
        Assert.Equal("100", Utils.ToRoundedStr(123.45, -2));
        Assert.Equal("0", Utils.ToRoundedStr(-0.001, 2));
        Assert.Equal("-2.5", Utils.ToRoundedStr(-2.47, 1));
        Assert.Equal("-2.4", Utils.ToRoundedStr(-2.44, 1));
    }

    [Fact]
    public void ToRoundedStr_ShouldHandleNullAndInvalidValues()
    {
        // Arrange & Act & Assert
        Assert.Equal("0", Utils.ToRoundedStr(null, 2));
        Assert.Equal("0", Utils.ToRoundedStr("text", 2));
    }

    [Fact]
    public void MergeInputTime_ShouldMergeDateAndTime()
    {
        // Arrange & Act
        var result = Utils.MergeInputTime("22:35", "2015-07-14");

        // Assert
        Assert.Equal(22, result.Hour);
        Assert.Equal(35, result.Minute);
        Assert.Equal(2015, result.Year);
        Assert.Equal(7, result.Month);
        Assert.Equal(14, result.Day);
    }
}

/// <summary>
/// Utility functions for 1:1 legacy compatibility
/// </summary>
public static class Utils
{
    /// <summary>
    /// Format number to fixed decimal places (2 by default)
    /// </summary>
    public static string ToFixed(double value, int decimals = 2)
    {
        return value.ToString($"F{decimals}", CultureInfo.InvariantCulture);
    }

    /// <summary>
    /// Round number to string with specified precision, handling edge cases
    /// </summary>
    public static string ToRoundedStr(object? value, int precision)
    {
        if (value == null || !double.TryParse(value.ToString(), out var numValue))
        {
            return "0";
        }

        if (precision < 0)
        {
            // Negative precision rounds to tens, hundreds, etc.
            var factor = Math.Pow(10, Math.Abs(precision));
            numValue = Math.Round(numValue / factor) * factor;
            return numValue.ToString("F0", CultureInfo.InvariantCulture);
        }

        // Handle very small numbers
        if (Math.Abs(numValue) < Math.Pow(10, -precision) / 2)
        {
            return "0";
        }

        var rounded = Math.Round(numValue, precision);

        // Remove trailing zeros for display
        var formatted = rounded.ToString($"F{precision}", CultureInfo.InvariantCulture);
        if (precision > 0 && formatted.Contains('.'))
        {
            formatted = formatted.TrimEnd('0').TrimEnd('.');
        }

        return formatted;
    }

    /// <summary>
    /// Merge time string (HH:mm) with date string (yyyy-MM-dd)
    /// </summary>
    public static DateTime MergeInputTime(string timeStr, string dateStr)
    {
        if (!TimeSpan.TryParse(timeStr, out var time))
        {
            throw new ArgumentException("Invalid time format", nameof(timeStr));
        }

        if (!DateTime.TryParse(dateStr, out var date))
        {
            throw new ArgumentException("Invalid date format", nameof(dateStr));
        }

        return date.Date.Add(time);
    }
}
