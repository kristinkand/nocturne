using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services;

public class SensorAgeServiceTests
{
    [Fact]
    public async Task GetSensorAgeAsync_WithMoreRecentChange_HidesSensorStart()
    {
        var sensorStart = LegacyDeviceAgeTestHelper.CreateInstance(
            ["Sensor Start"],
            ageHours: 200.2
        );
        var sensorChange = LegacyDeviceAgeTestHelper.CreateInstance(
            ["Sensor Change"],
            ageHours: 100.2
        );
        var service = LegacyDeviceAgeTestHelper.CreateService(sensorStart, sensorChange);
        var preferences = new DeviceAgePreferences
        {
            Info = 144,
            Warn = 164,
            Urgent = 166,
            Display = "hours",
            EnableAlerts = false
        };

        var result = await service.GetSensorAgeAsync("user-1", preferences);

        Assert.Equal("Sensor Change", result.Min);
        Assert.True(result.SensorChange.Found);
        Assert.False(result.SensorStart.Found);
    }
}
