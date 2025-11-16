using Microsoft.AspNetCore.Mvc;
using Moq;
using Nocturne.API.Controllers.V1;
using Nocturne.API.Services;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Abstractions;
using Xunit;

namespace Nocturne.API.Tests.Controllers;

public class DeviceAgeControllerTests
{
    private readonly Mock<ICannulaAgeService> _cannulaAgeServiceMock;
    private readonly Mock<ISensorAgeService> _sensorAgeServiceMock;
    private readonly Mock<IBatteryAgeService> _batteryAgeServiceMock;
    private readonly Mock<ICalibrationAgeService> _calibrationAgeServiceMock;
    private readonly Mock<IPostgreSqlService> _postgreSqlServiceMock;
    private readonly DeviceAgeController _controller;

    public DeviceAgeControllerTests()
    {
        _cannulaAgeServiceMock = new Mock<ICannulaAgeService>();
        _sensorAgeServiceMock = new Mock<ISensorAgeService>();
        _batteryAgeServiceMock = new Mock<IBatteryAgeService>();
        _calibrationAgeServiceMock = new Mock<ICalibrationAgeService>();
        _postgreSqlServiceMock = new Mock<IPostgreSqlService>();

        _controller = new DeviceAgeController(
            _cannulaAgeServiceMock.Object,
            _sensorAgeServiceMock.Object,
            _batteryAgeServiceMock.Object,
            _calibrationAgeServiceMock.Object,
            _postgreSqlServiceMock.Object
        );
    }

    [Fact]
    public async Task GetCannulaAge_WithDefaultParameters_ReturnsOkResult()
    {
        // Arrange
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Site Change",
                Mills = DateTimeOffset.UtcNow.AddHours(-10).ToUnixTimeMilliseconds(),
            },
        };
        var expectedResult = new DeviceAgeInfo { Found = true, Age = 10 };

        _postgreSqlServiceMock
            .Setup(x =>
                x.GetTreatmentsWithAdvancedFilterAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(treatments);

        _cannulaAgeServiceMock
            .Setup(x =>
                x.CalculateDeviceAge(
                    It.IsAny<List<Treatment>>(),
                    It.IsAny<long>(),
                    It.IsAny<DeviceAgePreferences>()
                )
            )
            .Returns(expectedResult);

        // Act
        var result = await _controller.GetCannulaAge();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var actualResult = Assert.IsType<DeviceAgeInfo>(okResult.Value);
        Assert.Equal(expectedResult.Found, actualResult.Found);
        Assert.Equal(expectedResult.Age, actualResult.Age);
    }

    [Fact]
    public async Task GetSensorAge_WithCustomParameters_CallsServiceWithCorrectPreferences()
    {
        // Arrange
        var treatments = new List<Treatment>();
        var expectedResult = new SensorAgeInfo();

        _postgreSqlServiceMock
            .Setup(x =>
                x.GetTreatmentsWithAdvancedFilterAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(treatments);

        _sensorAgeServiceMock
            .Setup(x =>
                x.CalculateSensorAge(
                    It.IsAny<List<Treatment>>(),
                    It.IsAny<long>(),
                    It.IsAny<DeviceAgePreferences>()
                )
            )
            .Returns(expectedResult);

        // Act
        var result = await _controller.GetSensorAge(
            info: 100,
            warn: 150,
            urgent: 170,
            display: "hours",
            enableAlerts: true
        );

        // Assert
        _sensorAgeServiceMock.Verify(
            x =>
                x.CalculateSensorAge(
                    It.IsAny<List<Treatment>>(),
                    It.IsAny<long>(),
                    It.Is<DeviceAgePreferences>(p =>
                        p.Info == 100
                        && p.Warn == 150
                        && p.Urgent == 170
                        && p.Display == "hours"
                        && p.EnableAlerts == true
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetBatteryAge_WithDefaultParameters_ReturnsOkResult()
    {
        // Arrange
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "Pump Battery Change",
                Mills = DateTimeOffset.UtcNow.AddHours(-300).ToUnixTimeMilliseconds(),
            },
        };
        var expectedResult = new DeviceAgeInfo { Found = true, Age = 300 };

        _postgreSqlServiceMock
            .Setup(x =>
                x.GetTreatmentsWithAdvancedFilterAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(treatments);

        _batteryAgeServiceMock
            .Setup(x =>
                x.CalculateDeviceAge(
                    It.IsAny<List<Treatment>>(),
                    It.IsAny<long>(),
                    It.IsAny<DeviceAgePreferences>()
                )
            )
            .Returns(expectedResult);

        // Act
        var result = await _controller.GetBatteryAge();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var actualResult = Assert.IsType<DeviceAgeInfo>(okResult.Value);
        Assert.Equal(expectedResult.Found, actualResult.Found);
        Assert.Equal(expectedResult.Age, actualResult.Age);
    }

    [Fact]
    public async Task GetCalibrationAge_WithDefaultParameters_ReturnsOkResult()
    {
        // Arrange
        var treatments = new List<Treatment>
        {
            new()
            {
                EventType = "BG Check",
                Mills = DateTimeOffset.UtcNow.AddHours(-5).ToUnixTimeMilliseconds(),
                Glucose = 120,
                GlucoseType = "Finger",
            },
        };
        var expectedResult = new DeviceAgeInfo { Found = true, Age = 5 };

        _postgreSqlServiceMock
            .Setup(x =>
                x.GetTreatmentsWithAdvancedFilterAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(treatments);

        _calibrationAgeServiceMock
            .Setup(x =>
                x.CalculateDeviceAge(
                    It.IsAny<List<Treatment>>(),
                    It.IsAny<long>(),
                    It.IsAny<DeviceAgePreferences>()
                )
            )
            .Returns(expectedResult);

        // Act
        var result = await _controller.GetCalibrationAge();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var actualResult = Assert.IsType<DeviceAgeInfo>(okResult.Value);
        Assert.Equal(expectedResult.Found, actualResult.Found);
        Assert.Equal(expectedResult.Age, actualResult.Age);
    }

    [Fact]
    public async Task GetCalibrationAge_WithCustomParameters_CallsServiceWithCorrectPreferences()
    {
        // Arrange
        var treatments = new List<Treatment>();
        var expectedResult = new DeviceAgeInfo();

        _postgreSqlServiceMock
            .Setup(x =>
                x.GetTreatmentsWithAdvancedFilterAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(treatments);

        _calibrationAgeServiceMock
            .Setup(x =>
                x.CalculateDeviceAge(
                    It.IsAny<List<Treatment>>(),
                    It.IsAny<long>(),
                    It.IsAny<DeviceAgePreferences>()
                )
            )
            .Returns(expectedResult);

        // Act
        var result = await _controller.GetCalibrationAge(
            info: 12,
            warn: 24,
            urgent: 48,
            display: "hours",
            enableAlerts: true
        );

        // Assert
        _calibrationAgeServiceMock.Verify(
            x =>
                x.CalculateDeviceAge(
                    It.IsAny<List<Treatment>>(),
                    It.IsAny<long>(),
                    It.Is<DeviceAgePreferences>(p =>
                        p.Info == 12
                        && p.Warn == 24
                        && p.Urgent == 48
                        && p.Display == "hours"
                        && p.EnableAlerts == true
                    )
                ),
            Times.Once
        );
    }

    [Fact]
    public async Task GetAllDeviceAges_ReturnsAllDeviceAgeInformation()
    {
        // Arrange
        var treatments = new List<Treatment>();
        var cannulaAge = new DeviceAgeInfo { Found = true, Age = 48 };
        var sensorAge = new SensorAgeInfo
        {
            SensorStart = new DeviceAgeInfo { Found = true, Age = 144 },
            Min = "Sensor Start",
        };
        var batteryAge = new DeviceAgeInfo { Found = true, Age = 312 };
        var calibrationAge = new DeviceAgeInfo { Found = true, Age = 30 };

        _postgreSqlServiceMock
            .Setup(x =>
                x.GetTreatmentsWithAdvancedFilterAsync(
                    It.IsAny<int>(),
                    It.IsAny<int>(),
                    It.IsAny<string>(),
                    It.IsAny<bool>(),
                    It.IsAny<CancellationToken>()
                )
            )
            .ReturnsAsync(treatments);

        _cannulaAgeServiceMock
            .Setup(x =>
                x.CalculateDeviceAge(
                    It.IsAny<List<Treatment>>(),
                    It.IsAny<long>(),
                    It.IsAny<DeviceAgePreferences>()
                )
            )
            .Returns(cannulaAge);

        _sensorAgeServiceMock
            .Setup(x =>
                x.CalculateSensorAge(
                    It.IsAny<List<Treatment>>(),
                    It.IsAny<long>(),
                    It.IsAny<DeviceAgePreferences>()
                )
            )
            .Returns(sensorAge);

        _batteryAgeServiceMock
            .Setup(x =>
                x.CalculateDeviceAge(
                    It.IsAny<List<Treatment>>(),
                    It.IsAny<long>(),
                    It.IsAny<DeviceAgePreferences>()
                )
            )
            .Returns(batteryAge);

        _calibrationAgeServiceMock
            .Setup(x =>
                x.CalculateDeviceAge(
                    It.IsAny<List<Treatment>>(),
                    It.IsAny<long>(),
                    It.IsAny<DeviceAgePreferences>()
                )
            )
            .Returns(calibrationAge);

        // Act
        var result = await _controller.GetAllDeviceAges();

        // Assert
        var okResult = Assert.IsType<OkObjectResult>(result.Result);
        var value = okResult.Value;
        Assert.NotNull(value);

        // Verify all services were called
        _cannulaAgeServiceMock.Verify(
            x =>
                x.CalculateDeviceAge(
                    It.IsAny<List<Treatment>>(),
                    It.IsAny<long>(),
                    It.IsAny<DeviceAgePreferences>()
                ),
            Times.Once
        );
        _sensorAgeServiceMock.Verify(
            x =>
                x.CalculateSensorAge(
                    It.IsAny<List<Treatment>>(),
                    It.IsAny<long>(),
                    It.IsAny<DeviceAgePreferences>()
                ),
            Times.Once
        );
        _batteryAgeServiceMock.Verify(
            x =>
                x.CalculateDeviceAge(
                    It.IsAny<List<Treatment>>(),
                    It.IsAny<long>(),
                    It.IsAny<DeviceAgePreferences>()
                ),
            Times.Once
        );
        _calibrationAgeServiceMock.Verify(
            x =>
                x.CalculateDeviceAge(
                    It.IsAny<List<Treatment>>(),
                    It.IsAny<long>(),
                    It.IsAny<DeviceAgePreferences>()
                ),
            Times.Once
        );
    }
}
