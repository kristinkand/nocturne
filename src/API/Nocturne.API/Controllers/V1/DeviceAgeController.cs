using Microsoft.AspNetCore.Mvc;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data.Abstractions;
using BatteryAgeService = Nocturne.API.Services.BatteryAgeService;
using CalibrationAgeService = Nocturne.API.Services.CalibrationAgeService;
using CannulaAgeService = Nocturne.API.Services.CannulaAgeService;
using SensorAgeService = Nocturne.API.Services.SensorAgeService;

namespace Nocturne.API.Controllers.V1;

/// <summary>
/// Device age controller for cannula, sensor, and battery age tracking
/// Provides 1:1 legacy JavaScript compatibility with original Nightscout age plugins
/// </summary>
[ApiController]
[Route("api/v1/[controller]")]
[Produces("application/json")]
public class DeviceAgeController : ControllerBase
{
    private readonly ICannulaAgeService _cannulaAgeService;
    private readonly ISensorAgeService _sensorAgeService;
    private readonly IBatteryAgeService _batteryAgeService;
    private readonly ICalibrationAgeService _calibrationAgeService;
    private readonly IPostgreSqlService _postgreSqlService;

    public DeviceAgeController(
        ICannulaAgeService cannulaAgeService,
        ISensorAgeService sensorAgeService,
        IBatteryAgeService batteryAgeService,
        ICalibrationAgeService calibrationAgeService,
        IPostgreSqlService postgreSqlService
    )
    {
        _cannulaAgeService = cannulaAgeService;
        _sensorAgeService = sensorAgeService;
        _batteryAgeService = batteryAgeService;
        _calibrationAgeService = calibrationAgeService;
        _postgreSqlService = postgreSqlService;
    }

    /// <summary>
    /// Get cannula age information based on site change treatments
    /// </summary>
    /// <param name="info">Info threshold in hours (default: 44)</param>
    /// <param name="warn">Warning threshold in hours (default: 48)</param>
    /// <param name="urgent">Urgent threshold in hours (default: 72)</param>
    /// <param name="display">Display format: hours or days (default: hours)</param>
    /// <param name="enableAlerts">Enable alert notifications (default: false)</param>
    /// <returns>Cannula age information</returns>
    [HttpGet("cannula")]
    public async Task<ActionResult<DeviceAgeInfo>> GetCannulaAge(
        [FromQuery] int? info = null,
        [FromQuery] int? warn = null,
        [FromQuery] int? urgent = null,
        [FromQuery] string? display = null,
        [FromQuery] bool? enableAlerts = null
    )
    {
        var preferences = GetPreferences(
            info,
            warn,
            urgent,
            display,
            enableAlerts,
            CannulaAgeService.GetDefaultPreferences()
        );
        var treatments = await GetTreatments();
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var result = _cannulaAgeService.CalculateDeviceAge(treatments, currentTime, preferences);
        return Ok(result);
    }

    /// <summary>
    /// Get sensor age information based on sensor start and sensor change treatments
    /// </summary>
    /// <param name="info">Info threshold in hours (default: 144)</param>
    /// <param name="warn">Warning threshold in hours (default: 164)</param>
    /// <param name="urgent">Urgent threshold in hours (default: 166)</param>
    /// <param name="display">Display format: hours or days (default: days)</param>
    /// <param name="enableAlerts">Enable alert notifications (default: false)</param>
    /// <returns>Sensor age information</returns>
    [HttpGet("sensor")]
    public async Task<ActionResult<SensorAgeInfo>> GetSensorAge(
        [FromQuery] int? info = null,
        [FromQuery] int? warn = null,
        [FromQuery] int? urgent = null,
        [FromQuery] string? display = null,
        [FromQuery] bool? enableAlerts = null
    )
    {
        var preferences = GetPreferences(
            info,
            warn,
            urgent,
            display,
            enableAlerts,
            SensorAgeService.GetDefaultPreferences()
        );
        var treatments = await GetTreatments();
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var result = _sensorAgeService.CalculateSensorAge(treatments, currentTime, preferences);
        return Ok(result);
    }

    /// <summary>
    /// Get battery age information based on battery change treatments
    /// </summary>
    /// <param name="info">Info threshold in hours (default: 312)</param>
    /// <param name="warn">Warning threshold in hours (default: 336)</param>
    /// <param name="urgent">Urgent threshold in hours (default: 360)</param>
    /// <param name="display">Display format: hours or days (default: days)</param>
    /// <param name="enableAlerts">Enable alert notifications (default: false)</param>
    /// <returns>Battery age information</returns>
    [HttpGet("battery")]
    public async Task<ActionResult<DeviceAgeInfo>> GetBatteryAge(
        [FromQuery] int? info = null,
        [FromQuery] int? warn = null,
        [FromQuery] int? urgent = null,
        [FromQuery] string? display = null,
        [FromQuery] bool? enableAlerts = null
    )
    {
        var preferences = GetPreferences(
            info,
            warn,
            urgent,
            display,
            enableAlerts,
            BatteryAgeService.GetDefaultPreferences()
        );
        var treatments = await GetTreatments();
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var result = _batteryAgeService.CalculateDeviceAge(treatments, currentTime, preferences);
        return Ok(result);
    }

    /// <summary>
    /// Get calibration age information based on BG Check and Calibration treatments
    /// </summary>
    /// <param name="info">Info threshold in hours (default: 24)</param>
    /// <param name="warn">Warning threshold in hours (default: 48)</param>
    /// <param name="urgent">Urgent threshold in hours (default: 72)</param>
    /// <param name="display">Display format: hours or days (default: hours)</param>
    /// <param name="enableAlerts">Enable alert notifications (default: false)</param>
    /// <returns>Calibration age information</returns>
    [HttpGet("calibration")]
    public async Task<ActionResult<DeviceAgeInfo>> GetCalibrationAge(
        [FromQuery] int? info = null,
        [FromQuery] int? warn = null,
        [FromQuery] int? urgent = null,
        [FromQuery] string? display = null,
        [FromQuery] bool? enableAlerts = null
    )
    {
        var preferences = GetPreferences(
            info,
            warn,
            urgent,
            display,
            enableAlerts,
            CalibrationAgeService.GetDefaultPreferences()
        );
        var treatments = await GetTreatments();
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var result = _calibrationAgeService.CalculateDeviceAge(
            treatments,
            currentTime,
            preferences
        );
        return Ok(result);
    }

    /// <summary>
    /// Get all device ages in a single request
    /// </summary>
    /// <returns>Combined device age information</returns>
    [HttpGet("all")]
    public async Task<ActionResult<object>> GetAllDeviceAges()
    {
        var treatments = await GetTreatments();
        var currentTime = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();

        var cannulaPrefs = CannulaAgeService.GetDefaultPreferences();
        var sensorPrefs = SensorAgeService.GetDefaultPreferences();
        var batteryPrefs = BatteryAgeService.GetDefaultPreferences();
        var calibrationPrefs = CalibrationAgeService.GetDefaultPreferences();

        var cannulaAge = _cannulaAgeService.CalculateDeviceAge(
            treatments,
            currentTime,
            cannulaPrefs
        );
        var sensorAge = _sensorAgeService.CalculateSensorAge(treatments, currentTime, sensorPrefs);
        var batteryAge = _batteryAgeService.CalculateDeviceAge(
            treatments,
            currentTime,
            batteryPrefs
        );
        var calibrationAge = _calibrationAgeService.CalculateDeviceAge(
            treatments,
            currentTime,
            calibrationPrefs
        );

        return Ok(
            new
            {
                cannula = cannulaAge,
                sensor = sensorAge,
                battery = batteryAge,
                calibration = calibrationAge,
                timestamp = currentTime,
            }
        );
    }

    /// <summary>
    /// Get treatments from the database
    /// </summary>
    private async Task<List<Treatment>> GetTreatments()
    {
        // Get treatments from the last 30 days to ensure we capture relevant data
        var thirtyDaysAgo = DateTimeOffset.UtcNow.AddDays(-30).ToUnixTimeMilliseconds();
        var findQuery = $"{{\"mills\": {{\"$gte\": {thirtyDaysAgo}}}}}";

        var treatments = await _postgreSqlService.GetTreatmentsWithAdvancedFilterAsync(
            count: 1000, // Get a large number to ensure we capture all relevant treatments
            skip: 0,
            findQuery: findQuery,
            reverseResults: false
        );

        return treatments.ToList();
    }

    /// <summary>
    /// Build preferences object from query parameters
    /// </summary>
    private static DeviceAgePreferences GetPreferences(
        int? info,
        int? warn,
        int? urgent,
        string? display,
        bool? enableAlerts,
        DeviceAgePreferences defaults
    )
    {
        return new DeviceAgePreferences
        {
            Info = info ?? defaults.Info,
            Warn = warn ?? defaults.Warn,
            Urgent = urgent ?? defaults.Urgent,
            Display = display ?? defaults.Display,
            EnableAlerts = enableAlerts ?? defaults.EnableAlerts,
        };
    }
}
