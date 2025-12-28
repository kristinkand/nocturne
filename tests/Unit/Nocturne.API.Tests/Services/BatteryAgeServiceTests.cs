using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services;

public class BatteryAgeServiceTests
{
    [Fact]
    public async Task GetBatteryAgeAsync_WithAlertsEnabled_CreatesNotification()
    {
        var instance = LegacyDeviceAgeTestHelper.CreateInstance(
            ["Pump Battery Change"],
            ageHours: 336.1
        );
        var service = LegacyDeviceAgeTestHelper.CreateService(instance);
        var preferences = new DeviceAgePreferences
        {
            Info = 312,
            Warn = 336,
            Urgent = 360,
            Display = "hours",
            EnableAlerts = true
        };

        var result = await service.GetBatteryAgeAsync("user-1", preferences);

        Assert.True(result.Found);
        Assert.Equal(336, result.Age);
        Assert.NotNull(result.Notification);
        Assert.Equal("Pump battery age 336 hours", result.Notification?.Title);
        Assert.Equal("Time to change pump battery", result.Notification?.Message);
        Assert.Equal("BAGE", result.Notification?.Group);
        Assert.Equal(1, result.Notification?.Level);
    }
}
