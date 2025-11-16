namespace Nocturne.API.Tests.Integration.Infrastructure;

/// <summary>
/// Provides utilities for test data isolation to prevent conflicts between tests
/// </summary>
public static class TestIsolationUtilities
{
    /// <summary>
    /// Generates a unique test run identifier
    /// </summary>
    /// <returns>Unique identifier for this test run</returns>
    public static string GenerateTestRunId()
    {
        return Guid.NewGuid().ToString()[..8];
    }

    /// <summary>
    /// Creates a unique collection name for test isolation
    /// </summary>
    /// <param name="baseName">Base collection name</param>
    /// <param name="testRunId">Optional test run ID (generates if null)</param>
    /// <returns>Unique collection name</returns>
    public static string GetUniqueCollectionName(string baseName, string? testRunId = null)
    {
        var runId = testRunId ?? GenerateTestRunId();
        var timestamp = DateTimeOffset.UtcNow.Ticks;
        return $"{baseName}_{runId}_{timestamp}";
    }

    /// <summary>
    /// Creates a unique database name for test isolation
    /// </summary>
    /// <param name="baseName">Base database name</param>
    /// <param name="testRunId">Optional test run ID (generates if null)</param>
    /// <returns>Unique database name</returns>
    public static string GetUniqueDatabaseName(string baseName, string? testRunId = null)
    {
        var runId = testRunId ?? GenerateTestRunId();
        return $"{baseName}_test_{runId}";
    }

    /// <summary>
    /// Creates a unique identifier suffix for test entities
    /// </summary>
    /// <param name="prefix">Optional prefix for the identifier</param>
    /// <returns>Unique identifier suffix</returns>
    public static string GenerateUniqueEntitySuffix(string prefix = "test")
    {
        return $"{prefix}-{Guid.NewGuid().ToString()[..8]}";
    }

    /// <summary>
    /// Creates a unique device name for test isolation
    /// </summary>
    /// <param name="deviceType">Type of device (e.g., "cgm", "meter", "pump")</param>
    /// <returns>Unique device name</returns>
    public static string GenerateUniqueDeviceName(string deviceType = "device")
    {
        return $"test-{deviceType}-{Guid.NewGuid().ToString()[..8]}";
    }

    /// <summary>
    /// Validates that an ObjectId is unique (not hard-coded)
    /// </summary>
    /// <param name="objectId">ObjectId to validate</param>
    /// <returns>True if the ID appears to be dynamically generated</returns>
    public static bool IsValidTestObjectId(string objectId)
    {
        // Check for known hard-coded test IDs that should not be used
        var hardCodedIds = new[]
        {
            "507f1f77bcf86cd799439011",
            "507f1f77bcf86cd799439012",
            "507f1f77bcf86cd799439013",
            "507f1f77bcf86cd799439014",
            "507f1f77bcf86cd799439015",
            "507f1f77bcf86cd799439016", // Found in EntriesAdvancedQueryIntegrationTests
            "507f1f77bcf86cd799439099", // Found in multiple test files for "nonExistent" scenarios
            "507f1f77bcf86cd799439999", // Another common "nonExistent" ID pattern
        };

        return !hardCodedIds.Contains(objectId);
    }

    /// <summary>
    /// Creates a unique test context identifier for grouping related test data
    /// </summary>
    /// <param name="testClassName">Name of the test class</param>
    /// <param name="testMethodName">Name of the test method</param>
    /// <returns>Unique test context identifier</returns>
    public static string GenerateTestContext(string testClassName, string testMethodName)
    {
        var runId = GenerateTestRunId();
        return $"{testClassName}.{testMethodName}_{runId}";
    }

    /// <summary>
    /// Validates that test data doesn't use time-dependent values
    /// </summary>
    /// <param name="timestamp">Timestamp to validate</param>
    /// <param name="toleranceMinutes">Allowed tolerance from base test time</param>
    /// <returns>True if timestamp is within expected test time range</returns>
    public static bool IsValidTestTimestamp(DateTimeOffset timestamp, int toleranceMinutes = 1440)
    {
        var baseTime = TestTimeProvider.BaseTestTime;
        var minTime = baseTime.AddMinutes(-toleranceMinutes);
        var maxTime = baseTime.AddMinutes(toleranceMinutes);

        return timestamp >= minTime && timestamp <= maxTime;
    }
}
