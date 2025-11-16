using System.Data.Common;
using BenchmarkDotNet.Attributes;
using BenchmarkDotNet.Running;
using Microsoft.Data.Sqlite;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Nocturne.Infrastructure.Data;
using Nocturne.Infrastructure.Data.Repositories;
using Nocturne.Infrastructure.Data.Services;
using Xunit;

#pragma warning disable CA1515 // Consider making public types internal

namespace Nocturne.Infrastructure.Data.Performance.Tests;

/// <summary>
/// Performance benchmarks for Entry and Treatment repositories
/// Tests various scenarios including bulk operations, complex queries, and edge cases
/// </summary>
[MemoryDiagnoser]
[SimpleJob]
[Trait("Category", "Performance")]
[Trait("Category", "BenchmarkDotNet")]
public class RepositoryPerformanceBenchmarks : IDisposable
{
    private ServiceProvider? _serviceProvider;
    private NocturneDbContext? _dbContext;
    private DbConnection? _connection;
    private EntryRepository? _entryRepository;
    private TreatmentRepository? _treatmentRepository;

    [GlobalSetup]
    public void Setup()
    {
        // Create in-memory SQLite database for benchmarking
        _connection = new SqliteConnection("Filename=:memory:");
        _connection.Open();

        var services = new ServiceCollection();
        services.AddLogging(builder => builder.SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<NocturneDbContext>(options =>
            options
                .UseSqlite(_connection)
                .EnableSensitiveDataLogging(false) // Disable for performance
                .EnableDetailedErrors(false)
        );

        services.AddScoped<IQueryParser, QueryParser>();
        services.AddScoped<EntryRepository>();
        services.AddScoped<TreatmentRepository>();

        _serviceProvider = services.BuildServiceProvider();
        _dbContext = _serviceProvider.GetRequiredService<NocturneDbContext>();

        // Create database schema
        _dbContext.Database.EnsureCreated();

        // Initialize repositories
        _entryRepository = _serviceProvider.GetRequiredService<EntryRepository>();
        _treatmentRepository = _serviceProvider.GetRequiredService<TreatmentRepository>();
    }

    [GlobalCleanup]
    public void Cleanup()
    {
        _serviceProvider?.Dispose();
        _connection?.Dispose();
    }

    #region Entry Repository Benchmarks

    [Benchmark]
    [Arguments(100)]
    [Arguments(500)]
    [Arguments(1000)]
    public async Task CreateEntries_BulkInsert(int entryCount)
    {
        var entries = GenerateTestEntries(entryCount);
        await _entryRepository!.CreateEntriesAsync(entries);
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    [Arguments(100)]
    public async Task QueryEntries_WithPagination(int pageSize)
    {
        // Setup - ensure we have data
        if (await _entryRepository!.CountEntriesAsync() < pageSize * 2)
        {
            var entries = GenerateTestEntries(pageSize * 5);
            await _entryRepository.CreateEntriesAsync(entries);
        }

        // Benchmark the query
        var result = await _entryRepository.GetEntriesAsync(count: pageSize, skip: 0);

        // Consume the results to ensure full execution
        _ = result.ToList();
    }

    [Benchmark]
    public async Task QueryEntries_WithTypeFilter()
    {
        // Setup
        if (await _entryRepository!.CountEntriesAsync() < 100)
        {
            var entries = GenerateTestEntries(200);
            await _entryRepository.CreateEntriesAsync(entries);
        }

        // Benchmark
        var result = await _entryRepository.GetEntriesAsync(type: "sgv", count: 50);
        _ = result.ToList();
    }

    [Benchmark]
    public async Task QueryEntries_WithAdvancedFilter()
    {
        // Setup
        if (await _entryRepository!.CountEntriesAsync() < 100)
        {
            var entries = GenerateTestEntries(200);
            await _entryRepository.CreateEntriesAsync(entries);
        }

        // Benchmark date filtering
        var filterTime = DateTimeOffset.UtcNow.AddHours(-12);
        var result = await _entryRepository.GetEntriesWithAdvancedFilterAsync(
            dateString: filterTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            count: 50
        );
        _ = result.ToList();
    }

    [Benchmark]
    public async Task QueryEntries_GetCurrent()
    {
        // Setup
        if (await _entryRepository!.CountEntriesAsync() < 10)
        {
            var entries = GenerateTestEntries(50);
            await _entryRepository.CreateEntriesAsync(entries);
        }

        // Benchmark
        var result = await _entryRepository.GetCurrentEntryAsync();
    }

    [Benchmark]
    public async Task CountEntries_Total()
    {
        // Setup
        if (await _entryRepository!.CountEntriesAsync() < 100)
        {
            var entries = GenerateTestEntries(200);
            await _entryRepository.CreateEntriesAsync(entries);
        }

        // Benchmark
        var count = await _entryRepository.CountEntriesAsync();
    }

    #endregion

    #region Treatment Repository Benchmarks

    [Benchmark]
    [Arguments(100)]
    [Arguments(500)]
    [Arguments(1000)]
    public async Task CreateTreatments_BulkInsert(int treatmentCount)
    {
        var treatments = GenerateTestTreatments(treatmentCount);
        await _treatmentRepository!.CreateTreatmentsAsync(treatments);
    }

    [Benchmark]
    [Arguments(10)]
    [Arguments(50)]
    [Arguments(100)]
    public async Task QueryTreatments_WithPagination(int pageSize)
    {
        // Setup
        if (await _treatmentRepository!.CountTreatmentsAsync() < pageSize * 2)
        {
            var treatments = GenerateTestTreatments(pageSize * 5);
            await _treatmentRepository.CreateTreatmentsAsync(treatments);
        }

        // Benchmark
        var result = await _treatmentRepository.GetTreatmentsAsync(count: pageSize, skip: 0);
        _ = result.ToList();
    }

    [Benchmark]
    public async Task QueryTreatments_WithEventTypeFilter()
    {
        // Setup
        if (await _treatmentRepository!.CountTreatmentsAsync() < 100)
        {
            var treatments = GenerateTestTreatments(200);
            await _treatmentRepository.CreateTreatmentsAsync(treatments);
        }

        // Benchmark
        var result = await _treatmentRepository.GetTreatmentsAsync(
            eventType: "Meal Bolus",
            count: 50
        );
        _ = result.ToList();
    }

    [Benchmark]
    public async Task QueryTreatments_WithAdvancedFilter()
    {
        // Setup
        if (await _treatmentRepository!.CountTreatmentsAsync() < 100)
        {
            var treatments = GenerateTestTreatments(200);
            await _treatmentRepository.CreateTreatmentsAsync(treatments);
        }

        // Benchmark
        var filterTime = DateTimeOffset.UtcNow.AddHours(-12);
        var result = await _treatmentRepository.GetTreatmentsWithAdvancedFilterAsync(
            dateString: filterTime.ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            count: 50
        );
        _ = result.ToList();
    }

    #endregion

    #region Mixed Operations Benchmarks

    [Benchmark]
    public async Task MixedOperations_EntriesAndTreatments()
    {
        // Create some entries
        var entries = GenerateTestEntries(20);
        await _entryRepository!.CreateEntriesAsync(entries);

        // Create some treatments
        var treatments = GenerateTestTreatments(10);
        await _treatmentRepository!.CreateTreatmentsAsync(treatments);

        // Query both
        var recentEntries = await _entryRepository.GetEntriesAsync(count: 10);
        var recentTreatments = await _treatmentRepository.GetTreatmentsAsync(count: 5);

        // Count both
        var entryCount = await _entryRepository.CountEntriesAsync();
        var treatmentCount = await _treatmentRepository.CountTreatmentsAsync();

        // Consume results
        _ = recentEntries.ToList();
        _ = recentTreatments.ToList();
    }

    #endregion

    #region Helper Methods

    private static Entry[] GenerateTestEntries(int count)
    {
        var random = new Random(42); // Fixed seed for consistent benchmarks
        var baseTime = DateTimeOffset.UtcNow;

        return Enumerable
            .Range(1, count)
            .Select(i => new Entry
            {
                Id = Guid.NewGuid().ToString(),
                Mills = baseTime.AddMinutes(-i).ToUnixTimeMilliseconds(),
                DateString = baseTime.AddMinutes(-i).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                Mgdl = 70.0 + random.Next(0, 200), // Random SGV between 70-270
                Sgv = 70.0 + random.Next(0, 200),
                Direction = GetRandomDirection(random),
                Type = i % 4 == 0 ? "mbg" : "sgv", // 25% MBG, 75% SGV
                Device = $"device-{i % 5}", // 5 different devices
                Delta = random.NextDouble() * 10 - 5, // Delta between -5 and +5
                Rssi = 80 + random.Next(0, 41), // RSSI between 80-120
                Noise = random.Next(1, 5), // Noise 1-4
                CreatedAt = baseTime.AddMinutes(-i).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            })
            .ToArray();
    }

    private static Treatment[] GenerateTestTreatments(int count)
    {
        var random = new Random(42); // Fixed seed for consistent benchmarks
        var baseTime = DateTimeOffset.UtcNow;
        var eventTypes = new[]
        {
            "Meal Bolus",
            "Correction Bolus",
            "Carb Correction",
            "BG Check",
            "Temp Basal",
        };

        return Enumerable
            .Range(1, count)
            .Select(i => new Treatment
            {
                Id = Guid.NewGuid().ToString(),
                Mills = baseTime.AddMinutes(-i * 2).ToUnixTimeMilliseconds(),
                Created_at = baseTime.AddMinutes(-i * 2).ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
                EventType = eventTypes[i % eventTypes.Length],
                Insulin = i % 3 == 0 ? null : 0.5 + random.NextDouble() * 5, // Random insulin 0.5-5.5
                Carbs = i % 2 == 0 ? null : random.Next(5, 101), // Random carbs 5-100
                Notes = $"Test treatment {i}",
                EnteredBy = $"user-{i % 3}" // 3 different users
            })
            .ToArray();
    }

    private static string GetRandomDirection(Random random)
    {
        var directions = new[]
        {
            "Flat",
            "SingleUp",
            "DoubleUp",
            "SingleDown",
            "DoubleDown",
            "FortyFiveUp",
            "FortyFiveDown",
        };
        return directions[random.Next(directions.Length)];
    }

    #endregion

    public void Dispose()
    {
        _serviceProvider?.Dispose();
        _connection?.Dispose();
    }
}

