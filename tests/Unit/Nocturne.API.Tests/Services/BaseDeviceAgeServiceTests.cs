using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services;

public class LegacyDeviceAgeServiceThresholdTests
{
    [Fact]
    public async Task GetCannulaAgeAsync_WithDefinitionThresholds_OverridesPreferences()
    {
        var threshold = LegacyDeviceAgeTestHelper.CreateThreshold(
            NotificationUrgency.Warn,
            hours: 4
        );
        var instance = LegacyDeviceAgeTestHelper.CreateInstance(
            ["Site Change"],
            ageHours: 6.1,
            thresholds: [threshold]
        );
        var service = LegacyDeviceAgeTestHelper.CreateService(instance);
        var preferences = new DeviceAgePreferences
        {
            Info = 10,
            Warn = 20,
            Urgent = 30,
            Display = "hours",
            EnableAlerts = false
        };

        var result = await service.GetCannulaAgeAsync("user-1", preferences);

        Assert.True(result.Found);
        Assert.Equal(1, result.Level);
    }
}
