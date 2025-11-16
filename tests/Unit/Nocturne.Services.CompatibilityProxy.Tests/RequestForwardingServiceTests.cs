using System.Text;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Moq;
using Nocturne.Core.Models;
using Nocturne.Services.CompatibilityProxy.Configuration;
using Nocturne.Services.CompatibilityProxy.Models;
using Nocturne.Services.CompatibilityProxy.Services;
using Xunit;

namespace Nocturne.Services.CompatibilityProxy.Tests.Unit;

public class RequestForwardingServiceTests
{
    private readonly Mock<IHttpClientFactory> _httpClientFactoryMock;
    private readonly Mock<ICorrelationService> _correlationServiceMock;
    private readonly Mock<IResponseComparisonService> _responseComparisonServiceMock;
    private readonly Mock<IResponseCacheService> _responseCacheServiceMock;
    private readonly Mock<IDiscrepancyPersistenceService> _discrepancyPersistenceServiceMock;
    private readonly ILogger<RequestForwardingService> _logger;

    public RequestForwardingServiceTests()
    {
        _httpClientFactoryMock = new Mock<IHttpClientFactory>();
        _correlationServiceMock = new Mock<ICorrelationService>();
        _responseComparisonServiceMock = new Mock<IResponseComparisonService>();
        _responseCacheServiceMock = new Mock<IResponseCacheService>();
        _discrepancyPersistenceServiceMock = new Mock<IDiscrepancyPersistenceService>();
        _logger = new LoggerFactory().CreateLogger<RequestForwardingService>();
    }

    private RequestForwardingService CreateService(CompatibilityProxyConfiguration config)
    {
        var options = Options.Create(config);
        return new RequestForwardingService(
            _httpClientFactoryMock.Object,
            options,
            _logger,
            _correlationServiceMock.Object,
            _responseComparisonServiceMock.Object,
            _responseCacheServiceMock.Object,
            _discrepancyPersistenceServiceMock.Object
        );
    }

    [Theory]
    [InlineData(0, "Default strategy: Nightscout")] // A/B testing disabled
    [InlineData(100, "Strategy: Nocturne")] // A/B testing at 100%
    public void SelectABTestResponse_VariousPercentages_ShouldSelectCorrectly(
        int percentage,
        string expectedReason
    )
    {
        // Arrange
        var config = new CompatibilityProxyConfiguration
        {
            DefaultStrategy = ResponseSelectionStrategy.ABTest,
            ABTestingPercentage = percentage,
        };

        var service = CreateService(config);
        var response = new CompatibilityProxyResponse
        {
            NightscoutResponse = new TargetResponse
            {
                Target = "Nightscout",
                StatusCode = 200,
                IsSuccess = true,
                Body = Encoding.UTF8.GetBytes("""{"from": "nightscout"}"""),
            },
            NocturneResponse = new TargetResponse
            {
                Target = "Nocturne",
                StatusCode = 200,
                IsSuccess = true,
                Body = Encoding.UTF8.GetBytes("""{"from": "nocturne"}"""),
            },
            CorrelationId = "test-correlation-123",
        };

        // Use reflection to access the private SelectResponse method
        var selectResponseMethod = typeof(RequestForwardingService).GetMethod(
            "SelectResponse",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );

        // Act
        var selectedResponse = (TargetResponse)
            selectResponseMethod!.Invoke(service, new object[] { response })!;

        // Assert
        Assert.NotNull(selectedResponse);
        if (percentage == 0)
        {
            Assert.Equal("Nightscout", selectedResponse.Target);
        }
        else if (percentage == 100)
        {
            Assert.Equal("Nocturne", selectedResponse.Target);
        }
        Assert.Contains(expectedReason.Split(':')[0], response.SelectionReason);
    }

    [Fact]
    public void SelectComparedResponse_WithCriticalDifferences_ShouldLogWarningAndSelectNightscout()
    {
        // Arrange
        var config = new CompatibilityProxyConfiguration
        {
            DefaultStrategy = ResponseSelectionStrategy.Compare,
        };

        var service = CreateService(config);
        var comparisonResult = new ResponseComparisonResult
        {
            CorrelationId = "test-correlation",
            OverallMatch = Nocturne.Core.Models.ResponseMatchType.CriticalDifferences,
            Summary = "Critical differences detected",
        };

        var response = new CompatibilityProxyResponse
        {
            NightscoutResponse = new TargetResponse
            {
                Target = "Nightscout",
                StatusCode = 200,
                IsSuccess = true,
            },
            NocturneResponse = new TargetResponse
            {
                Target = "Nocturne",
                StatusCode = 404,
                IsSuccess = false,
            },
            ComparisonResult = comparisonResult,
        };

        // Use reflection to access the private SelectResponse method
        var selectResponseMethod = typeof(RequestForwardingService).GetMethod(
            "SelectResponse",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );

        // Act
        var selectedResponse = (TargetResponse)
            selectResponseMethod!.Invoke(service, new object[] { response })!;

        // Assert
        Assert.Equal("Nightscout", selectedResponse.Target);
        Assert.Contains("Critical differences detected", response.SelectionReason);
    }

    [Fact]
    public void SelectComparedResponse_WithPerfectMatch_ShouldUseFastestStrategy()
    {
        // Arrange
        var config = new CompatibilityProxyConfiguration
        {
            DefaultStrategy = ResponseSelectionStrategy.Compare,
        };

        var service = CreateService(config);
        var comparisonResult = new ResponseComparisonResult
        {
            CorrelationId = "test-correlation",
            OverallMatch = Nocturne.Core.Models.ResponseMatchType.Perfect,
            Summary = "Responses match perfectly",
        };

        var response = new CompatibilityProxyResponse
        {
            NightscoutResponse = new TargetResponse
            {
                Target = "Nightscout",
                StatusCode = 200,
                IsSuccess = true,
                ResponseTimeMs = 150,
            },
            NocturneResponse = new TargetResponse
            {
                Target = "Nocturne",
                StatusCode = 200,
                IsSuccess = true,
                ResponseTimeMs = 100, // Faster
            },
            ComparisonResult = comparisonResult,
        };

        // Use reflection to access the private SelectResponse method
        var selectResponseMethod = typeof(RequestForwardingService).GetMethod(
            "SelectResponse",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );

        // Act
        var selectedResponse = (TargetResponse)
            selectResponseMethod!.Invoke(service, new object[] { response })!;

        // Assert
        Assert.Equal("Nocturne", selectedResponse.Target); // Should select faster response
        Assert.Contains("Fastest", response.SelectionReason);
    }

    [Theory]
    [InlineData("/api/v1/entries/slow", 60)]
    [InlineData("/api/v1/status", 5)]
    [InlineData("/api/v1/data", 30)]
    public void GetTimeoutForEndpoint_VariousPaths_ShouldReturnCorrectTimeout(
        string path,
        int expectedTimeout
    )
    {
        // Arrange
        var config = new CompatibilityProxyConfiguration
        {
            TimeoutSeconds = 30,
            EndpointTimeouts = new Dictionary<string, int> { ["entries"] = 60, ["status"] = 5 },
        };

        var service = CreateService(config);

        // Use reflection to access the private GetTimeoutForEndpoint method
        var getTimeoutMethod = typeof(RequestForwardingService).GetMethod(
            "GetTimeoutForEndpoint",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );

        // Act
        var timeout = (int)getTimeoutMethod!.Invoke(service, new object[] { path })!;

        // Assert
        Assert.Equal(expectedTimeout, timeout);
    }

    [Fact]
    public void FilterSensitiveErrorMessage_ContainsSensitiveData_ShouldRedactFieldNames()
    {
        // Arrange
        var config = new CompatibilityProxyConfiguration
        {
            SensitiveFields = new List<string> { "api_secret", "token", "password" },
        };

        var service = CreateService(config);
        var errorMessage = "Authentication failed with api_secret=12345 and token=abcdef";

        // Use reflection to access the private FilterSensitiveErrorMessage method
        var filterMethod = typeof(RequestForwardingService).GetMethod(
            "FilterSensitiveErrorMessage",
            System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance
        );

        // Act
        var filtered = (string)filterMethod!.Invoke(service, new object[] { errorMessage })!;

        // Assert
        Assert.Contains("[REDACTED]", filtered);
        Assert.DoesNotContain("api_secret", filtered);
        Assert.DoesNotContain("token", filtered);
        // Note: The actual values (12345, abcdef) are not redacted in the current implementation
        // This is because the method only replaces field names, not field values
    }
}
