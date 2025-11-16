using System.Diagnostics;
using Microsoft.Extensions.Options;
using Nocturne.API.Configuration;
using Nocturne.API.Models.Compatibility;

namespace Nocturne.API.Services.Compatibility;

/// <summary>
/// Service for forwarding requests to target systems
/// </summary>
public interface IRequestForwardingService
{
    /// <summary>
    /// Forward a cloned request to both target systems
    /// </summary>
    /// <param name="clonedRequest">The cloned request to forward</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Combined response from both systems</returns>
    Task<CompatibilityProxyResponse> ForwardRequestAsync(
        ClonedRequest clonedRequest,
        CancellationToken cancellationToken = default
    );
}

/// <summary>
/// Implementation of request forwarding service with Phase 2 enhancements
/// </summary>
public class RequestForwardingService : IRequestForwardingService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly IOptions<CompatibilityProxyConfiguration> _configuration;
    private readonly ILogger<RequestForwardingService> _logger;
    private readonly ICorrelationService _correlationService;
    private readonly IResponseComparisonService _responseComparisonService;
    private readonly IResponseCacheService _responseCacheService;
    private readonly IDiscrepancyPersistenceService _discrepancyPersistenceService;

    /// <summary>
    /// Initializes a new instance of the RequestForwardingService class
    /// </summary>
    /// <param name="httpClientFactory">Factory for creating HTTP clients</param>
    /// <param name="configuration">compatibilityProxy configuration settings</param>
    /// <param name="logger">Logger instance for this service</param>
    /// <param name="correlationService">Service for managing correlation IDs</param>
    /// <param name="responseComparisonService">Service for comparing responses</param>
    /// <param name="responseCacheService">Service for caching responses</param>
    /// <param name="discrepancyPersistenceService">Service for persisting discrepancy analysis</param>
    public RequestForwardingService(
        IHttpClientFactory httpClientFactory,
        IOptions<CompatibilityProxyConfiguration> configuration,
        ILogger<RequestForwardingService> logger,
        ICorrelationService correlationService,
        IResponseComparisonService responseComparisonService,
        IResponseCacheService responseCacheService,
        IDiscrepancyPersistenceService discrepancyPersistenceService
    )
    {
        _httpClientFactory = httpClientFactory;
        _configuration = configuration;
        _logger = logger;
        _correlationService = correlationService;
        _responseComparisonService = responseComparisonService;
        _responseCacheService = responseCacheService;
        _discrepancyPersistenceService = discrepancyPersistenceService;
    }

    /// <inheritdoc />
    public async Task<CompatibilityProxyResponse> ForwardRequestAsync(
        ClonedRequest clonedRequest,
        CancellationToken cancellationToken = default
    )
    {
        var totalStopwatch = Stopwatch.StartNew();

        // Generate correlation ID for tracking
        var correlationId = _correlationService.GenerateCorrelationId();
        _correlationService.SetCorrelationId(correlationId);

        _logger.LogInformation(
            "Forwarding request: {Method} {Path} [CorrelationId: {CorrelationId}]",
            clonedRequest.Method,
            clonedRequest.Path,
            correlationId
        );

        // Check cache first if enabled
        var cacheKey = _responseCacheService.GenerateCacheKey(clonedRequest);
        if (_responseCacheService.ShouldCacheRequest(clonedRequest))
        {
            var cachedResponse = await _responseCacheService.GetCachedResponseAsync(cacheKey);
            if (cachedResponse != null)
            {
                _logger.LogInformation(
                    "Returning cached response for {Method} {Path} [CorrelationId: {CorrelationId}]",
                    clonedRequest.Method,
                    clonedRequest.Path,
                    correlationId
                );

                cachedResponse.CorrelationId = correlationId;
                return cachedResponse;
            }
        }

        // Forward to both systems concurrently
        var nightscoutTask = ForwardToTargetAsync(
            clonedRequest,
            "Nightscout",
            _configuration.Value.NightscoutUrl,
            cancellationToken
        );
        var nocturneTask = ForwardToTargetAsync(
            clonedRequest,
            "Nocturne",
            _configuration.Value.NocturneUrl,
            cancellationToken
        );

        await Task.WhenAll(nightscoutTask, nocturneTask);

        var response = new CompatibilityProxyResponse
        {
            NightscoutResponse = await nightscoutTask,
            NocturneResponse = await nocturneTask,
            TotalProcessingTimeMs = totalStopwatch.ElapsedMilliseconds,
            CorrelationId = correlationId,
        };

        // Compare responses if both are available
        if (response.NightscoutResponse != null && response.NocturneResponse != null)
        {
            try
            {
                response.ComparisonResult = await _responseComparisonService.CompareResponsesAsync(
                    response.NightscoutResponse,
                    response.NocturneResponse,
                    correlationId,
                    clonedRequest.Path
                );

                _logger.LogDebug(
                    "Response comparison completed for {CorrelationId}: {OverallMatch}",
                    correlationId,
                    response.ComparisonResult.OverallMatch
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error comparing responses for {CorrelationId}",
                    correlationId
                );
            }
        }

        // Select response based on strategy
        response.SelectedResponse = SelectResponse(response);

        // Store discrepancy analysis for Phase 3 dashboard
        if (response.ComparisonResult != null)
        {
            try
            {
                await _discrepancyPersistenceService.StoreAnalysisAsync(
                    response.ComparisonResult,
                    response,
                    clonedRequest.Method,
                    clonedRequest.Path,
                    cancellationToken
                );
            }
            catch (Exception ex)
            {
                _logger.LogWarning(
                    ex,
                    "Error storing discrepancy analysis for {CorrelationId}",
                    correlationId
                );
                // Don't fail the request if persistence fails
            }
        }

        // Cache successful responses if enabled
        if (
            _responseCacheService.ShouldCacheRequest(clonedRequest)
            && response.SelectedResponse?.IsSuccess == true
        )
        {
            await _responseCacheService.SetCachedResponseAsync(cacheKey, response);
        }

        _logger.LogInformation(
            "Request forwarded. Selected: {Target}, Total time: {TotalTime}ms [CorrelationId: {CorrelationId}]",
            response.SelectedResponse?.Target,
            response.TotalProcessingTimeMs,
            correlationId
        );

        return response;
    }

    private async Task<TargetResponse> ForwardToTargetAsync(
        ClonedRequest clonedRequest,
        string targetName,
        string targetUrl,
        CancellationToken cancellationToken
    )
    {
        var stopwatch = Stopwatch.StartNew();
        var response = new TargetResponse { Target = targetName };
        var correlationId = _correlationService.GetCurrentCorrelationId();

        try
        {
            if (string.IsNullOrEmpty(targetUrl))
            {
                _logger.LogWarning(
                    "Target URL not configured for {Target} [CorrelationId: {CorrelationId}]",
                    targetName,
                    correlationId
                );
                response.ErrorMessage = "Target URL not configured";
                return response;
            }

            using var httpClient = _httpClientFactory.CreateClient($"{targetName}Client");

            // Get timeout for this specific endpoint or use default
            var timeout = GetTimeoutForEndpoint(clonedRequest.Path);
            httpClient.Timeout = TimeSpan.FromSeconds(timeout);

            var requestUri = new Uri(new Uri(targetUrl), clonedRequest.Path);
            using var httpRequest = new HttpRequestMessage(
                new HttpMethod(clonedRequest.Method),
                requestUri
            );

            // Add correlation ID header for tracking
            if (!string.IsNullOrEmpty(correlationId))
            {
                httpRequest.Headers.Add("X-Correlation-ID", correlationId);
            }

            // Add headers (excluding sensitive ones from logging)
            foreach (var header in clonedRequest.Headers)
            {
                if (header.Key.StartsWith("Content-", StringComparison.OrdinalIgnoreCase))
                {
                    continue; // Content headers are handled separately
                }
                httpRequest.Headers.TryAddWithoutValidation(header.Key, header.Value);
            }

            // Add content if present
            if (clonedRequest.Body?.Length > 0)
            {
                httpRequest.Content = new ByteArrayContent(clonedRequest.Body);
                if (!string.IsNullOrEmpty(clonedRequest.ContentType))
                {
                    httpRequest.Content.Headers.TryAddWithoutValidation(
                        "Content-Type",
                        clonedRequest.ContentType
                    );
                }
            }

            _logger.LogDebug(
                "Sending request to {Target}: {Uri} [CorrelationId: {CorrelationId}]",
                targetName,
                requestUri,
                correlationId
            );

            using var httpResponse = await httpClient.SendAsync(httpRequest, cancellationToken);

            response.StatusCode = (int)httpResponse.StatusCode;
            response.IsSuccess = httpResponse.IsSuccessStatusCode;
            response.ContentType = httpResponse.Content.Headers.ContentType?.ToString();
            response.Body = await httpResponse.Content.ReadAsByteArrayAsync(cancellationToken);

            // Copy response headers
            foreach (var header in httpResponse.Headers)
            {
                response.Headers[header.Key] = header.Value.ToArray();
            }
            foreach (var header in httpResponse.Content.Headers)
            {
                response.Headers[header.Key] = header.Value.ToArray();
            }

            _logger.LogDebug(
                "Received response from {Target}: {StatusCode} in {ResponseTime}ms [CorrelationId: {CorrelationId}]",
                targetName,
                response.StatusCode,
                stopwatch.ElapsedMilliseconds,
                correlationId
            );
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogWarning(
                "Request to {Target} timed out after {Timeout}s [CorrelationId: {CorrelationId}]",
                targetName,
                GetTimeoutForEndpoint(clonedRequest.Path),
                correlationId
            );
            response.ErrorMessage = "Request timed out";
            response.StatusCode = 408; // Request Timeout
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error forwarding request to {Target} [CorrelationId: {CorrelationId}]",
                targetName,
                correlationId
            );
            response.ErrorMessage = FilterSensitiveErrorMessage(ex.Message);
            response.StatusCode = 500; // Internal Server Error
        }
        finally
        {
            response.ResponseTimeMs = stopwatch.ElapsedMilliseconds;
        }

        return response;
    }

    private TargetResponse SelectResponse(CompatibilityProxyResponse compatibilityProxyResponse)
    {
        var strategy = _configuration.Value.DefaultStrategy;

        return strategy switch
        {
            ResponseSelectionStrategy.Nightscout => SelectNightscoutResponse(
                compatibilityProxyResponse
            ),
            ResponseSelectionStrategy.Nocturne => SelectNocturneResponse(
                compatibilityProxyResponse
            ),
            ResponseSelectionStrategy.Fastest => SelectFastestResponse(compatibilityProxyResponse),
            ResponseSelectionStrategy.Compare => SelectComparedResponse(compatibilityProxyResponse),
            ResponseSelectionStrategy.ABTest => SelectABTestResponse(compatibilityProxyResponse),
            _ => SelectNightscoutResponse(compatibilityProxyResponse),
        };
    }

    private TargetResponse SelectNightscoutResponse(
        CompatibilityProxyResponse compatibilityProxyResponse
    )
    {
        compatibilityProxyResponse.SelectionReason = "Default strategy: Nightscout";
        return compatibilityProxyResponse.NightscoutResponse
            ?? new TargetResponse
            {
                Target = "Nightscout",
                StatusCode = 503,
                ErrorMessage = "Nightscout not available",
            };
    }

    private TargetResponse SelectNocturneResponse(
        CompatibilityProxyResponse compatibilityProxyResponse
    )
    {
        compatibilityProxyResponse.SelectionReason = "Strategy: Nocturne";
        return compatibilityProxyResponse.NocturneResponse
            ?? new TargetResponse
            {
                Target = "Nocturne",
                StatusCode = 503,
                ErrorMessage = "Nocturne not available",
            };
    }

    private TargetResponse SelectFastestResponse(
        CompatibilityProxyResponse compatibilityProxyResponse
    )
    {
        var nightscout = compatibilityProxyResponse.NightscoutResponse;
        var nocturne = compatibilityProxyResponse.NocturneResponse;

        if (nightscout?.IsSuccess == true && nocturne?.IsSuccess == true)
        {
            if (nightscout.ResponseTimeMs <= nocturne.ResponseTimeMs)
            {
                compatibilityProxyResponse.SelectionReason =
                    $"Fastest: Nightscout ({nightscout.ResponseTimeMs}ms vs {nocturne.ResponseTimeMs}ms)";
                return nightscout;
            }
            else
            {
                compatibilityProxyResponse.SelectionReason =
                    $"Fastest: Nocturne ({nocturne.ResponseTimeMs}ms vs {nightscout.ResponseTimeMs}ms)";
                return nocturne;
            }
        }

        // Fallback to any successful response
        if (nightscout?.IsSuccess == true)
        {
            compatibilityProxyResponse.SelectionReason = "Nightscout only successful response";
            return nightscout;
        }
        if (nocturne?.IsSuccess == true)
        {
            compatibilityProxyResponse.SelectionReason = "Nocturne only successful response";
            return nocturne;
        }

        // Both failed, return Nightscout as default
        compatibilityProxyResponse.SelectionReason = "Both failed, returning Nightscout";
        return nightscout
            ?? new TargetResponse
            {
                Target = "Nightscout",
                StatusCode = 503,
                ErrorMessage = "Both systems unavailable",
            };
    }

    private TargetResponse SelectComparedResponse(
        CompatibilityProxyResponse compatibilityProxyResponse
    )
    {
        var comparison = compatibilityProxyResponse.ComparisonResult;

        // If we have a comparison result, use it to make an informed decision
        if (comparison != null)
        {
            switch (comparison.OverallMatch)
            {
                case Nocturne.Core.Models.ResponseMatchType.Perfect:
                case Nocturne.Core.Models.ResponseMatchType.MinorDifferences:
                    // Responses are similar, choose fastest
                    return SelectFastestResponse(compatibilityProxyResponse);

                case Nocturne.Core.Models.ResponseMatchType.MajorDifferences:
                case Nocturne.Core.Models.ResponseMatchType.CriticalDifferences:
                    // Significant differences, default to Nightscout but log warning
                    _logger.LogWarning(
                        "Significant response differences detected for {CorrelationId}: {Summary}",
                        comparison.CorrelationId,
                        comparison.Summary
                    );
                    compatibilityProxyResponse.SelectionReason =
                        $"Critical differences detected, defaulting to Nightscout: {comparison.Summary}";
                    return compatibilityProxyResponse.NightscoutResponse
                        ?? new TargetResponse
                        {
                            Target = "Nightscout",
                            StatusCode = 503,
                            ErrorMessage = "Nightscout not available",
                        };

                default:
                    break;
            }
        }

        // Fallback to fastest strategy if comparison fails
        return SelectFastestResponse(compatibilityProxyResponse);
    }

    private TargetResponse SelectABTestResponse(
        CompatibilityProxyResponse compatibilityProxyResponse
    )
    {
        var percentage = _configuration.Value.ABTestingPercentage;

        // If A/B testing is disabled, fallback to Nightscout
        if (percentage <= 0)
        {
            return SelectNightscoutResponse(compatibilityProxyResponse);
        }

        // If A/B testing is at 100%, use Nocturne
        if (percentage >= 100)
        {
            return SelectNocturneResponse(compatibilityProxyResponse);
        }

        // Use correlation ID or request path to determine which response to use
        // This ensures consistency for the same request
        var correlationId = compatibilityProxyResponse.CorrelationId;
        var hash = correlationId.GetHashCode();
        var bucketValue = Math.Abs(hash % 100);

        if (bucketValue < percentage)
        {
            compatibilityProxyResponse.SelectionReason =
                $"A/B Test: Nocturne (bucket {bucketValue} < {percentage}%)";
            return SelectNocturneResponse(compatibilityProxyResponse);
        }
        else
        {
            compatibilityProxyResponse.SelectionReason =
                $"A/B Test: Nightscout (bucket {bucketValue} >= {percentage}%)";
            return SelectNightscoutResponse(compatibilityProxyResponse);
        }
    }

    private int GetTimeoutForEndpoint(string path)
    {
        var endpointTimeouts = _configuration.Value.EndpointTimeouts;

        // Check for specific endpoint timeouts
        foreach (var kvp in endpointTimeouts)
        {
            if (path.Contains(kvp.Key, StringComparison.OrdinalIgnoreCase))
            {
                return kvp.Value;
            }
        }

        // Return default timeout
        return _configuration.Value.TimeoutSeconds;
    }

    private string FilterSensitiveErrorMessage(string errorMessage)
    {
        var sensitiveFields = _configuration.Value.SensitiveFields;
        var filteredMessage = errorMessage;

        foreach (var sensitiveField in sensitiveFields)
        {
            // Simple replacement to avoid exposing sensitive data in error messages
            filteredMessage = filteredMessage.Replace(
                sensitiveField,
                "[REDACTED]",
                StringComparison.OrdinalIgnoreCase
            );
        }

        return filteredMessage;
    }
}
