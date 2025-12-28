using Nocturne.API.Services;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services;

public class CannulaAgeServiceTests
{
    [Fact]
    public async Task GetCannulaAgeAsync_WithMatchingInstance_ReturnsDeviceAgeInfo()
    {
        var instance = LegacyDeviceAgeTestHelper.CreateInstance(
            ["Site Change"],
            ageHours: 50.25,
            notes: "Changed cannula"
        );
        var service = LegacyDeviceAgeTestHelper.CreateService(instance);
        var preferences = new DeviceAgePreferences
        {
            Info = 44,
            Warn = 48,
            Urgent = 72,
            Display = "days",
            EnableAlerts = false
        };

        var result = await service.GetCannulaAgeAsync("user-1", preferences);

        Assert.True(result.Found);
        Assert.Equal(50, result.Age);
        Assert.Equal(2, result.Days);
        Assert.Equal(2, result.Hours);
        Assert.Equal("2d2h", result.Display);
        Assert.Equal("Changed cannula", result.Notes);
    }
}
