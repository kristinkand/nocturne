using System.Text;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Core.Models;
using Nocturne.Services.CompatibilityProxy.Configuration;
using Nocturne.Services.CompatibilityProxy.Models;
using Nocturne.Services.CompatibilityProxy.Services;
using Xunit;

namespace Nocturne.Services.CompatibilityProxy.Tests.Unit;

public class ResponseCacheServiceTests
{
    private readonly ResponseCacheService _service;
    private readonly IMemoryCache _memoryCache;
    private readonly ILogger<ResponseCacheService> _logger;

    public ResponseCacheServiceTests()
    {
        _memoryCache = new MemoryCache(new MemoryCacheOptions());
        _logger = new LoggerFactory().CreateLogger<ResponseCacheService>();
        var config = Options.Create(
            new CompatibilityProxyConfiguration
            {
                EnableResponseCaching = true,
                ResponseCacheTtlSeconds = 300,
                MaxResponseSizeForComparison = 1024 * 1024,
            }
        );
        _service = new ResponseCacheService(_memoryCache, config, _logger);
    }

    [Fact]
    public void GenerateCacheKey_ValidRequest_ShouldReturnHashedKey()
    {
        // Arrange
        var request = new ClonedRequest
        {
            Method = "GET",
            Path = "/api/v1/entries",
            Headers = new Dictionary<string, string[]>
            {
                ["Authorization"] = new[] { "Bearer token123" },
            },
        };

        // Act
        var cacheKey = _service.GenerateCacheKey(request);

        // Assert
        Assert.NotEmpty(cacheKey);
        Assert.StartsWith("compatibility_proxy_cache_", cacheKey);
    }

    [Fact]
    public void GenerateCacheKey_SameRequests_ShouldReturnSameKey()
    {
        // Arrange
        var request1 = new ClonedRequest
        {
            Method = "GET",
            Path = "/api/v1/entries",
            Headers = new Dictionary<string, string[]>
            {
                ["Authorization"] = new[] { "Bearer token123" },
            },
        };

        var request2 = new ClonedRequest
        {
            Method = "GET",
            Path = "/api/v1/entries",
            Headers = new Dictionary<string, string[]>
            {
                ["Authorization"] = new[] { "Bearer token123" },
            },
        };

        // Act
        var cacheKey1 = _service.GenerateCacheKey(request1);
        var cacheKey2 = _service.GenerateCacheKey(request2);

        // Assert
        Assert.Equal(cacheKey1, cacheKey2);
    }

    [Fact]
    public void GenerateCacheKey_DifferentAuth_ShouldReturnDifferentKeys()
    {
        // Arrange
        var request1 = new ClonedRequest
        {
            Method = "GET",
            Path = "/api/v1/entries",
            Headers = new Dictionary<string, string[]>
            {
                ["Authorization"] = new[] { "Bearer token123" },
            },
        };

        var request2 = new ClonedRequest
        {
            Method = "GET",
            Path = "/api/v1/entries",
            Headers = new Dictionary<string, string[]>
            {
                ["Authorization"] = new[] { "Bearer token456" },
            },
        };

        // Act
        var cacheKey1 = _service.GenerateCacheKey(request1);
        var cacheKey2 = _service.GenerateCacheKey(request2);

        // Assert
        Assert.NotEqual(cacheKey1, cacheKey2);
    }

    [Fact]
    public async Task GetCachedResponseAsync_NoCache_ShouldReturnNull()
    {
        // Act
        var result = await _service.GetCachedResponseAsync("non_existent_key");

        // Assert
        Assert.Null(result);
    }

    [Fact]
    public async Task SetAndGetCachedResponse_ValidResponse_ShouldCacheAndRetrieve()
    {
        // Arrange
        var cacheKey = "test_cache_key";
        var response = new CompatibilityProxyResponse
        {
            SelectedResponse = new TargetResponse
            {
                Target = "Nightscout",
                StatusCode = 200,
                IsSuccess = true,
                Body = Encoding.UTF8.GetBytes("""{"status": "ok"}"""),
                ContentType = "application/json",
            },
            CorrelationId = "test-correlation",
        };

        // Act
        await _service.SetCachedResponseAsync(cacheKey, response);
        var cached = await _service.GetCachedResponseAsync(cacheKey);

        // Assert
        Assert.NotNull(cached);
        Assert.Equal(response.CorrelationId, cached.CorrelationId);
        Assert.Equal(response.SelectedResponse.StatusCode, cached.SelectedResponse?.StatusCode);
    }

    [Fact]
    public async Task SetCachedResponse_ErrorResponse_ShouldNotCache()
    {
        // Arrange
        var cacheKey = "test_error_key";
        var response = new CompatibilityProxyResponse
        {
            SelectedResponse = new TargetResponse
            {
                Target = "Nightscout",
                StatusCode = 500,
                IsSuccess = false,
                ErrorMessage = "Internal Server Error",
            },
        };

        // Act
        await _service.SetCachedResponseAsync(cacheKey, response);
        var cached = await _service.GetCachedResponseAsync(cacheKey);

        // Assert
        Assert.Null(cached);
    }

    [Theory]
    [InlineData("GET", "", true)]
    [InlineData("HEAD", "", true)]
    [InlineData("OPTIONS", "", true)]
    [InlineData("POST", "", false)]
    [InlineData("PUT", "", false)]
    [InlineData("DELETE", "", false)]
    [InlineData("GET", "body content", false)]
    public void ShouldCacheRequest_VariousMethods_ShouldReturnExpectedResult(
        string method,
        string bodyContent,
        bool expected
    )
    {
        // Arrange
        var request = new ClonedRequest
        {
            Method = method,
            Path = "/api/v1/entries",
            Body = string.IsNullOrEmpty(bodyContent) ? null : Encoding.UTF8.GetBytes(bodyContent),
        };

        // Act
        var result = _service.ShouldCacheRequest(request);

        // Assert
        Assert.Equal(expected, result);
    }

    [Fact]
    public void ShouldCacheRequest_StatusEndpoint_ShouldNotCache()
    {
        // Arrange
        var request = new ClonedRequest { Method = "GET", Path = "/api/v1/status" };

        // Act
        var result = _service.ShouldCacheRequest(request);

        // Assert
        Assert.False(result);
    }
}
