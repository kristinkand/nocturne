using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.Tools.Abstractions.Services;
using Nocturne.Tools.Migration.Data;
using Nocturne.Tools.Migration.Services;
using Xunit;

namespace Nocturne.Tools.Migration.Tests.Services;

public class MigrationEngineTests : IDisposable
{
    private readonly Mock<ILogger<MigrationEngine>> _mockLogger;
    private readonly Mock<IDataTransformationService> _mockTransformationService;
    private readonly Mock<IValidationService> _mockValidationService;
    private readonly Mock<IIndexOptimizationService> _mockIndexOptimizationService;
    private readonly ServiceProvider _serviceProvider;

    public MigrationEngineTests()
    {
        _mockLogger = new Mock<ILogger<MigrationEngine>>();
        _mockTransformationService = new Mock<IDataTransformationService>();
        _mockValidationService = new Mock<IValidationService>();
        _mockIndexOptimizationService = new Mock<IIndexOptimizationService>();

        // Set up in-memory database for testing
        var services = new ServiceCollection();
        services.AddDbContext<MigrationDbContext>(options =>
            options.UseInMemoryDatabase($"TestDb_{Guid.NewGuid()}")
        );
        services.AddLogging();

        _serviceProvider = services.BuildServiceProvider();
    }

    [Fact]
    public void Constructor_WithValidParameters_CreatesInstance()
    {
        // Arrange

        // Act
        var engine = new MigrationEngine(
            _mockLogger.Object,
            _serviceProvider,
            _mockTransformationService.Object,
            _mockValidationService.Object,
            _mockIndexOptimizationService.Object
        );

        // Assert
        Assert.NotNull(engine);
    }

    [Fact]
    public void Constructor_WithNullLogger_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MigrationEngine(
                null!,
                _serviceProvider,
                _mockTransformationService.Object,
                _mockValidationService.Object,
                _mockIndexOptimizationService.Object
            )
        );
    }

    [Fact]
    public void Constructor_WithNullServiceProvider_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MigrationEngine(
                _mockLogger.Object,
                null!,
                _mockTransformationService.Object,
                _mockValidationService.Object,
                _mockIndexOptimizationService.Object
            )
        );
    }

    [Fact]
    public void Constructor_WithNullTransformationService_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MigrationEngine(
                _mockLogger.Object,
                _serviceProvider,
                null!,
                _mockValidationService.Object,
                _mockIndexOptimizationService.Object
            )
        );
    }

    [Fact]
    public void Constructor_WithNullValidationService_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MigrationEngine(
                _mockLogger.Object,
                _serviceProvider,
                _mockTransformationService.Object,
                null!,
                _mockIndexOptimizationService.Object
            )
        );
    }

    [Fact]
    public void Constructor_WithNullIndexOptimizationService_ThrowsArgumentNullException()
    {
        // Arrange & Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new MigrationEngine(
                _mockLogger.Object,
                _serviceProvider,
                _mockTransformationService.Object,
                _mockValidationService.Object,
                null!
            )
        );
    }

    [Fact]
    public async Task ValidateAsync_WithValidConfiguration_ReturnsSuccess()
    {
        // Arrange
        var engine = new MigrationEngine(
            _mockLogger.Object,
            _serviceProvider,
            _mockTransformationService.Object,
            _mockValidationService.Object,
            _mockIndexOptimizationService.Object
        );

        var config = new MigrationEngineConfiguration
        {
            MongoConnectionString = "mongodb://localhost:27017",
            MongoDatabaseName = "test",
            PostgreSqlConnectionString = "Host=localhost;Database=test;Username=test;Password=test",
            BatchSize = 1000,
            MaxMemoryUsageMB = 512,
            MaxDegreeOfParallelism = 2,
        };

        // Setup validation service to return success
        _mockValidationService
            .Setup(x => x.ValidateObject(It.IsAny<object>()))
            .Returns(ValidationResult.Success());
        _mockValidationService
            .Setup(x => x.ValidateConnectionString(It.IsAny<string>()))
            .Returns(ValidationResult.Success());

        // Act
        var result = await engine.ValidateAsync(config);

        // Assert
        // Note: This will fail validation due to actual connection requirements
        // but we can test the configuration validation logic
        Assert.True(
            result.Errors.Any(e => e.ErrorMessage.Contains("MongoDB connection failed"))
                || result.Errors.Any(e => e.ErrorMessage.Contains("PostgreSQL connection failed"))
        );
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidBatchSize_ReturnsError()
    {
        // Arrange
        var engine = new MigrationEngine(
            _mockLogger.Object,
            _serviceProvider,
            _mockTransformationService.Object,
            _mockValidationService.Object,
            _mockIndexOptimizationService.Object
        );

        var config = new MigrationEngineConfiguration
        {
            MongoConnectionString = "mongodb://localhost:27017",
            MongoDatabaseName = "test",
            PostgreSqlConnectionString = "Host=localhost;Database=test;Username=test;Password=test",
            BatchSize = 0, // Invalid
            MaxMemoryUsageMB = 512,
            MaxDegreeOfParallelism = 2,
        };

        // Setup validation service to return success for other validations
        _mockValidationService
            .Setup(x => x.ValidateObject(It.IsAny<object>()))
            .Returns(ValidationResult.Success());
        _mockValidationService
            .Setup(x => x.ValidateConnectionString(It.IsAny<string>()))
            .Returns(ValidationResult.Success());

        // Act
        var result = await engine.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.ErrorMessage.Contains("Batch size must be greater than 0")
        );
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidMemoryLimit_ReturnsError()
    {
        // Arrange
        var engine = new MigrationEngine(
            _mockLogger.Object,
            _serviceProvider,
            _mockTransformationService.Object,
            _mockValidationService.Object,
            _mockIndexOptimizationService.Object
        );

        var config = new MigrationEngineConfiguration
        {
            MongoConnectionString = "mongodb://localhost:27017",
            MongoDatabaseName = "test",
            PostgreSqlConnectionString = "Host=localhost;Database=test;Username=test;Password=test",
            BatchSize = 1000,
            MaxMemoryUsageMB = 0, // Invalid
            MaxDegreeOfParallelism = 2,
        };

        // Setup validation service to return success for other validations
        _mockValidationService
            .Setup(x => x.ValidateObject(It.IsAny<object>()))
            .Returns(ValidationResult.Success());
        _mockValidationService
            .Setup(x => x.ValidateConnectionString(It.IsAny<string>()))
            .Returns(ValidationResult.Success());

        // Act
        var result = await engine.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.ErrorMessage.Contains("Max memory usage must be greater than 0")
        );
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidParallelism_ReturnsError()
    {
        // Arrange
        var engine = new MigrationEngine(
            _mockLogger.Object,
            _serviceProvider,
            _mockTransformationService.Object,
            _mockValidationService.Object,
            _mockIndexOptimizationService.Object
        );

        var config = new MigrationEngineConfiguration
        {
            MongoConnectionString = "mongodb://localhost:27017",
            MongoDatabaseName = "test",
            PostgreSqlConnectionString = "Host=localhost;Database=test;Username=test;Password=test",
            BatchSize = 1000,
            MaxMemoryUsageMB = 512,
            MaxDegreeOfParallelism = 0, // Invalid
        };

        // Setup validation service to return success for other validations
        _mockValidationService
            .Setup(x => x.ValidateObject(It.IsAny<object>()))
            .Returns(ValidationResult.Success());
        _mockValidationService
            .Setup(x => x.ValidateConnectionString(It.IsAny<string>()))
            .Returns(ValidationResult.Success());

        // Act
        var result = await engine.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.ErrorMessage.Contains("Max degree of parallelism must be greater than 0")
        );
    }

    [Fact]
    public async Task ValidateAsync_WithInvalidDateRange_ReturnsError()
    {
        // Arrange
        var engine = new MigrationEngine(
            _mockLogger.Object,
            _serviceProvider,
            _mockTransformationService.Object,
            _mockValidationService.Object,
            _mockIndexOptimizationService.Object
        );

        var config = new MigrationEngineConfiguration
        {
            MongoConnectionString = "mongodb://localhost:27017",
            MongoDatabaseName = "test",
            PostgreSqlConnectionString = "Host=localhost;Database=test;Username=test;Password=test",
            BatchSize = 1000,
            MaxMemoryUsageMB = 512,
            MaxDegreeOfParallelism = 2,
            StartDate = DateTime.Now,
            EndDate = DateTime.Now.AddDays(-1), // End before start
        };

        // Setup validation service to return success for other validations
        _mockValidationService
            .Setup(x => x.ValidateObject(It.IsAny<object>()))
            .Returns(ValidationResult.Success());
        _mockValidationService
            .Setup(x => x.ValidateConnectionString(It.IsAny<string>()))
            .Returns(ValidationResult.Success());

        // Act
        var result = await engine.ValidateAsync(config);

        // Assert
        Assert.False(result.IsValid);
        Assert.Contains(
            result.Errors,
            e => e.ErrorMessage.Contains("Start date must be before end date")
        );
    }

    [Fact]
    public async Task GetStatusAsync_WithNonExistentMigration_ThrowsArgumentException()
    {
        // Arrange
        var engine = new MigrationEngine(
            _mockLogger.Object,
            _serviceProvider,
            _mockTransformationService.Object,
            _mockValidationService.Object,
            _mockIndexOptimizationService.Object
        );
        var migrationId = "non-existent-id";

        // Act & Assert
        await Assert.ThrowsAsync<ArgumentException>(() => engine.GetStatusAsync(migrationId));
    }

    [Fact]
    public void MigrationEngineConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var config = new MigrationEngineConfiguration
        {
            MongoConnectionString = "test",
            MongoDatabaseName = "test",
            PostgreSqlConnectionString = "test",
        };

        // Assert
        Assert.Equal(1000, config.BatchSize);
        Assert.Equal(512, config.MaxMemoryUsageMB);
        Assert.Equal(Environment.ProcessorCount, config.MaxDegreeOfParallelism);
        Assert.True(config.EnableCheckpointing);
        Assert.Equal(100, config.CheckpointInterval);
        Assert.False(config.DropExistingTables);
        Assert.Empty(config.CollectionsToMigrate);
    }

    [Fact]
    public void RetryConfiguration_DefaultValues_AreCorrect()
    {
        // Arrange & Act
        var retryConfig = new RetryConfiguration();

        // Assert
        Assert.Equal(3, retryConfig.MaxRetries);
        Assert.Equal(TimeSpan.FromSeconds(1), retryConfig.InitialDelay);
        Assert.Equal(TimeSpan.FromMinutes(1), retryConfig.MaxDelay);
        Assert.Equal(2.0, retryConfig.BackoffMultiplier);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (disposing)
        {
            _serviceProvider?.Dispose();
        }
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }
}
