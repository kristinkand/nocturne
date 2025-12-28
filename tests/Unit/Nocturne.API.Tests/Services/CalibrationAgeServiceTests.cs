using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services;

public class InsulinAgeServiceTests
{
    [Fact]
    public async Task GetInsulinAgeAsync_WithPreferences_ReturnsDeviceAgeInfo()
    {
        var instance = LegacyDeviceAgeTestHelper.CreateInstance(
            ["Insulin Change"],
            ageHours: 10.5,
            notes: "Reservoir refilled"
        );
        var service = LegacyDeviceAgeTestHelper.CreateService(instance);
        var preferences = new DeviceAgePreferences
        {
            Info = 4,
            Warn = 8,
            Urgent = 12,
            Display = "hours",
            EnableAlerts = false
        };

        var result = await service.GetInsulinAgeAsync("user-1", preferences);

        Assert.True(result.Found);
        Assert.Equal(10, result.Age);
        Assert.Equal("10h", result.Display);
        Assert.Equal("Reservoir refilled", result.Notes);
    }
}
