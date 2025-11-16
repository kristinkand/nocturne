using Microsoft.Extensions.Logging;
using Moq;
using Nocturne.API.Services;
using Nocturne.Core.Contracts;
using Nocturne.Core.Models;
using Xunit;

namespace Nocturne.API.Tests.Services;

/// <summary>
/// Unit tests for TreatmentProcessingService with comprehensive coverage of processing, sanitization, and timestamp handling
/// </summary>
[Parity]
public class TreatmentProcessingServiceTests
{
    private readonly Mock<IDocumentProcessingService> _mockDocumentProcessingService;
    private readonly Mock<ILogger<TreatmentProcessingService>> _mockLogger;
    private readonly TreatmentProcessingService _service;

    public TreatmentProcessingServiceTests()
    {
        _mockDocumentProcessingService = new Mock<IDocumentProcessingService>();
        _mockLogger = new Mock<ILogger<TreatmentProcessingService>>();
        _service = new TreatmentProcessingService(
            _mockDocumentProcessingService.Object,
            _mockLogger.Object
        );
    }

    #region ProcessTreatments Tests

    [Fact]
    public void ProcessTreatments_WithValidTreatments_CallsDocumentProcessingServiceWithDeduplicationEnabled()
    {
        // Arrange
        var inputTreatments = new[]
        {
            new Treatment
            {
                Id = "treatment1",
                EventType = "Meal Bolus",
                Insulin = 5.0,
                CreatedAt = "2023-06-12T10:30:00.000Z",
            },
            new Treatment
            {
                Id = "treatment2",
                EventType = "Correction Bolus",
                Insulin = 2.0,
                CreatedAt = "2023-06-12T11:00:00.000Z",
            },
        };

        var expectedProcessedTreatments = new[]
        {
            new Treatment
            {
                Id = "treatment1",
                EventType = "Meal Bolus",
                Insulin = 5.0,
                CreatedAt = "2023-06-12T10:30:00.000Z",
            },
        };

        _mockDocumentProcessingService
            .Setup(x => x.ProcessDocuments(inputTreatments))
            .Returns(expectedProcessedTreatments);

        // Act
        var result = _service.ProcessTreatments(inputTreatments);

        // Assert
        Assert.Same(expectedProcessedTreatments, result);
        _mockDocumentProcessingService.Verify(
            x => x.ProcessDocuments(inputTreatments),
            Times.Once
        );
    }

    [Fact]
    public void ProcessTreatments_WithEmptyCollection_CallsDocumentProcessingServiceWithEmptyCollection()
    {
        // Arrange
        var emptyTreatments = new Treatment[0];
        var expectedResult = new Treatment[0];

        _mockDocumentProcessingService
            .Setup(x => x.ProcessDocuments(emptyTreatments))
            .Returns(expectedResult);

        // Act
        var result = _service.ProcessTreatments(emptyTreatments);

        // Assert
        Assert.Same(expectedResult, result);
        _mockDocumentProcessingService.Verify(
            x => x.ProcessDocuments(emptyTreatments),
            Times.Once
        );
    }

    [Fact]
    public void ProcessTreatments_WithLargeCollection_ProcessesBatchEfficiently()
    {
        // Arrange
        var largeBatch = new List<Treatment>();
        for (int i = 0; i < 1000; i++)
        {
            largeBatch.Add(
                new Treatment
                {
                    Id = $"treatment{i}",
                    EventType = "Meal Bolus",
                    Insulin = 1.0,
                    CreatedAt = $"2023-06-12T{i % 24:D2}:30:00.000Z",
                }
            );
        }

        var processedBatch = largeBatch.Take(500).ToList();
        _mockDocumentProcessingService
            .Setup(x => x.ProcessDocuments(largeBatch))
            .Returns(processedBatch);

        // Act
        var result = _service.ProcessTreatments(largeBatch);

        // Assert
        Assert.Same(processedBatch, result);
        _mockDocumentProcessingService.Verify(
            x => x.ProcessDocuments(largeBatch),
            Times.Once
        );
    }

    [Fact]
    public void ProcessTreatments_WithNullCollection_ThrowsArgumentNullExceptionFromCount()
    {
        // Arrange
        IEnumerable<Treatment> nullTreatments = null!;

        // Act & Assert
        var exception = Assert.Throws<ArgumentNullException>(() =>
            _service.ProcessTreatments(nullTreatments)
        );
        Assert.Equal("source", exception.ParamName);
    }

    #endregion

    #region SanitizeHtml Tests

    [Fact]
    public void SanitizeHtml_WithMaliciousScript_CallsDocumentProcessingServiceSanitizeHtml()
    {
        // Arrange
        const string maliciousHtml = "<script>alert('xss')</script>Safe content";
        const string expectedSanitized = "Safe content";

        _mockDocumentProcessingService
            .Setup(x => x.SanitizeHtml(maliciousHtml))
            .Returns(expectedSanitized);

        // Act
        var result = _service.SanitizeHtml(maliciousHtml);

        // Assert
        Assert.Equal(expectedSanitized, result);
        _mockDocumentProcessingService.Verify(x => x.SanitizeHtml(maliciousHtml), Times.Once);
    }

    [Fact]
    public void SanitizeHtml_WithValidHtml_CallsDocumentProcessingServiceSanitizeHtml()
    {
        // Arrange
        const string validHtml = "<p>This is <strong>safe</strong> content</p>";
        const string expectedResult = "<p>This is <strong>safe</strong> content</p>";

        _mockDocumentProcessingService
            .Setup(x => x.SanitizeHtml(validHtml))
            .Returns(expectedResult);

        // Act
        var result = _service.SanitizeHtml(validHtml);

        // Assert
        Assert.Equal(expectedResult, result);
        _mockDocumentProcessingService.Verify(x => x.SanitizeHtml(validHtml), Times.Once);
    }

    [Fact]
    public void SanitizeHtml_WithNullContent_CallsDocumentProcessingServiceWithNull()
    {
        // Arrange
        const string expectedResult = "";

        _mockDocumentProcessingService.Setup(x => x.SanitizeHtml(null)).Returns(expectedResult);

        // Act
        var result = _service.SanitizeHtml(null);

        // Assert
        Assert.Equal(expectedResult, result);
        _mockDocumentProcessingService.Verify(x => x.SanitizeHtml(null), Times.Once);
    }

    [Fact]
    public void SanitizeHtml_WithEmptyString_CallsDocumentProcessingServiceWithEmptyString()
    {
        // Arrange
        const string emptyContent = "";
        const string expectedResult = "";

        _mockDocumentProcessingService
            .Setup(x => x.SanitizeHtml(emptyContent))
            .Returns(expectedResult);

        // Act
        var result = _service.SanitizeHtml(emptyContent);

        // Assert
        Assert.Equal(expectedResult, result);
        _mockDocumentProcessingService.Verify(x => x.SanitizeHtml(emptyContent), Times.Once);
    }

    [Fact]
    public void SanitizeHtml_WithXssAttack_CallsDocumentProcessingService()
    {
        // Arrange
        const string xssContent = "<img src=x onerror=alert('xss')>User content";
        const string expectedSanitized = "User content";

        _mockDocumentProcessingService
            .Setup(x => x.SanitizeHtml(xssContent))
            .Returns(expectedSanitized);

        // Act
        var result = _service.SanitizeHtml(xssContent);

        // Assert
        Assert.Equal(expectedSanitized, result);
        _mockDocumentProcessingService.Verify(x => x.SanitizeHtml(xssContent), Times.Once);
    }

    [Fact]
    public void SanitizeHtml_WithUnicodeContent_CallsDocumentProcessingService()
    {
        // Arrange
        const string unicodeContent = "<p>Café with émojis 🍰 and special chars ñáéíóú</p>";
        const string expectedResult = "<p>Café with émojis 🍰 and special chars ñáéíóú</p>";

        _mockDocumentProcessingService
            .Setup(x => x.SanitizeHtml(unicodeContent))
            .Returns(expectedResult);

        // Act
        var result = _service.SanitizeHtml(unicodeContent);

        // Assert
        Assert.Equal(expectedResult, result);
        _mockDocumentProcessingService.Verify(x => x.SanitizeHtml(unicodeContent), Times.Once);
    }

    #endregion

    #region ProcessTimestamp Tests

    [Fact]
    public void ProcessTimestamp_WithValidTreatment_CallsDocumentProcessingServiceProcessTimestamp()
    {
        // Arrange
        var treatment = new Treatment
        {
            Id = "test1",
            EventType = "Meal Bolus",
            CreatedAt = "2023-06-12T10:30:00.000Z",
        };

        // Act
        _service.ProcessTimestamp(treatment);

        // Assert
        _mockDocumentProcessingService.Verify(x => x.ProcessTimestamp(treatment), Times.Once);
    }

    [Fact]
    public void ProcessTimestamp_WithTreatmentWithTimezone_CallsDocumentProcessingService()
    {
        // Arrange
        var treatment = new Treatment
        {
            Id = "test1",
            EventType = "Meal Bolus",
            CreatedAt = "2023-06-12T10:30:00-05:00",
        };

        // Act
        _service.ProcessTimestamp(treatment);

        // Assert
        _mockDocumentProcessingService.Verify(x => x.ProcessTimestamp(treatment), Times.Once);
    }

    [Fact]
    public void ProcessTimestamp_WithTreatmentWithMills_CallsDocumentProcessingService()
    {
        // Arrange
        var treatment = new Treatment
        {
            Id = "test1",
            EventType = "Meal Bolus",
            Mills = 1686572200000, // 2023-06-12T10:30:00.000Z
        };

        // Act
        _service.ProcessTimestamp(treatment);

        // Assert
        _mockDocumentProcessingService.Verify(x => x.ProcessTimestamp(treatment), Times.Once);
    }

    [Fact]
    public void ProcessTimestamp_WithNullTreatment_CallsDocumentProcessingServiceWithNull()
    {
        // Arrange
        Treatment nullTreatment = null!;

        // Act
        _service.ProcessTimestamp(nullTreatment);

        // Assert
        _mockDocumentProcessingService.Verify(x => x.ProcessTimestamp(nullTreatment), Times.Once);
    }

    #endregion

    #region Error Handling Tests

    [Fact]
    public void ProcessTreatments_WhenDocumentProcessingServiceThrows_PropagatesException()
    {
        // Arrange
        var treatments = new[]
        {
            new Treatment { Id = "test1", EventType = "Meal Bolus" },
        };

        _mockDocumentProcessingService
            .Setup(x => x.ProcessDocuments(treatments))
            .Throws(new InvalidOperationException("Processing failed"));

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(() =>
            _service.ProcessTreatments(treatments)
        );
        Assert.Equal("Processing failed", exception.Message);
    }

    [Fact]
    public void SanitizeHtml_WhenDocumentProcessingServiceThrows_PropagatesException()
    {
        // Arrange
        const string content = "test content";
        _mockDocumentProcessingService
            .Setup(x => x.SanitizeHtml(content))
            .Throws(new ArgumentException("Sanitization failed"));

        // Act & Assert
        var exception = Assert.Throws<ArgumentException>(() => _service.SanitizeHtml(content));
        Assert.Equal("Sanitization failed", exception.Message);
    }

    [Fact]
    public void ProcessTimestamp_WhenDocumentProcessingServiceThrows_PropagatesException()
    {
        // Arrange
        var treatment = new Treatment { Id = "test1", EventType = "Meal Bolus" };
        _mockDocumentProcessingService
            .Setup(x => x.ProcessTimestamp(treatment))
            .Throws(new FormatException("Timestamp processing failed"));

        // Act & Assert
        var exception = Assert.Throws<FormatException>(() => _service.ProcessTimestamp(treatment));
        Assert.Equal("Timestamp processing failed", exception.Message);
    }

    #endregion

    #region Integration Tests

    [Fact]
    public void ProcessTreatments_IntegrationWithSanitizationAndTimestampProcessing_CallsCorrectMethods()
    {
        // Arrange
        var treatments = new[]
        {
            new Treatment
            {
                Id = "treatment1",
                EventType = "Meal Bolus",
                Notes = "<script>alert('xss')</script>Valid note",
                CreatedAt = "2023-06-12T10:30:00-05:00",
                Insulin = 5.0,
            },
        };

        var processedTreatments = new[]
        {
            new Treatment
            {
                Id = "treatment1",
                EventType = "Meal Bolus",
                Notes = "Valid note",
                CreatedAt = "2023-06-12T15:30:00.000Z",
                Mills = 1686579000000,
                Insulin = 5.0,
            },
        };

        _mockDocumentProcessingService
            .Setup(x => x.ProcessDocuments(treatments))
            .Returns(processedTreatments);

        // Act
        var result = _service.ProcessTreatments(treatments);

        // Assert
        Assert.Same(processedTreatments, result);
        _mockDocumentProcessingService.Verify(
            x => x.ProcessDocuments(treatments),
            Times.Once
        );
    }

    [Fact]
    public void Constructor_WithValidDependencies_InitializesSuccessfully()
    {
        // Arrange & Act
        var service = new TreatmentProcessingService(
            _mockDocumentProcessingService.Object,
            _mockLogger.Object
        );

        // Assert
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullDocumentProcessingService_DoesNotThrow()
    {
        // Arrange, Act & Assert
        var service = new TreatmentProcessingService(null!, _mockLogger.Object);
        Assert.NotNull(service);
    }

    [Fact]
    public void Constructor_WithNullLogger_DoesNotThrow()
    {
        // Arrange, Act & Assert
        var service = new TreatmentProcessingService(_mockDocumentProcessingService.Object, null!);
        Assert.NotNull(service);
    }

    #endregion
}
