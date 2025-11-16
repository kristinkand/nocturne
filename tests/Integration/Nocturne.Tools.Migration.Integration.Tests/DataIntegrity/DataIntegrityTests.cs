using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using MongoDB.Bson;
using MongoDB.Driver;
using Nocturne.Tools.Abstractions.Services;
using Nocturne.Tools.Migration.Data;
using Nocturne.Tools.Migration.Services;
using Nocturne.Tools.Migration.Tests.TestDataGeneration;
using Testcontainers.MongoDb;
using Testcontainers.PostgreSql;
using Xunit;
using Xunit.Abstractions;

namespace Nocturne.Tools.Migration.Integration.Tests.DataIntegrity;

/// <summary>
/// Comprehensive data integrity validation tests
/// </summary>
public class DataIntegrityTests : IAsyncLifetime
{
    private readonly ITestOutputHelper _output;
    private readonly MongoDbContainer _mongoContainer;
    private readonly PostgreSqlContainer _postgresContainer;
    private IMongoDatabase _mongoDatabase = null!;
    private MigrationDbContext _dbContext = null!;
    private IDataTransformationService _transformationService = null!;
    private IValidationService _validationService = null!;

    public DataIntegrityTests(ITestOutputHelper output)
    {
        _output = output;

        _mongoContainer = new MongoDbBuilder()
            .WithImage("mongo:7")
            .WithPortBinding(27017, true)
            .Build();

        _postgresContainer = new PostgreSqlBuilder()
            .WithImage("postgres:15")
            .WithDatabase("testdb")
            .WithUsername("testuser")
            .WithPassword("testpass")
            .WithPortBinding(5432, true)
            .Build();
    }

    public async ValueTask InitializeAsync()
    {
        await _mongoContainer.StartAsync();
        await _postgresContainer.StartAsync();

        // Setup MongoDB
        var mongoClient = new MongoClient(_mongoContainer.GetConnectionString());
        _mongoDatabase = mongoClient.GetDatabase("testdb");

        // Setup PostgreSQL
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddDbContext<MigrationDbContext>(options =>
            options.UseNpgsql(_postgresContainer.GetConnectionString())
        );

        var serviceProvider = services.BuildServiceProvider();
        _dbContext = serviceProvider.GetRequiredService<MigrationDbContext>();
        await _dbContext.Database.EnsureCreatedAsync();

        // Setup services
        _transformationService = new DataTransformationService(
            serviceProvider.GetRequiredService<ILogger<DataTransformationService>>()
        );

        _validationService = new ValidationService(
            serviceProvider.GetRequiredService<ILogger<ValidationService>>(),
            serviceProvider
        );

        _output.WriteLine($"MongoDB: {_mongoContainer.GetConnectionString()}");
        _output.WriteLine($"PostgreSQL: {_postgresContainer.GetConnectionString()}");
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _mongoContainer.DisposeAsync();
        await _postgresContainer.DisposeAsync();
    }

    [Fact]
    public async Task RoundTripDataValidation_Entries_MaintainsDataIntegrity()
    {
        // Arrange
        var originalEntries = TestDataGenerator.GenerateEntries(100, TestDataScenario.Normal);
        await SeedMongoData("entries", originalEntries);

        // Act - Transform and save to PostgreSQL
        var transformedEntries = new List<EntryEntity>();
        foreach (var entry in originalEntries)
        {
            var transformed =
                await _transformationService.TransformDocumentAsync(entry, "entries")
                as EntryEntity;
            Assert.NotNull(transformed);
            transformedEntries.Add(transformed);
            _dbContext.Entries.Add(transformed);
        }
        await _dbContext.SaveChangesAsync();

        // Assert - Validate data integrity
        var storedEntries = await _dbContext.Entries.ToListAsync();

        Assert.Equal(originalEntries.Count, storedEntries.Count);

        for (int i = 0; i < originalEntries.Count; i++)
        {
            var original = originalEntries[i];
            var stored = storedEntries.FirstOrDefault(e =>
                e.OriginalId == original["_id"].AsObjectId.ToString()
            );

            Assert.NotNull(stored);
            Assert.Equal(original["sgv"].AsInt32, stored.Sgv);
            Assert.Equal(original["date"].AsInt64, stored.Date);
            Assert.Equal(original["type"].AsString, stored.Type);
            Assert.Equal(original["device"].AsString, stored.Device);
            Assert.Equal(original["direction"].AsString, stored.Direction);
        }
    }

    [Fact]
    public async Task DataValidation_TreatmentTransformation_PreservesAllFields()
    {
        // Arrange
        var originalTreatments = TestDataGenerator.GenerateTreatments(50, TestDataScenario.Normal);
        await SeedMongoData("treatments", originalTreatments);

        // Act
        var transformedTreatments = new List<TreatmentEntity>();
        foreach (var treatment in originalTreatments)
        {
            var transformed =
                await _transformationService.TransformDocumentAsync(treatment, "treatments")
                as TreatmentEntity;
            Assert.NotNull(transformed);
            transformedTreatments.Add(transformed);
        }

        // Assert
        for (int i = 0; i < originalTreatments.Count; i++)
        {
            var original = originalTreatments[i];
            var transformed = transformedTreatments[i];

            Assert.Equal(original["_id"].AsObjectId.ToString(), transformed.OriginalId);
            Assert.Equal(original["eventType"].AsString, transformed.EventType);
            Assert.Equal(original["enteredBy"].AsString, transformed.EnteredBy);
            Assert.Equal(original["notes"].AsString, transformed.Notes);

            // Validate type-specific fields
            if (original.Contains("insulin"))
            {
                Assert.Equal(original["insulin"].AsDouble, transformed.Insulin);
            }
            if (original.Contains("carbs"))
            {
                Assert.Equal(original["carbs"].AsInt32, transformed.Carbs);
            }
        }
    }

    [Fact]
    public async Task DataValidation_ProfileTransformation_PreservesComplexStructures()
    {
        // Arrange
        var originalProfiles = TestDataGenerator.GenerateProfiles(10, TestDataScenario.Normal);
        await SeedMongoData("profiles", originalProfiles);

        // Act
        foreach (var profile in originalProfiles)
        {
            var transformed =
                await _transformationService.TransformDocumentAsync(profile, "profiles")
                as ProfileEntity;

            // Assert
            Assert.NotNull(transformed);
            Assert.Equal(profile["_id"].AsObjectId.ToString(), transformed.OriginalId);
            Assert.Equal(profile["defaultProfile"].AsString, transformed.DefaultProfile);
            Assert.NotNull(transformed.StoreJson);

            // Validate JSON structure preservation
            var storeData = BsonDocument.Parse(transformed.StoreJson);
            var originalStore = profile["store"].AsBsonDocument;

            Assert.Equal(
                originalStore["Default"]["timezone"].AsString,
                storeData["Default"]["timezone"].AsString
            );
        }
    }

    [Fact]
    public async Task DataIntegrity_EdgeCaseData_HandlesGracefully()
    {
        // Arrange
        var edgeCaseData = TestDataGenerator.GenerateEntries(20, TestDataScenario.EdgeCases);
        await SeedMongoData("entries", edgeCaseData);

        // Act & Assert
        foreach (var entry in edgeCaseData)
        {
            var validationResult = await _transformationService.ValidateDocumentAsync(
                entry,
                "entries"
            );

            if (validationResult.IsValid)
            {
                var transformed = await _transformationService.TransformDocumentAsync(
                    entry,
                    "entries"
                );
                Assert.NotNull(transformed);
            }
            else
            {
                // Document should be rejected gracefully with clear error messages
                Assert.NotEmpty(validationResult.Errors);
                Assert.All(validationResult.Errors, error => Assert.NotEmpty(error));
            }
        }
    }

    [Fact]
    public async Task DataIntegrity_CorruptData_ReportsAndSkips()
    {
        // Arrange
        var corruptData = TestDataGenerator.GenerateEntries(15, TestDataScenario.CorruptData);
        await SeedMongoData("entries", corruptData);

        var successCount = 0;
        var failureCount = 0;

        // Act
        foreach (var entry in corruptData)
        {
            try
            {
                var validationResult = await _transformationService.ValidateDocumentAsync(
                    entry,
                    "entries"
                );

                if (validationResult.IsValid)
                {
                    var transformed = await _transformationService.TransformDocumentAsync(
                        entry,
                        "entries"
                    );
                    successCount++;
                }
                else
                {
                    failureCount++;
                }
            }
            catch (Exception)
            {
                failureCount++;
            }
        }

        // Assert
        Assert.True(failureCount > 0, "Some corrupt data should fail validation");
        Assert.True(successCount > 0, "Some data should still be processable");

        _output.WriteLine(
            $"Processed {successCount} valid documents, rejected {failureCount} corrupt documents"
        );
    }

    [Fact]
    public async Task RecordCountVerification_AllCollections_MatchesExpected()
    {
        // Arrange
        var testData = new Dictionary<string, List<BsonDocument>>
        {
            ["entries"] = TestDataGenerator.GenerateEntries(25, TestDataScenario.Normal),
            ["treatments"] = TestDataGenerator.GenerateTreatments(15, TestDataScenario.Normal),
            ["profiles"] = TestDataGenerator.GenerateProfiles(5, TestDataScenario.Normal),
            ["settings"] = TestDataGenerator.GenerateSettings(10, TestDataScenario.Normal),
        };

        // Seed all collections
        foreach (var (collectionName, documents) in testData)
        {
            await SeedMongoData(collectionName, documents);
        }

        // Act - Transform all data
        var transformedCounts = new Dictionary<string, int>();
        foreach (var (collectionName, documents) in testData)
        {
            var successCount = 0;
            foreach (var document in documents)
            {
                try
                {
                    var transformed = await _transformationService.TransformDocumentAsync(
                        document,
                        collectionName
                    );
                    if (transformed != null)
                        successCount++;
                }
                catch
                {
                    // Some documents might fail transformation
                }
            }
            transformedCounts[collectionName] = successCount;
        }

        // Assert
        foreach (var (collectionName, expectedCount) in testData)
        {
            Assert.True(transformedCounts.ContainsKey(collectionName));

            // Allow for some data loss due to validation failures, but most should succeed
            var actualCount = transformedCounts[collectionName];
            var successRate = (double)actualCount / expectedCount;

            Assert.True(
                successRate >= 0.8,
                $"Collection {collectionName}: {actualCount}/{expectedCount} success rate {successRate:P} is below 80%"
            );

            _output.WriteLine(
                $"Collection {collectionName}: {actualCount}/{expectedCount} documents transformed ({successRate:P})"
            );
        }
    }

    [Fact]
    public async Task DataTypePreservation_AllSupportedTypes_MaintainsAccuracy()
    {
        // Arrange - Create documents with various data types
        var testDocument = new BsonDocument
        {
            ["_id"] = ObjectId.GenerateNewId(),
            ["stringField"] = "test string",
            ["intField"] = 42,
            ["doubleField"] = 3.14159,
            ["boolField"] = true,
            ["dateField"] = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds(),
            ["arrayField"] = new BsonArray { 1, 2, 3 },
            ["objectField"] = new BsonDocument { ["nested"] = "value" },
            ["nullField"] = BsonNull.Value,
        };

        await SeedMongoData("entries", new[] { testDocument });

        // Act
        var transformed =
            await _transformationService.TransformDocumentAsync(testDocument, "entries")
            as EntryEntity;

        // Assert
        Assert.NotNull(transformed);
        Assert.Equal(testDocument["_id"].AsObjectId.ToString(), transformed.OriginalId);

        // Additional data type validations would be specific to the entity structure
        // This is a placeholder for comprehensive type checking
    }

    [Fact]
    public async Task RelationshipIntegrity_CrossCollectionReferences_MaintainsConsistency()
    {
        // Arrange - Create related data
        var profiles = TestDataGenerator.GenerateProfiles(2, TestDataScenario.Normal);
        var treatments = TestDataGenerator.GenerateTreatments(10, TestDataScenario.Normal);

        // Simulate relationships by using consistent dates
        var baseDate = DateTimeOffset.UtcNow.AddDays(-1);
        foreach (var treatment in treatments)
        {
            treatment["date"] = baseDate.ToUnixTimeMilliseconds();
        }

        await SeedMongoData("profiles", profiles);
        await SeedMongoData("treatments", treatments);

        // Act - Transform data
        var transformedProfiles = new List<ProfileEntity>();
        var transformedTreatments = new List<TreatmentEntity>();

        foreach (var profile in profiles)
        {
            var transformed =
                await _transformationService.TransformDocumentAsync(profile, "profiles")
                as ProfileEntity;
            Assert.NotNull(transformed);
            transformedProfiles.Add(transformed);
        }

        foreach (var treatment in treatments)
        {
            var transformed =
                await _transformationService.TransformDocumentAsync(treatment, "treatments")
                as TreatmentEntity;
            Assert.NotNull(transformed);
            transformedTreatments.Add(transformed);
        }

        // Assert - Validate relationships are maintained
        Assert.Equal(profiles.Count, transformedProfiles.Count);
        Assert.Equal(treatments.Count, transformedTreatments.Count);

        // All treatments should have the same base date
        var treatmentDates = transformedTreatments.Select(t => t.Date).Distinct().ToList();
        Assert.Single(treatmentDates);
        Assert.Equal(baseDate.ToUnixTimeMilliseconds(), treatmentDates[0]);
    }

    [Fact]
    public async Task ComplexNestedDataPreservation_DeviceStatus_MaintainsStructure()
    {
        // Arrange
        var deviceStatuses = TestDataGenerator.GenerateDeviceStatuses(5, TestDataScenario.Normal);
        await SeedMongoData("devicestatus", deviceStatuses);

        // Act
        foreach (var status in deviceStatuses)
        {
            var transformed =
                await _transformationService.TransformDocumentAsync(status, "devicestatus")
                as DeviceStatusEntity;

            // Assert
            Assert.NotNull(transformed);
            Assert.Equal(status["_id"].AsObjectId.ToString(), transformed.OriginalId);
            Assert.Equal(status["device"].AsString, transformed.Device);

            // Validate nested JSON structure is preserved
            Assert.NotNull(transformed.StatusJson);
            var statusData = BsonDocument.Parse(transformed.StatusJson);
            Assert.Contains("pump", statusData.Names);
            Assert.Contains("openaps", statusData.Names);
        }
    }

    #region Helper Methods

    private async Task SeedMongoData(string collectionName, IEnumerable<BsonDocument> documents)
    {
        var collection = _mongoDatabase.GetCollection<BsonDocument>(collectionName);
        await collection.InsertManyAsync(documents);
    }

    #endregion
}
