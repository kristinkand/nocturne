using System.Linq.Expressions;

namespace Nocturne.Infrastructure.Data.Tests.QueryParsing;

/// <summary>
/// Tests for MongoDB-style query parsing and translation to Entity Framework expressions
/// </summary>
[Trait("Category", "Unit")]
[Trait("Category", "QueryParser")]
[Trait("Category", "MongoDB")]
public class QueryParserTests
{
    #region Basic Query Parsing Tests

    [Theory]
    [InlineData("{\"type\":\"sgv\"}", "type", "sgv")]
    [InlineData("{\"eventType\":\"Meal Bolus\"}", "eventType", "Meal Bolus")]
    [InlineData("{\"device\":\"dexcom\"}", "device", "dexcom")]
    public void ParseSimpleEqualityQuery_ShouldExtractFieldAndValue_WhenValidQueryProvided(
        string mongoQuery,
        string expectedField,
        string expectedValue
    )
    {
        // Arrange & Act
        var result = QueryParser.ParseSimpleQuery(mongoQuery);

        // Assert
        result.Should().NotBeNull();
        result.Should().ContainKey(expectedField);
        result[expectedField].Should().Be(expectedValue);
    }

    [Fact]
    public void ParseSimpleEqualityQuery_ShouldReturnEmpty_WhenInvalidJsonProvided()
    {
        // Arrange
        var invalidJson = "not-valid-json";

        // Act
        var result = QueryParser.ParseSimpleQuery(invalidJson);

        // Assert
        result.Should().BeEmpty();
    }

    [Fact]
    public void ParseSimpleEqualityQuery_ShouldHandleMultipleFields_WhenComplexQueryProvided()
    {
        // Arrange
        var mongoQuery = "{\"type\":\"sgv\",\"device\":\"dexcom\",\"mgdl\":{\"$gte\":100}}";

        // Act
        var result = QueryParser.ParseSimpleQuery(mongoQuery);

        // Assert
        result.Should().HaveCount(3);
        result["type"].Should().Be("sgv");
        result["device"].Should().Be("dexcom");
        result.Should().ContainKey("mgdl"); // Complex operators are preserved as raw JSON
    }

    #endregion

    #region Range Query Tests

    [Theory]
    [InlineData("{\"mgdl\":{\"$gte\":100}}", "mgdl", 100.0, null, true, false)]
    [InlineData("{\"sgv\":{\"$lte\":180}}", "sgv", null, 180.0, false, true)]
    [InlineData("{\"insulin\":{\"$gt\":2.0}}", "insulin", 2.0, null, false, false)]
    [InlineData("{\"carbs\":{\"$lt\":50.0}}", "carbs", null, 50.0, false, false)]
    public void ParseRangeQuery_ShouldExtractRangeConditions_WhenValidRangeQueryProvided(
        string mongoQuery,
        string fieldName,
        double? minValue,
        double? maxValue,
        bool minInclusive,
        bool maxInclusive
    )
    {
        // Act
        var result = QueryParser.ParseRangeQuery(mongoQuery, fieldName);

        // Assert
        result.Should().NotBeNull();
        result.FieldName.Should().Be(fieldName);
        result.MinValue.Should().Be(minValue);
        result.MaxValue.Should().Be(maxValue);
        result.MinInclusive.Should().Be(minInclusive);
        result.MaxInclusive.Should().Be(maxInclusive);
    }

    [Fact]
    public void ParseRangeQuery_ShouldHandleComplexRange_WhenBothMinAndMaxProvided()
    {
        // Arrange
        var mongoQuery = "{\"mills\":{\"$gte\":1609459200000,\"$lte\":1609545600000}}";

        // Act
        var result = QueryParser.ParseRangeQuery(mongoQuery, "mills");

        // Assert
        result.Should().NotBeNull();
        result.FieldName.Should().Be("mills");
        result.MinValue.Should().Be(1609459200000);
        result.MaxValue.Should().Be(1609545600000);
        result.MinInclusive.Should().BeTrue();
        result.MaxInclusive.Should().BeTrue();
    }

    [Fact]
    public void ParseRangeQuery_ShouldReturnNull_WhenFieldNotFoundInQuery()
    {
        // Arrange
        var mongoQuery = "{\"type\":\"sgv\"}";

        // Act
        var result = QueryParser.ParseRangeQuery(mongoQuery, "mills");

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region Date Range Query Tests

    [Fact]
    public void ParseDateRangeQuery_ShouldExtractDateConditions_WhenValidDateRangeProvided()
    {
        // Arrange
        var startDate = DateTimeOffset.UtcNow.AddDays(-7);
        var endDate = DateTimeOffset.UtcNow;
        var mongoQuery =
            $"{{\"mills\":{{\"$gte\":{startDate.ToUnixTimeMilliseconds()},\"$lte\":{endDate.ToUnixTimeMilliseconds()}}}}}";

        // Act
        var result = QueryParser.ParseDateRangeQuery(mongoQuery);

        // Assert
        result.Should().NotBeNull();
        result.StartMills.Should().Be(startDate.ToUnixTimeMilliseconds());
        result.EndMills.Should().Be(endDate.ToUnixTimeMilliseconds());
    }

    [Fact]
    public void ParseDateRangeQuery_ShouldHandleStringDates_WhenISO8601DatesProvided()
    {
        // Arrange
        var testDate = new DateTimeOffset(2024, 1, 1, 0, 0, 0, TimeSpan.Zero);
        var mongoQuery = "{\"dateString\":{\"$gte\":\"2024-01-01T00:00:00.000Z\"}}";

        // Act
        var result = QueryParser.ParseDateRangeQuery(mongoQuery);

        // Assert
        result.Should().NotBeNull();
        result.StartMills.Should().Be(testDate.ToUnixTimeMilliseconds());
        result.EndMills.Should().BeNull();
    }

    #endregion

    #region Array and $in Query Tests

    [Fact]
    public void ParseInQuery_ShouldExtractArrayValues_WhenInQueryProvided()
    {
        // Arrange
        var mongoQuery = "{\"type\":{\"$in\":[\"sgv\",\"mbg\",\"cal\"]}}";

        // Act
        var result = QueryParser.ParseInQuery(mongoQuery, "type");

        // Assert
        result.Should().NotBeNull();
        result.FieldName.Should().Be("type");
        result.Values.Should().BeEquivalentTo(new[] { "sgv", "mbg", "cal" });
    }

    [Fact]
    public void ParseInQuery_ShouldHandleNumericValues_WhenNumericInQueryProvided()
    {
        // Arrange
        var mongoQuery = "{\"noise\":{\"$in\":[1,2,3]}}";

        // Act
        var result = QueryParser.ParseInQuery(mongoQuery, "noise");

        // Assert
        result.Should().NotBeNull();
        result.FieldName.Should().Be("noise");
        result.Values.Should().BeEquivalentTo(new object[] { 1, 2, 3 });
    }

    #endregion

    #region Logical Operators Tests

    [Fact]
    public void ParseLogicalQuery_ShouldExtractAndConditions_WhenAndQueryProvided()
    {
        // Arrange
        var mongoQuery = "{\"$and\":[{\"type\":\"sgv\"},{\"mgdl\":{\"$gte\":100}}]}";

        // Act
        var result = QueryParser.ParseLogicalQuery(mongoQuery);

        // Assert
        result.Should().NotBeNull();
        result.Operator.Should().Be("$and");
        result.Conditions.Should().HaveCount(2);
    }

    [Fact]
    public void ParseLogicalQuery_ShouldExtractOrConditions_WhenOrQueryProvided()
    {
        // Arrange
        var mongoQuery = "{\"$or\":[{\"type\":\"sgv\"},{\"type\":\"mbg\"}]}";

        // Act
        var result = QueryParser.ParseLogicalQuery(mongoQuery);

        // Assert
        result.Should().NotBeNull();
        result.Operator.Should().Be("$or");
        result.Conditions.Should().HaveCount(2);
    }

    #endregion

    #region Complex Query Integration Tests

    [Fact]
    public void ParseComplexQuery_ShouldHandleNestedConditions_WhenComplexQueryProvided()
    {
        // Arrange - Real-world Nightscout query example
        var mongoQuery =
            @"{
            ""$and"": [
                {""type"": ""sgv""},
                {""mills"": {""$gte"": 1609459200000}},
                {""$or"": [
                    {""mgdl"": {""$gte"": 80, ""$lte"": 180}},
                    {""device"": ""dexcom""}
                ]}
            ]
        }";

        // Act
        var result = QueryParser.ParseComplexQuery(mongoQuery);

        // Assert
        result.Should().NotBeNull();
        result.HasLogicalOperators.Should().BeTrue();
        result.HasRangeQueries.Should().BeTrue();
        result.SimpleConditions.Should().NotBeEmpty();
    }

    [Fact]
    public void ParseComplexQuery_ShouldHandleTimeRangeQuery_WhenDateRangeProvided()
    {
        // Arrange - Common time-based query
        var startTime = DateTimeOffset.UtcNow.AddHours(-24);
        var endTime = DateTimeOffset.UtcNow;
        var mongoQuery =
            $@"{{
            ""type"": ""sgv"",
            ""mills"": {{
                ""$gte"": {startTime.ToUnixTimeMilliseconds()},
                ""$lte"": {endTime.ToUnixTimeMilliseconds()}
            }}
        }}";

        // Act
        var result = QueryParser.ParseComplexQuery(mongoQuery);

        // Assert
        result.Should().NotBeNull();
        result.DateRange.Should().NotBeNull();
        result.DateRange!.StartMills.Should().Be(startTime.ToUnixTimeMilliseconds());
        result.DateRange.EndMills.Should().Be(endTime.ToUnixTimeMilliseconds());
        result.SimpleConditions["type"].Should().Be("sgv");
    }

    #endregion

    #region Edge Cases and Error Handling Tests

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    [InlineData("null")]
    [InlineData("undefined")]
    public void ParseQuery_ShouldReturnEmpty_WhenInvalidOrEmptyQueryProvided(string invalidQuery)
    {
        // Act
        var simpleResult = QueryParser.ParseSimpleQuery(invalidQuery);
        var complexResult = QueryParser.ParseComplexQuery(invalidQuery);

        // Assert
        simpleResult.Should().BeEmpty();
        complexResult.Should().NotBeNull();
        complexResult.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ParseQuery_ShouldHandleMalformedJson_Gracefully()
    {
        // Arrange
        var malformedJson = "{\"type\":\"sgv\",\"mgdl\":}"; // Missing value

        // Act & Assert - Should not throw
        var simpleResult = QueryParser.ParseSimpleQuery(malformedJson);
        var complexResult = QueryParser.ParseComplexQuery(malformedJson);

        simpleResult.Should().BeEmpty();
        complexResult.IsEmpty.Should().BeTrue();
    }

    [Fact]
    public void ParseQuery_ShouldHandleUnsupportedOperators_Gracefully()
    {
        // Arrange - Query with operators not yet implemented
        var unsupportedQuery =
            @"{
            ""type"": ""sgv"",
            ""mgdl"": {""$regex"": ""^1[0-9]{2}$""},
            ""meta"": {""$exists"": true}
        }";

        // Act
        var result = QueryParser.ParseComplexQuery(unsupportedQuery);

        // Assert
        result.Should().NotBeNull();
        result.SimpleConditions["type"].Should().Be("sgv");
        result.UnsupportedOperators.Should().Contain("$regex");
        result.UnsupportedOperators.Should().Contain("$exists");
    }

    #endregion
}

/// <summary>
/// Static helper class for parsing MongoDB-style queries
/// This would be implemented in the actual infrastructure layer
/// </summary>
public static class QueryParser
{
    public static Dictionary<string, object> ParseSimpleQuery(string mongoQuery)
    {
        var result = new Dictionary<string, object>();

        if (string.IsNullOrWhiteSpace(mongoQuery))
            return result;

        try
        {
            using var document = JsonDocument.Parse(mongoQuery);
            var root = document.RootElement;

            foreach (var property in root.EnumerateObject())
            {
                if (property.Value.ValueKind == JsonValueKind.String)
                {
                    result[property.Name] = property.Value.GetString() ?? "";
                }
                else if (property.Value.ValueKind == JsonValueKind.Number)
                {
                    result[property.Name] = property.Value.GetDouble();
                }
                else if (property.Value.ValueKind == JsonValueKind.Object)
                {
                    result[property.Name] = property.Value.GetRawText();
                }
            }
        }
        catch (JsonException)
        {
            // Return empty on parse error
        }

        return result;
    }

    public static RangeQuery? ParseRangeQuery(string mongoQuery, string fieldName)
    {
        try
        {
            using var document = JsonDocument.Parse(mongoQuery);
            var root = document.RootElement;

            if (
                !root.TryGetProperty(fieldName, out var fieldElement)
                || fieldElement.ValueKind != JsonValueKind.Object
            )
            {
                return null;
            }

            var rangeQuery = new RangeQuery { FieldName = fieldName };

            foreach (var property in fieldElement.EnumerateObject())
            {
                switch (property.Name)
                {
                    case "$gte":
                        rangeQuery.MinValue = property.Value.GetDouble();
                        rangeQuery.MinInclusive = true;
                        break;
                    case "$gt":
                        rangeQuery.MinValue = property.Value.GetDouble();
                        rangeQuery.MinInclusive = false;
                        break;
                    case "$lte":
                        rangeQuery.MaxValue = property.Value.GetDouble();
                        rangeQuery.MaxInclusive = true;
                        break;
                    case "$lt":
                        rangeQuery.MaxValue = property.Value.GetDouble();
                        rangeQuery.MaxInclusive = false;
                        break;
                }
            }

            return rangeQuery;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static DateRangeQuery? ParseDateRangeQuery(string mongoQuery)
    {
        try
        {
            using var document = JsonDocument.Parse(mongoQuery);
            var root = document.RootElement;

            // Look for mills field first
            if (
                root.TryGetProperty("mills", out var millsElement)
                && millsElement.ValueKind == JsonValueKind.Object
            )
            {
                return ParseDateRangeFromMills(millsElement);
            }

            // Look for dateString field
            if (
                root.TryGetProperty("dateString", out var dateElement)
                && dateElement.ValueKind == JsonValueKind.Object
            )
            {
                return ParseDateRangeFromString(dateElement);
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private static DateRangeQuery ParseDateRangeFromMills(JsonElement millsElement)
    {
        var dateRange = new DateRangeQuery();

        foreach (var property in millsElement.EnumerateObject())
        {
            switch (property.Name)
            {
                case "$gte":
                    dateRange.StartMills = property.Value.GetInt64();
                    break;
                case "$lte":
                    dateRange.EndMills = property.Value.GetInt64();
                    break;
            }
        }

        return dateRange;
    }

    private static DateRangeQuery ParseDateRangeFromString(JsonElement dateElement)
    {
        var dateRange = new DateRangeQuery();

        foreach (var property in dateElement.EnumerateObject())
        {
            switch (property.Name)
            {
                case "$gte":
                    if (DateTime.TryParse(property.Value.GetString(), out var startDate))
                    {
                        dateRange.StartMills = ((DateTimeOffset)startDate).ToUnixTimeMilliseconds();
                    }
                    break;
                case "$lte":
                    if (DateTime.TryParse(property.Value.GetString(), out var endDate))
                    {
                        dateRange.EndMills = ((DateTimeOffset)endDate).ToUnixTimeMilliseconds();
                    }
                    break;
            }
        }

        return dateRange;
    }

    public static InQuery? ParseInQuery(string mongoQuery, string fieldName)
    {
        try
        {
            using var document = JsonDocument.Parse(mongoQuery);
            var root = document.RootElement;

            if (
                !root.TryGetProperty(fieldName, out var fieldElement)
                || fieldElement.ValueKind != JsonValueKind.Object
            )
            {
                return null;
            }

            if (
                !fieldElement.TryGetProperty("$in", out var inElement)
                || inElement.ValueKind != JsonValueKind.Array
            )
            {
                return null;
            }

            var values = new List<object>();
            foreach (var item in inElement.EnumerateArray())
            {
                switch (item.ValueKind)
                {
                    case JsonValueKind.String:
                        values.Add(item.GetString() ?? "");
                        break;
                    case JsonValueKind.Number:
                        values.Add(item.GetDouble());
                        break;
                }
            }

            return new InQuery { FieldName = fieldName, Values = values };
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static LogicalQuery? ParseLogicalQuery(string mongoQuery)
    {
        try
        {
            using var document = JsonDocument.Parse(mongoQuery);
            var root = document.RootElement;

            foreach (var property in root.EnumerateObject())
            {
                if (property.Name == "$and" || property.Name == "$or")
                {
                    if (property.Value.ValueKind == JsonValueKind.Array)
                    {
                        var conditions = new List<string>();
                        foreach (var condition in property.Value.EnumerateArray())
                        {
                            conditions.Add(condition.GetRawText());
                        }

                        return new LogicalQuery
                        {
                            Operator = property.Name,
                            Conditions = conditions,
                        };
                    }
                }
            }

            return null;
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static ComplexQuery ParseComplexQuery(string mongoQuery)
    {
        if (string.IsNullOrWhiteSpace(mongoQuery))
        {
            return new ComplexQuery { IsEmpty = true };
        }

        try
        {
            var complexQuery = new ComplexQuery();

            // Parse simple conditions
            complexQuery.SimpleConditions = ParseSimpleQuery(mongoQuery);

            // Parse date range
            complexQuery.DateRange = ParseDateRangeQuery(mongoQuery);

            // Parse logical operators
            complexQuery.LogicalQuery = ParseLogicalQuery(mongoQuery);

            // Detect unsupported operators
            complexQuery.UnsupportedOperators = DetectUnsupportedOperators(mongoQuery);

            return complexQuery;
        }
        catch (JsonException)
        {
            return new ComplexQuery { IsEmpty = true };
        }
    }

    private static List<string> DetectUnsupportedOperators(string mongoQuery)
    {
        var unsupported = new List<string>();
        var knownUnsupported = new[] { "$regex", "$exists", "$size", "$elemMatch", "$type" };

        foreach (var op in knownUnsupported)
        {
            if (mongoQuery.Contains(op))
            {
                unsupported.Add(op);
            }
        }

        return unsupported;
    }
}

// Supporting classes for query parsing
public class RangeQuery
{
    public string FieldName { get; set; } = "";
    public double? MinValue { get; set; }
    public double? MaxValue { get; set; }
    public bool MinInclusive { get; set; }
    public bool MaxInclusive { get; set; }
}

public class DateRangeQuery
{
    public long? StartMills { get; set; }
    public long? EndMills { get; set; }
}

public class InQuery
{
    public string FieldName { get; set; } = "";
    public List<object> Values { get; set; } = new();
}

public class LogicalQuery
{
    public string Operator { get; set; } = "";
    public List<string> Conditions { get; set; } = new();
}

public class ComplexQuery
{
    public Dictionary<string, object> SimpleConditions { get; set; } = new();
    public DateRangeQuery? DateRange { get; set; }
    public LogicalQuery? LogicalQuery { get; set; }
    public List<string> UnsupportedOperators { get; set; } = new();
    public bool IsEmpty { get; set; }
    public bool HasLogicalOperators => LogicalQuery != null;
    public bool HasRangeQueries => DateRange != null;
}
