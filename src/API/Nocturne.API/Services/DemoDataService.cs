using Nocturne.Core.Contracts;
using Nocturne.Core.Models;

namespace Nocturne.API.Services;

public class DemoModeConfiguration
{
    public bool Enabled { get; set; } = false;
    public int IntervalMinutes { get; set; } = 5;
    public int InitialGlucose { get; set; } = 120;
    public int WalkVariance { get; set; } = 10;
    public int MinGlucose { get; set; } = 70;
    public int MaxGlucose { get; set; } = 250;
    public string Device { get; set; } = "demo-cgm";
}

public interface IDemoDataService
{
    Task<Entry> GenerateEntryAsync(CancellationToken cancellationToken = default);
    bool IsEnabled { get; }
    DemoModeConfiguration GetConfiguration();
}

public class DemoDataService : IDemoDataService
{
    private readonly IEntryService _entryService;
    private readonly ILogger<DemoDataService> _logger;
    private readonly DemoModeConfiguration _config;
    private readonly Random _random = new();
    private double _currentGlucose;
    private readonly object _lock = new();

    public DemoDataService(
        IEntryService entryService,
        IConfiguration configuration,
        ILogger<DemoDataService> logger
    )
    {
        _entryService = entryService;
        _logger = logger;
        _config =
            configuration.GetSection("DemoMode").Get<DemoModeConfiguration>()
            ?? new DemoModeConfiguration();
        _currentGlucose = _config.InitialGlucose;
    }

    public bool IsEnabled => _config.Enabled;

    public DemoModeConfiguration GetConfiguration() => _config;

    public Task<Entry> GenerateEntryAsync(CancellationToken cancellationToken = default)
    {
        if (!_config.Enabled)
        {
            throw new InvalidOperationException("Demo mode is not enabled");
        }

        lock (_lock)
        {
            // Generate glucose change using drunken walk algorithm
            var change = GenerateRandomWalk();
            _currentGlucose = Math.Max(
                _config.MinGlucose,
                Math.Min(_config.MaxGlucose, _currentGlucose + change)
            );

            var now = DateTime.UtcNow;
            var mills = new DateTimeOffset(now).ToUnixTimeMilliseconds();
            var direction = CalculateDirection(change);

            var entry = new Entry
            {
                Type = "sgv",
                Device = _config.Device,
                Mills = mills,
                Date = now,
                DateString = now.ToString("o"),
                Mgdl = Math.Round(_currentGlucose, 0),
                Sgv = Math.Round(_currentGlucose, 0),
                Direction = direction.ToString(),
                Delta = Math.Round(change, 1),
                IsDemo = true,
                Filtered = Math.Round(_currentGlucose + (_random.NextDouble() - 0.5) * 2, 0),
                Unfiltered = Math.Round(_currentGlucose + (_random.NextDouble() - 0.5) * 5, 0),
                Rssi = _random.Next(0, 101),
                Noise = _random.Next(0, 5),
                CreatedAt = now.ToString("o"),
                ModifiedAt = now,
            };

            _logger.LogDebug(
                "Generated demo entry: SGV={Sgv}, Direction={Direction}, Change={Change}",
                entry.Sgv,
                entry.Direction,
                change
            );

            return Task.FromResult(entry);
        }
    }

    private double GenerateRandomWalk()
    {
        // Box-Muller transform for normal distribution
        var u1 = _random.NextDouble();
        var u2 = _random.NextDouble();
        var z0 = Math.Sqrt(-2 * Math.Log(u1)) * Math.Cos(2 * Math.PI * u2);

        // Scale by variance
        return z0 * _config.WalkVariance;
    }

    private Direction CalculateDirection(double change)
    {
        // Convert glucose change to direction enum
        return change switch
        {
            > 10 => Direction.DoubleUp,
            > 5 => Direction.SingleUp,
            > 2 => Direction.FortyFiveUp,
            > -2 => Direction.Flat,
            > -5 => Direction.FortyFiveDown,
            > -10 => Direction.SingleDown,
            _ => Direction.DoubleDown,
        };
    }
}
