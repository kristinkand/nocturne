using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Services;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services;

/// <summary>
/// Unit tests for DemoDataService
/// </summary>
public class DemoDataServiceTests
{
    private readonly Mock<IEntryService> _mockEntryService;
    private readonly Mock<ILogger<DemoDataService>> _mockLogger;

    public DemoDataServiceTests()
    {
        _mockEntryService = new Mock<IEntryService>();
        _mockLogger = new Mock<ILogger<DemoDataService>>();
    }

    private DemoDataService CreateService(DemoModeConfiguration? config = null)
    {
        var configBuilder = new ConfigurationBuilder();

        if (config != null)
        {
            configBuilder.AddInMemoryCollection(
                new Dictionary<string, string?>
                {
                    ["DemoMode:Enabled"] = config.Enabled.ToString(),
                    ["DemoMode:IntervalMinutes"] = config.IntervalMinutes.ToString(),
                    ["DemoMode:InitialGlucose"] = config.InitialGlucose.ToString(),
                    ["DemoMode:WalkVariance"] = config.WalkVariance.ToString(),
                    ["DemoMode:MinGlucose"] = config.MinGlucose.ToString(),
                    ["DemoMode:MaxGlucose"] = config.MaxGlucose.ToString(),
                    ["DemoMode:Device"] = config.Device,
                }
            );
        }

        var configuration = configBuilder.Build();

        return new DemoDataService(_mockEntryService.Object, configuration, _mockLogger.Object);
    }

    #region Configuration Tests

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WithDefaultConfiguration_ShouldUseDefaults()
    {
        // Arrange
        var expectedConfig = new DemoModeConfiguration();

        // Act
        var service = CreateService();

        // Assert
        Assert.False(service.IsEnabled);
        var config = service.GetConfiguration();
        Assert.Equal(expectedConfig.Enabled, config.Enabled);
        Assert.Equal(expectedConfig.IntervalMinutes, config.IntervalMinutes);
        Assert.Equal(expectedConfig.InitialGlucose, config.InitialGlucose);
        Assert.Equal(expectedConfig.WalkVariance, config.WalkVariance);
        Assert.Equal(expectedConfig.MinGlucose, config.MinGlucose);
        Assert.Equal(expectedConfig.MaxGlucose, config.MaxGlucose);
        Assert.Equal(expectedConfig.Device, config.Device);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void Constructor_WithCustomConfiguration_ShouldUseProvidedValues()
    {
        // Arrange
        var customConfig = new DemoModeConfiguration
        {
            Enabled = true,
            IntervalMinutes = 3,
            InitialGlucose = 140,
            WalkVariance = 15,
            MinGlucose = 60,
            MaxGlucose = 300,
            Device = "custom-demo-cgm",
        };

        // Act
        var service = CreateService(customConfig);

        // Assert
        Assert.True(service.IsEnabled);
        var config = service.GetConfiguration();
        Assert.Equal(customConfig.Enabled, config.Enabled);
        Assert.Equal(customConfig.IntervalMinutes, config.IntervalMinutes);
        Assert.Equal(customConfig.InitialGlucose, config.InitialGlucose);
        Assert.Equal(customConfig.WalkVariance, config.WalkVariance);
        Assert.Equal(customConfig.MinGlucose, config.MinGlucose);
        Assert.Equal(customConfig.MaxGlucose, config.MaxGlucose);
        Assert.Equal(customConfig.Device, config.Device);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void IsEnabled_WhenConfiguredTrue_ShouldReturnTrue()
    {
        // Arrange
        var config = new DemoModeConfiguration { Enabled = true };

        // Act
        var service = CreateService(config);

        // Assert
        Assert.True(service.IsEnabled);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void IsEnabled_WhenConfiguredFalse_ShouldReturnFalse()
    {
        // Arrange
        var config = new DemoModeConfiguration { Enabled = false };

        // Act
        var service = CreateService(config);

        // Assert
        Assert.False(service.IsEnabled);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public void GetConfiguration_ShouldReturnConfigurationObject()
    {
        // Arrange
        var expectedConfig = new DemoModeConfiguration
        {
            Enabled = true,
            IntervalMinutes = 5,
            InitialGlucose = 120,
            WalkVariance = 10,
            MinGlucose = 70,
            MaxGlucose = 250,
            Device = "demo-cgm",
        };

        // Act
        var service = CreateService(expectedConfig);

        // Assert
        var actualConfig = service.GetConfiguration();
        Assert.Equal(expectedConfig.Enabled, actualConfig.Enabled);
        Assert.Equal(expectedConfig.IntervalMinutes, actualConfig.IntervalMinutes);
        Assert.Equal(expectedConfig.InitialGlucose, actualConfig.InitialGlucose);
        Assert.Equal(expectedConfig.WalkVariance, actualConfig.WalkVariance);
        Assert.Equal(expectedConfig.MinGlucose, actualConfig.MinGlucose);
        Assert.Equal(expectedConfig.MaxGlucose, actualConfig.MaxGlucose);
        Assert.Equal(expectedConfig.Device, actualConfig.Device);
    }

    #endregion

    #region GenerateEntryAsync Tests

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateEntryAsync_WhenDemoModeDisabled_ShouldThrowInvalidOperationException()
    {
        // Arrange
        var config = new DemoModeConfiguration { Enabled = false };
        var service = CreateService(config);

        // Act & Assert
        await Assert.ThrowsAsync<InvalidOperationException>(() => service.GenerateEntryAsync());
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateEntryAsync_WhenEnabled_ShouldReturnValidEntry()
    {
        // Arrange
        var config = new DemoModeConfiguration
        {
            Enabled = true,
            InitialGlucose = 120,
            MinGlucose = 70,
            MaxGlucose = 250,
            Device = "test-device",
        };
        var service = CreateService(config);

        // Act
        var entry = await service.GenerateEntryAsync();

        // Assert
        Assert.NotNull(entry);
        Assert.Equal("sgv", entry.Type);
        Assert.Equal(config.Device, entry.Device);
        Assert.True(entry.IsDemo);
        Assert.True(entry.Mills > 0);
        Assert.NotNull(entry.Date);
        Assert.NotNull(entry.DateString);
        Assert.True(entry.Mgdl >= config.MinGlucose && entry.Mgdl <= config.MaxGlucose);
        Assert.True(entry.Sgv >= config.MinGlucose && entry.Sgv <= config.MaxGlucose);
        Assert.NotNull(entry.Direction);
        Assert.NotNull(entry.CreatedAt);
        Assert.NotNull(entry.ModifiedAt);
        Assert.True(entry.Rssi >= 0 && entry.Rssi <= 100);
        Assert.True(entry.Noise >= 0 && entry.Noise < 5);
        Assert.NotNull(entry.Filtered);
        Assert.NotNull(entry.Unfiltered);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateEntryAsync_ShouldRespectMinGlucoseBoundary()
    {
        // Arrange
        var config = new DemoModeConfiguration
        {
            Enabled = true,
            InitialGlucose = 75, // Close to min
            MinGlucose = 70,
            MaxGlucose = 250,
            WalkVariance = 20, // Large variance to test boundary
        };
        var service = CreateService(config);

        // Act - Generate multiple entries to test boundary enforcement
        for (int i = 0; i < 10; i++)
        {
            var entry = await service.GenerateEntryAsync();

            // Assert
            Assert.True(
                entry.Mgdl >= config.MinGlucose,
                $"Entry {i}: Mgdl {entry.Mgdl} is below minimum {config.MinGlucose}"
            );
            Assert.True(
                entry.Sgv >= config.MinGlucose,
                $"Entry {i}: Sgv {entry.Sgv} is below minimum {config.MinGlucose}"
            );
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateEntryAsync_ShouldRespectMaxGlucoseBoundary()
    {
        // Arrange
        var config = new DemoModeConfiguration
        {
            Enabled = true,
            InitialGlucose = 245, // Close to max
            MinGlucose = 70,
            MaxGlucose = 250,
            WalkVariance = 20, // Large variance to test boundary
        };
        var service = CreateService(config);

        // Act - Generate multiple entries to test boundary enforcement
        for (int i = 0; i < 10; i++)
        {
            var entry = await service.GenerateEntryAsync();

            // Assert
            Assert.True(
                entry.Mgdl <= config.MaxGlucose,
                $"Entry {i}: Mgdl {entry.Mgdl} is above maximum {config.MaxGlucose}"
            );
            Assert.True(
                entry.Sgv <= config.MaxGlucose,
                $"Entry {i}: Sgv {entry.Sgv} is above maximum {config.MaxGlucose}"
            );
        }
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateEntryAsync_ShouldGenerateDifferentValues()
    {
        // Arrange
        var config = new DemoModeConfiguration
        {
            Enabled = true,
            InitialGlucose = 120,
            WalkVariance = 10,
        };
        var service = CreateService(config);

        // Act - Generate multiple entries
        var entries = new List<Entry>();
        for (int i = 0; i < 5; i++)
        {
            entries.Add(await service.GenerateEntryAsync());
        }

        // Assert - Values should change between entries (with high probability)
        var glucoseValues = entries.Select(e => e.Mgdl).ToList();
        var uniqueValues = glucoseValues.Distinct().Count();

        // Allow for some repetition but expect mostly different values
        Assert.True(
            uniqueValues >= 3,
            $"Expected at least 3 unique glucose values, got {uniqueValues}"
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateEntryAsync_WithLargeVariance_ShouldStillRespectBoundaries()
    {
        // Arrange
        var config = new DemoModeConfiguration
        {
            Enabled = true,
            InitialGlucose = 120,
            MinGlucose = 70,
            MaxGlucose = 250,
            WalkVariance = 50, // Very large variance
        };
        var service = CreateService(config);

        // Act - Generate many entries to test with large variance
        for (int i = 0; i < 20; i++)
        {
            var entry = await service.GenerateEntryAsync();

            // Assert
            Assert.True(
                entry.Mgdl >= config.MinGlucose && entry.Mgdl <= config.MaxGlucose,
                $"Entry {i}: Glucose {entry.Mgdl} is outside bounds [{config.MinGlucose}, {config.MaxGlucose}]"
            );
        }
    }

    #endregion

    #region Direction Calculation Tests

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateEntryAsync_ShouldProduceValidDirections()
    {
        // Arrange
        var config = new DemoModeConfiguration
        {
            Enabled = true,
            InitialGlucose = 120,
            WalkVariance = 15,
        };
        var service = CreateService(config);
        var validDirections = new[]
        {
            "DoubleUp",
            "SingleUp",
            "FortyFiveUp",
            "Flat",
            "FortyFiveDown",
            "SingleDown",
            "DoubleDown",
        };

        // Act - Generate multiple entries to test direction variety
        var directions = new List<string>();
        for (int i = 0; i < 20; i++)
        {
            var entry = await service.GenerateEntryAsync();
            directions.Add(entry.Direction!);
        }

        // Assert
        Assert.All(directions, direction => Assert.Contains(direction, validDirections));

        // Should produce some variety in directions (not all the same)
        var uniqueDirections = directions.Distinct().Count();
        Assert.True(
            uniqueDirections >= 2,
            $"Expected at least 2 different directions, got {uniqueDirections}"
        );
    }

    #endregion

    #region Thread Safety Tests

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateEntryAsync_ConcurrentCalls_ShouldBeThreadSafe()
    {
        // Arrange
        var config = new DemoModeConfiguration
        {
            Enabled = true,
            InitialGlucose = 120,
            WalkVariance = 10,
        };
        var service = CreateService(config);
        const int concurrentCalls = 10;

        // Act - Make concurrent calls
        var tasks = Enumerable
            .Range(0, concurrentCalls)
            .Select(_ => service.GenerateEntryAsync())
            .ToArray();

        var entries = await Task.WhenAll(tasks);

        // Assert
        Assert.Equal(concurrentCalls, entries.Length);
        Assert.All(
            entries,
            entry =>
            {
                Assert.NotNull(entry);
                Assert.True(entry.Mgdl >= config.MinGlucose && entry.Mgdl <= config.MaxGlucose);
                Assert.True(entry.IsDemo);
            }
        );

        // All entries should have valid timestamps
        Assert.All(entries, entry => Assert.True(entry.Mills > 0));
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateEntryAsync_ConcurrentCallsWithLargeVariance_ShouldMaintainBoundaries()
    {
        // Arrange
        var config = new DemoModeConfiguration
        {
            Enabled = true,
            InitialGlucose = 120,
            MinGlucose = 50,
            MaxGlucose = 300,
            WalkVariance = 30, // Large variance to stress test
        };
        var service = CreateService(config);
        const int concurrentCalls = 20;

        // Act - Make many concurrent calls
        var tasks = Enumerable
            .Range(0, concurrentCalls)
            .Select(_ => service.GenerateEntryAsync())
            .ToArray();

        var entries = await Task.WhenAll(tasks);

        // Assert
        Assert.All(
            entries,
            entry =>
            {
                Assert.True(
                    entry.Mgdl >= config.MinGlucose,
                    $"Glucose {entry.Mgdl} is below minimum {config.MinGlucose}"
                );
                Assert.True(
                    entry.Mgdl <= config.MaxGlucose,
                    $"Glucose {entry.Mgdl} is above maximum {config.MaxGlucose}"
                );
            }
        );
    }

    #endregion

    #region Edge Cases

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateEntryAsync_WithZeroVariance_ShouldProduceStableValues()
    {
        // Arrange
        var config = new DemoModeConfiguration
        {
            Enabled = true,
            InitialGlucose = 120,
            WalkVariance = 0, // No variance
        };
        var service = CreateService(config);

        // Act
        var entry1 = await service.GenerateEntryAsync();
        var entry2 = await service.GenerateEntryAsync();

        // Assert - Values should be very close (random walk with 0 variance should have minimal change)
        Assert.True(
            Math.Abs(entry1.Mgdl - entry2.Mgdl) <= 1,
            $"Expected minimal change with zero variance, got {entry1.Mgdl} and {entry2.Mgdl}"
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateEntryAsync_WithExtremeConfiguration_ShouldHandleGracefully()
    {
        // Arrange
        var config = new DemoModeConfiguration
        {
            Enabled = true,
            InitialGlucose = 100,
            MinGlucose = 99, // Very narrow range
            MaxGlucose = 101, // Very narrow range
            WalkVariance = 50, // Large variance relative to range
        };
        var service = CreateService(config);

        // Act & Assert - Should not throw and should respect boundaries
        for (int i = 0; i < 10; i++)
        {
            var entry = await service.GenerateEntryAsync();
            Assert.True(entry.Mgdl >= config.MinGlucose && entry.Mgdl <= config.MaxGlucose);
        }
    }

    #endregion

    #region Entry Format Validation

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateEntryAsync_ShouldPopulateAllRequiredFields()
    {
        // Arrange
        var config = new DemoModeConfiguration { Enabled = true, Device = "test-cgm" };
        var service = CreateService(config);

        // Act
        var entry = await service.GenerateEntryAsync();

        // Assert
        Assert.Equal("sgv", entry.Type);
        Assert.Equal(config.Device, entry.Device);
        Assert.True(entry.Mills > 0);
        Assert.NotNull(entry.Date);
        Assert.NotNull(entry.DateString);
        Assert.True(entry.Mgdl > 0);
        Assert.True(entry.Sgv > 0);
        Assert.Equal(entry.Mgdl, entry.Sgv); // Should be the same for demo data
        Assert.NotNull(entry.Direction);
        Assert.True(entry.IsDemo);
        Assert.NotNull(entry.Filtered);
        Assert.NotNull(entry.Unfiltered);
        Assert.NotNull(entry.Rssi);
        Assert.NotNull(entry.Noise);
        Assert.NotNull(entry.CreatedAt);
        Assert.NotNull(entry.ModifiedAt);
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateEntryAsync_ShouldGenerateReasonableFilteredAndUnfilteredValues()
    {
        // Arrange
        var config = new DemoModeConfiguration { Enabled = true, InitialGlucose = 120 };
        var service = CreateService(config);

        // Act
        var entry = await service.GenerateEntryAsync();

        // Assert
        Assert.NotNull(entry.Filtered);
        Assert.NotNull(entry.Unfiltered);

        // Filtered should be close to the actual glucose value (within +/- 2)
        Assert.True(
            Math.Abs(entry.Filtered.Value - entry.Mgdl) <= 2,
            $"Filtered value {entry.Filtered} too far from glucose {entry.Mgdl}"
        );

        // Unfiltered should be close to the actual glucose value (within +/- 5)
        Assert.True(
            Math.Abs(entry.Unfiltered.Value - entry.Mgdl) <= 5,
            $"Unfiltered value {entry.Unfiltered} too far from glucose {entry.Mgdl}"
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateEntryAsync_ShouldGenerateValidRSSIAndNoise()
    {
        // Arrange
        var config = new DemoModeConfiguration { Enabled = true };
        var service = CreateService(config);

        // Act
        var entry = await service.GenerateEntryAsync();

        // Assert
        Assert.NotNull(entry.Rssi);
        Assert.True(
            entry.Rssi >= 0 && entry.Rssi <= 100,
            $"RSSI {entry.Rssi} is out of valid range [0, 100]"
        );

        Assert.NotNull(entry.Noise);
        Assert.True(
            entry.Noise >= 0 && entry.Noise < 5,
            $"Noise {entry.Noise} is out of valid range [0, 4]"
        );
    }

    [Fact]
    [Trait("Category", "Unit")]
    public async Task GenerateEntryAsync_ShouldGenerateConsistentTimestamps()
    {
        // Arrange
        var config = new DemoModeConfiguration { Enabled = true };
        var service = CreateService(config);
        var beforeGeneration = DateTime.UtcNow;

        // Act
        var entry = await service.GenerateEntryAsync();
        var afterGeneration = DateTime.UtcNow;

        // Assert
        Assert.NotNull(entry.Date);
        Assert.NotNull(entry.DateString);
        Assert.NotNull(entry.CreatedAt);
        Assert.NotNull(entry.ModifiedAt);

        // All timestamps should be within a reasonable timeframe
        Assert.True(
            entry.Date >= beforeGeneration.AddSeconds(-1)
                && entry.Date <= afterGeneration.AddSeconds(1)
        );
        Assert.True(
            entry.ModifiedAt >= beforeGeneration.AddSeconds(-1)
                && entry.ModifiedAt <= afterGeneration.AddSeconds(1)
        );

        // Mills should correspond to the Date
        var expectedMills = new DateTimeOffset(entry.Date.Value).ToUnixTimeMilliseconds();
        Assert.Equal(expectedMills, entry.Mills);
    }

    #endregion
}
