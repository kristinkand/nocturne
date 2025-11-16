using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using Nocturne.Tools.Migration.Models;
using Nocturne.Tools.Migration.Services;
using Xunit;

namespace Nocturne.Tools.Migration.Tests.Services;

/// <summary>
/// Tests for the index optimization service
/// </summary>
public class IndexOptimizationServiceTests
{
    private readonly IIndexOptimizationService _indexOptimizationService;
    private readonly ILogger<IndexOptimizationService> _logger;

    public IndexOptimizationServiceTests()
    {
        _logger = NullLogger<IndexOptimizationService>.Instance;
        _indexOptimizationService = new IndexOptimizationService(_logger);
    }

    [Fact]
    public async Task CreateCollectionSpecificStrategiesAsync_ForEntries_ReturnsOptimizedStrategies()
    {
        // Arrange
        var options = new IndexOptimizationOptions
        {
            EnableTimeSeriesOptimizations = true,
            CreateCoveringIndexes = true,
            CreatePartialIndexes = true,
        };

        // Act
        var strategies = await _indexOptimizationService.CreateCollectionSpecificStrategiesAsync(
            "entries",
            options
        );

        // Assert
        Assert.NotEmpty(strategies);

        var strategiesList = strategies.ToList();

        // Check for time-series optimization
        Assert.Contains(strategiesList, s => s.IndexName == "ix_entries_date_mills_type");

        // Check for covering index
        Assert.Contains(strategiesList, s => s.IndexName == "ix_entries_date_sgv_type_covering");

        // Check for partial index
        Assert.Contains(
            strategiesList,
            s => s.IndexName == "ix_entries_sgv_date_partial" && s.IsPartial
        );

        // Verify high priority strategies exist
        Assert.Contains(strategiesList, s => s.EstimatedBenefit == PerformanceBenefit.Critical);
    }

    [Fact]
    public async Task CreateCollectionSpecificStrategiesAsync_ForTreatments_IncludesJsonbIndexes()
    {
        // Arrange
        var options = new IndexOptimizationOptions();

        // Act
        var strategies = await _indexOptimizationService.CreateCollectionSpecificStrategiesAsync(
            "treatments",
            options
        );

        // Assert
        Assert.NotEmpty(strategies);

        var strategiesList = strategies.ToList();

        // Check for JSONB GIN indexes
        Assert.Contains(
            strategiesList,
            s => s.IndexType == IndexType.Gin && s.IndexName.Contains("additional_properties")
        );
        Assert.Contains(
            strategiesList,
            s => s.IndexType == IndexType.Gin && s.IndexName.Contains("boluscalc")
        );

        // Check for compound index
        Assert.Contains(strategiesList, s => s.IndexName == "ix_treatments_eventtype_date");
    }

    [Fact]
    public async Task CreateCollectionSpecificStrategiesAsync_ForAuth_CreatesUniqueIndexes()
    {
        // Arrange
        var options = new IndexOptimizationOptions();

        // Act
        var strategies = await _indexOptimizationService.CreateCollectionSpecificStrategiesAsync(
            "auth",
            options
        );

        // Assert
        Assert.NotEmpty(strategies);

        var strategiesList = strategies.ToList();

        // Check for unique username index
        var usernameIndex = strategiesList.FirstOrDefault(s =>
            s.IndexName == "ix_auth_username_unique"
        );
        Assert.NotNull(usernameIndex);
        Assert.True(usernameIndex.IsUnique);
        Assert.Equal(PerformanceBenefit.Critical, usernameIndex.EstimatedBenefit);

        // Check for unique email index
        var emailIndex = strategiesList.FirstOrDefault(s => s.IndexName == "ix_auth_email_unique");
        Assert.NotNull(emailIndex);
        Assert.True(emailIndex.IsUnique);

        // Check for JSONB indexes
        Assert.Contains(
            strategiesList,
            s => s.IndexType == IndexType.Gin && s.IndexName.Contains("roles")
        );
        Assert.Contains(
            strategiesList,
            s => s.IndexType == IndexType.Gin && s.IndexName.Contains("permissions")
        );
    }

    [Fact]
    public async Task CreateCollectionSpecificStrategiesAsync_ForFood_IncludesTextSearchIndex()
    {
        // Arrange
        var options = new IndexOptimizationOptions();

        // Act
        var strategies = await _indexOptimizationService.CreateCollectionSpecificStrategiesAsync(
            "food",
            options
        );

        // Assert
        Assert.NotEmpty(strategies);

        var strategiesList = strategies.ToList();

        // Check for food search optimization
        Assert.Contains(strategiesList, s => s.IndexName == "ix_food_name_category");

        // Check for text search index
        var textSearchIndex = strategiesList.FirstOrDefault(s => s.IndexName == "ix_food_name_gin");
        Assert.NotNull(textSearchIndex);
        Assert.Equal(IndexType.Gin, textSearchIndex.IndexType);
        Assert.Contains(
            textSearchIndex.Columns,
            c => c.Expression != null && c.Expression.Contains("to_tsvector")
        );
    }

    [Fact]
    public async Task CreateCollectionSpecificStrategiesAsync_WithDisabledPartialIndexes_DoesNotCreatePartialIndexes()
    {
        // Arrange
        var options = new IndexOptimizationOptions { CreatePartialIndexes = false };

        // Act
        var strategies = await _indexOptimizationService.CreateCollectionSpecificStrategiesAsync(
            "entries",
            options
        );

        // Assert
        Assert.NotEmpty(strategies);

        var strategiesList = strategies.ToList();

        // Verify no partial indexes are created
        Assert.DoesNotContain(strategiesList, s => s.IsPartial);
    }

    [Fact]
    public async Task CreateCollectionSpecificStrategiesAsync_WithDisabledTimeSeriesOptimizations_SkipsTimeSeriesIndexes()
    {
        // Arrange
        var options = new IndexOptimizationOptions { EnableTimeSeriesOptimizations = false };

        // Act
        var strategies = await _indexOptimizationService.CreateCollectionSpecificStrategiesAsync(
            "entries",
            options
        );

        // Assert
        Assert.NotEmpty(strategies);

        var strategiesList = strategies.ToList();

        // Should have fewer strategies without time-series optimizations
        Assert.DoesNotContain(strategiesList, s => s.IndexName == "ix_entries_date_mills_type");
    }

    [Fact]
    public async Task CreateCollectionSpecificStrategiesAsync_ForUnsupportedCollection_ReturnsEmpty()
    {
        // Arrange
        var options = new IndexOptimizationOptions();

        // Act
        var strategies = await _indexOptimizationService.CreateCollectionSpecificStrategiesAsync(
            "unsupported_collection",
            options
        );

        // Assert
        Assert.Empty(strategies);
    }

    [Theory]
    [InlineData("entries")]
    [InlineData("treatments")]
    [InlineData("profiles")]
    [InlineData("devicestatus")]
    [InlineData("food")]
    [InlineData("activity")]
    [InlineData("settings")]
    [InlineData("auth")]
    public async Task CreateCollectionSpecificStrategiesAsync_ForAllSupportedCollections_ReturnsStrategies(
        string collectionName
    )
    {
        // Arrange
        var options = new IndexOptimizationOptions();

        // Act
        var strategies = await _indexOptimizationService.CreateCollectionSpecificStrategiesAsync(
            collectionName,
            options
        );

        // Assert
        Assert.NotEmpty(strategies);

        var strategiesList = strategies.ToList();

        // All strategies should have valid properties
        Assert.All(
            strategiesList,
            strategy =>
            {
                Assert.NotEmpty(strategy.IndexName);
                Assert.NotEmpty(strategy.TableName);
                Assert.NotEmpty(strategy.Columns);
                Assert.NotNull(strategy.Description);
            }
        );
    }
}
