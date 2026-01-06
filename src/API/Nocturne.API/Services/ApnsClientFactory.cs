using dotAPNS;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Nocturne.Core.Models;

namespace Nocturne.API.Services;

/// <summary>
/// Default implementation of IApnsClientFactory
/// Creates real APNS clients using dotAPNS library
/// </summary>
public class ApnsClientFactory : IApnsClientFactory
{
    private readonly LoopConfiguration _configuration;
    private readonly HttpClient _httpClient;
    private readonly ILogger<ApnsClientFactory> _logger;

    public ApnsClientFactory(
        IOptions<LoopConfiguration> configuration,
        IHttpClientFactory httpClientFactory,
        ILogger<ApnsClientFactory> logger
    )
    {
        _configuration = configuration.Value;
        _httpClient = httpClientFactory.CreateClient("dotAPNS");
        _logger = logger;

        // Configure custom APNS server URL if provided (for testing)
        if (!string.IsNullOrEmpty(_configuration.ApnsServerOverrideUrl))
        {
            _httpClient.BaseAddress = new Uri(_configuration.ApnsServerOverrideUrl);
            _logger.LogInformation(
                "APNS client factory configured to use custom server: {OverrideUrl}",
                _configuration.ApnsServerOverrideUrl
            );
        }
    }

    /// <summary>
    /// Checks if the factory is properly configured with all required settings
    /// </summary>
    public bool IsConfigured =>
        !string.IsNullOrEmpty(_configuration.ApnsKey)
        && !string.IsNullOrEmpty(_configuration.ApnsKeyId)
        && !string.IsNullOrEmpty(_configuration.DeveloperTeamId)
        && _configuration.DeveloperTeamId.Length == 10;

    /// <summary>
    /// Creates an APNS client configured for the specified bundle ID
    /// </summary>
    public IApnsClient? CreateClient(string bundleId)
    {
        if (!IsConfigured)
        {
            _logger.LogWarning("Cannot create APNS client: configuration is invalid");
            return null;
        }

        try
        {
            var options = new ApnsJwtOptions
            {
                KeyId = _configuration.ApnsKeyId!,
                TeamId = _configuration.DeveloperTeamId!,
                CertContent = _configuration.ApnsKey!,
                BundleId = bundleId,
            };

            return ApnsClient.CreateUsingJwt(_httpClient, options);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to create APNS client for bundle: {BundleId}", bundleId);
            return null;
        }
    }
}
