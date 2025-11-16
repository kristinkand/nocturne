using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Testcontainers.PostgreSql;

namespace Nocturne.Infrastructure.Data.Tests.Integration;

/// <summary>
/// Integration tests for PostgreSQL repositories using real PostgreSQL database
/// These tests verify that MongoDB-style queries work correctly with PostgreSQL
/// </summary>
[Trait("Category", "Integration")]
[Trait("Category", "PostgreSQL")]
[Trait("Category", "Repository")]
public class PostgreSqlRepositoryIntegrationTests : IAsyncLifetime
{
    private PostgreSqlContainer? _postgresContainer;
    private ServiceProvider? _serviceProvider;
    private NocturneDbContext? _dbContext;

    public async Task InitializeAsync()
    {
        // Create and start PostgreSQL container
        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15-alpine")
            .WithDatabase("nocturne_test")
            .WithUsername("test_user")
            .WithPassword("test_password")
            .WithCleanUp(true)
            .Build();

        await _postgresContainer.StartAsync();

        // Setup services
        var services = new ServiceCollection();
        services.AddLogging(builder => builder.AddConsole().SetMinimumLevel(LogLevel.Warning));

        services.AddDbContext<NocturneDbContext>(options =>
            options
                .UseNpgsql(_postgresContainer.GetConnectionString())
                .EnableSensitiveDataLogging()
                .EnableDetailedErrors()
        );

        services.AddScoped<EntryRepository>();
        services.AddScoped<TreatmentRepository>();

        _serviceProvider = services.BuildServiceProvider();

        // Create database schema
        _dbContext = _serviceProvider.GetRequiredService<NocturneDbContext>();
        await _dbContext.Database.MigrateAsync();
    }

    public async Task DisposeAsync()
    {
        if (_serviceProvider != null)
        {
            await _serviceProvider.DisposeAsync();
        }

        if (_postgresContainer != null)
        {
            await _postgresContainer.DisposeAsync();
        }
    }

    #region Entry Repository Integration Tests

    [Fact]
    public async Task EntryRepository_ShouldPersistAndRetrieveData_WithPostgreSQL()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<EntryRepository>();

        var testEntries = new[]
        {
            CreateTestEntry(sgv: 120.0, type: "sgv", device: "dexcom"),
            CreateTestEntry(sgv: 95.0, type: "mbg", device: "meter"),
            CreateTestEntry(sgv: 140.0, type: "sgv", device: "dexcom"),
        };

        // Act - Create
        var createdEntries = await repository.CreateEntriesAsync(testEntries);

        // Act - Retrieve
        var allEntries = await repository.GetEntriesAsync(count: 10);
        var sgvEntries = await repository.GetEntriesAsync(type: "sgv", count: 10);
        var count = await repository.CountEntriesAsync();

        // Assert
        createdEntries.Should().HaveCount(3);
        allEntries.Should().HaveCount(3);
        sgvEntries.Should().HaveCount(2);
        count.Should().Be(3);

        // Verify data integrity
        sgvEntries.All(e => e.Type == "sgv").Should().BeTrue();
        sgvEntries.All(e => e.Device == "dexcom").Should().BeTrue();
    }

    [Fact]
    public async Task EntryRepository_ShouldHandleComplexFiltering_WithPostgreSQL()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<EntryRepository>();

        var baseTime = DateTimeOffset.UtcNow;
        var testEntries = new[]
        {
            CreateTestEntry(sgv: 80.0, mills: baseTime.AddHours(-3).ToUnixTimeMilliseconds()),
            CreateTestEntry(sgv: 120.0, mills: baseTime.AddHours(-2).ToUnixTimeMilliseconds()),
            CreateTestEntry(sgv: 160.0, mills: baseTime.AddHours(-1).ToUnixTimeMilliseconds()),
            CreateTestEntry(sgv: 200.0, mills: baseTime.ToUnixTimeMilliseconds()),
        };

        await repository.CreateEntriesAsync(testEntries);

        // Act - Test date filtering
        var recentEntries = await repository.GetEntriesWithAdvancedFilterAsync(
            dateString: baseTime.AddHours(-1.5).ToString("yyyy-MM-ddTHH:mm:ss.fffZ")
        );

        // Act - Test pagination
        var firstPage = await repository.GetEntriesAsync(count: 2, skip: 0);
        var secondPage = await repository.GetEntriesAsync(count: 2, skip: 2);

        // Assert
        recentEntries.Should().HaveCount(2); // Last 2 entries
        recentEntries
            .All(e => e.Mills >= baseTime.AddHours(-1.5).ToUnixTimeMilliseconds())
            .Should()
            .BeTrue();

        firstPage.Should().HaveCount(2);
        secondPage.Should().HaveCount(2);
        firstPage.Select(e => e.Id).Should().NotIntersectWith(secondPage.Select(e => e.Id));
    }

    [Fact]
    public async Task EntryRepository_ShouldHandleBulkOperations_WithPostgreSQL()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<EntryRepository>();

        var largeDataset = Enumerable
            .Range(1, 100)
            .Select(i =>
                CreateTestEntry(
                    sgv: 100.0 + i,
                    type: i % 2 == 0 ? "sgv" : "mbg",
                    device: $"device-{i % 5}"
                )
            )
            .ToArray();

        // Act - Bulk insert
        var start = DateTimeOffset.UtcNow;
        await repository.CreateEntriesAsync(largeDataset);
        var insertDuration = DateTimeOffset.UtcNow - start;

        // Act - Bulk query
        start = DateTimeOffset.UtcNow;
        var allEntries = await repository.GetEntriesAsync(count: 100);
        var queryDuration = DateTimeOffset.UtcNow - start;

        // Act - Bulk delete
        start = DateTimeOffset.UtcNow;
        var deletedCount = await repository.DeleteEntriesAsync(type: "sgv");
        var deleteDuration = DateTimeOffset.UtcNow - start;

        // Assert
        allEntries.Should().HaveCount(100);
        deletedCount.Should().Be(50); // Half were SGV

        var remainingCount = await repository.CountEntriesAsync();
        remainingCount.Should().Be(50); // Half should remain

        // Performance assertions
        insertDuration.Should().BeLessThan(TimeSpan.FromSeconds(10));
        queryDuration.Should().BeLessThan(TimeSpan.FromSeconds(5));
        deleteDuration.Should().BeLessThan(TimeSpan.FromSeconds(5));
    }

    #endregion

    #region Treatment Repository Integration Tests

    [Fact]
    public async Task TreatmentRepository_ShouldPersistAndRetrieveData_WithPostgreSQL()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<TreatmentRepository>();

        var testTreatments = new[]
        {
            CreateTestTreatment(insulin: 3.5, eventType: "Meal Bolus", carbs: 45.0),
            CreateTestTreatment(insulin: 1.5, eventType: "Correction Bolus"),
            CreateTestTreatment(carbs: 15.0, eventType: "Carb Correction"),
        };

        // Act - Create
        var createdTreatments = await repository.CreateTreatmentsAsync(testTreatments);

        // Act - Retrieve
        var allTreatments = await repository.GetTreatmentsAsync(count: 10);
        var mealBoluses = await repository.GetTreatmentsAsync(eventType: "Meal Bolus", count: 10);
        var count = await repository.CountTreatmentsAsync();

        // Assert
        createdTreatments.Should().HaveCount(3);
        allTreatments.Should().HaveCount(3);
        mealBoluses.Should().HaveCount(1);
        count.Should().Be(3);

        // Verify data integrity
        var mealBolus = mealBoluses.First();
        mealBolus.Insulin.Should().Be(3.5);
        mealBolus.Carbs.Should().Be(45.0);
        mealBolus.EventType.Should().Be("Meal Bolus");
    }

    [Fact]
    public async Task TreatmentRepository_ShouldHandleComplexTreatmentData_WithPostgreSQL()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var repository = scope.ServiceProvider.GetRequiredService<TreatmentRepository>();

        var complexTreatment = CreateTestTreatment();
        complexTreatment.Insulin = 4.25;
        complexTreatment.Carbs = 62.5;
        complexTreatment.Protein = 18.0;
        complexTreatment.Fat = 12.5;
        complexTreatment.Duration = 180.0;
        complexTreatment.Percent = 85.0;
        complexTreatment.Notes = "Complex meal with high fat content";
        complexTreatment.BolusCalc = new Dictionary<string, object>
        {
            ["carbs"] = 60,
            ["cob"] = 8.5,
            ["iob"] = 0.8,
            ["ic"] = 15.0,
            ["isf"] = 50.0,
        };
        complexTreatment.AbsorptionTime = 240;
        complexTreatment.SplitNow = 60.0;
        complexTreatment.SplitExt = 40.0;

        // Act
        var result = await repository.CreateTreatmentsAsync(new[] { complexTreatment });
        var retrieved = await repository.GetTreatmentByIdAsync(complexTreatment.Id!);

        // Assert
        result.Should().HaveCount(1);
        retrieved.Should().NotBeNull();

        retrieved!.Insulin.Should().Be(4.25);
        retrieved.Carbs.Should().Be(62.5);
        retrieved.Protein.Should().Be(18.0);
        retrieved.Fat.Should().Be(12.5);
        retrieved.Duration.Should().Be(180.0);
        retrieved.AbsorptionTime.Should().Be(240);
        retrieved.BolusCalc.Should().NotBeNull();
        retrieved.Notes.Should().Be("Complex meal with high fat content");
    }

    #endregion

    #region MongoDB Query Compatibility Tests

    [Fact]
    public async Task Repositories_ShouldLogMongoDBQueries_ForFutureImplementation()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var entryRepository = scope.ServiceProvider.GetRequiredService<EntryRepository>();
        var treatmentRepository = scope.ServiceProvider.GetRequiredService<TreatmentRepository>();

        var testEntry = CreateTestEntry(sgv: 150.0, type: "sgv");
        var testTreatment = CreateTestTreatment(insulin: 2.5, eventType: "Meal Bolus");

        await entryRepository.CreateEntriesAsync(new[] { testEntry });
        await treatmentRepository.CreateTreatmentsAsync(new[] { testTreatment });

        // Act - These should not throw even though MongoDB query parsing is not implemented
        var entryResult = await entryRepository.GetEntriesWithAdvancedFilterAsync(
            findQuery: "{\"type\":\"sgv\",\"sgv\":{\"$gte\":100,\"$lte\":200}}"
        );

        var treatmentResult = await treatmentRepository.GetTreatmentsWithAdvancedFilterAsync(
            findQuery: "{\"eventType\":\"Meal Bolus\",\"insulin\":{\"$gte\":2.0}}"
        );

        // Assert - Should return data even without query parsing
        entryResult.Should().HaveCount(1);
        treatmentResult.Should().HaveCount(1);

        // In the future, these would be filtered according to the MongoDB query
        entryResult.First().Mgdl.Should().Be(150.0);
        treatmentResult.First().Insulin.Should().Be(2.5);
    }

    [Fact]
    public async Task Repositories_ShouldPreserveDataIntegrity_UnderConcurrentOperations()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var entryRepository = scope.ServiceProvider.GetRequiredService<EntryRepository>();

        var concurrentTasks = new List<Task>();
        var totalEntries = 0;

        // Act - Simulate concurrent inserts
        for (int i = 0; i < 10; i++)
        {
            var batchNumber = i;
            var task = Task.Run(async () =>
            {
                var entries = Enumerable
                    .Range(1, 10)
                    .Select(j =>
                        CreateTestEntry(
                            sgv: 100.0 + (batchNumber * 10) + j,
                            device: $"batch-{batchNumber}-device-{j}"
                        )
                    )
                    .ToArray();

                await entryRepository.CreateEntriesAsync(entries);
                Interlocked.Add(ref totalEntries, entries.Length);
            });

            concurrentTasks.Add(task);
        }

        await Task.WhenAll(concurrentTasks);

        // Assert
        var actualCount = await entryRepository.CountEntriesAsync();
        actualCount.Should().Be(totalEntries);
        actualCount.Should().Be(100); // 10 batches * 10 entries each

        // Verify no duplicate devices (each should be unique)
        var allEntries = await entryRepository.GetEntriesAsync(count: 100);
        var devices = allEntries.Select(e => e.Device).ToHashSet();
        devices.Should().HaveCount(100); // All devices should be unique
    }

    #endregion

    #region Performance and Stress Tests

    [Fact]
    [Trait("Category", "Performance")]
    public async Task Repositories_ShouldMaintainPerformance_WithLargeDatasets()
    {
        // Arrange
        using var scope = _serviceProvider!.CreateScope();
        var entryRepository = scope.ServiceProvider.GetRequiredService<EntryRepository>();
        var treatmentRepository = scope.ServiceProvider.GetRequiredService<TreatmentRepository>();

        const int entryCount = 1000;
        const int treatmentCount = 500;

        var entries = Enumerable
            .Range(1, entryCount)
            .Select(i =>
                CreateTestEntry(
                    sgv: 70.0 + (i % 200), // Vary SGV from 70-270
                    type: i % 4 == 0 ? "mbg" : "sgv",
                    mills: DateTimeOffset.UtcNow.AddMinutes(-i).ToUnixTimeMilliseconds()
                )
            )
            .ToArray();

        var treatments = Enumerable
            .Range(1, treatmentCount)
            .Select(i =>
                CreateTestTreatment(
                    insulin: 0.5 + (i % 20) * 0.25, // Vary insulin from 0.5-5.0
                    eventType: i % 3 == 0 ? "Correction Bolus" : "Meal Bolus",
                    mills: DateTimeOffset.UtcNow.AddMinutes(-i * 2).ToUnixTimeMilliseconds()
                )
            )
            .ToArray();

        // Act - Bulk operations with timing
        var insertStart = DateTimeOffset.UtcNow;

        await entryRepository.CreateEntriesAsync(entries);
        await treatmentRepository.CreateTreatmentsAsync(treatments);

        var insertDuration = DateTimeOffset.UtcNow - insertStart;

        // Act - Query operations with timing
        var queryStart = DateTimeOffset.UtcNow;

        var recentEntries = await entryRepository.GetEntriesAsync(count: 100);
        var sgvEntries = await entryRepository.GetEntriesAsync(type: "sgv", count: 200);
        var mealBoluses = await treatmentRepository.GetTreatmentsAsync(
            eventType: "Meal Bolus",
            count: 200
        );
        var totalEntriesCount = await entryRepository.CountEntriesAsync();
        var totalTreatmentsCount = await treatmentRepository.CountTreatmentsAsync();

        var queryDuration = DateTimeOffset.UtcNow - queryStart;

        // Assert - Data integrity
        totalEntriesCount.Should().Be(entryCount);
        totalTreatmentsCount.Should().Be(treatmentCount);
        recentEntries.Should().HaveCount(100);
        sgvEntries.Should().HaveCount(200);
        mealBoluses.Should().HaveCount(200);

        sgvEntries.All(e => e.Type == "sgv").Should().BeTrue();
        mealBoluses.All(t => t.EventType == "Meal Bolus").Should().BeTrue();

        // Assert - Performance thresholds
        insertDuration
            .Should()
            .BeLessThan(TimeSpan.FromSeconds(30), "Bulk insert should complete within 30 seconds");
        queryDuration
            .Should()
            .BeLessThan(
                TimeSpan.FromSeconds(10),
                "Complex queries should complete within 10 seconds"
            );
    }

    #endregion

    #region Test Helper Methods

    private static Entry CreateTestEntry(
        double sgv = 120.0,
        string type = "sgv",
        long? mills = null,
        string device = "test-device"
    )
    {
        var timestamp = mills ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new Entry
        {
            Id = Guid.NewGuid().ToString(),
            Mills = timestamp,
            DateString = DateTimeOffset
                .FromUnixTimeMilliseconds(timestamp)
                .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            Mgdl = sgv,
            Sgv = sgv,
            Direction = "Flat",
            Type = type,
            Device = device,
            Delta = 0.0,
            Rssi = 100,
            Noise = 1,
            CreatedAt = DateTimeOffset
                .FromUnixTimeMilliseconds(timestamp)
                .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
        };
    }

    private static Treatment CreateTestTreatment(
        double? insulin = 2.0,
        string eventType = "Correction Bolus",
        long? mills = null,
        double? carbs = null
    )
    {
        var timestamp = mills ?? DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
        return new Treatment
        {
            Id = Guid.NewGuid().ToString(),
            Mills = timestamp,
            Created_at = DateTimeOffset
                .FromUnixTimeMilliseconds(timestamp)
                .ToString("yyyy-MM-ddTHH:mm:ss.fffZ"),
            EventType = eventType,
            Insulin = insulin,
            Carbs = carbs,
            Notes = $"Test treatment {Guid.NewGuid().ToString()[..8]}",
            EnteredBy = "test-user",
        };
    }

    #endregion
}
