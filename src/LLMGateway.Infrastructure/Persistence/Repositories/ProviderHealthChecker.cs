using LLMGateway.Domain.Interfaces;
using LLMGateway.Infrastructure.ChatCompletion;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace LLMGateway.Infrastructure.Persistence.Repositories;

/// <summary>
/// Health checker for LLM providers.
/// Calls provider health endpoints to determine availability.
/// </summary>
public class ProviderHealthChecker : IProviderHealthChecker
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<ProviderHealthChecker> _logger;
    private readonly IConfiguration _configuration;

    public ProviderHealthChecker(
        HttpClient httpClient,
        ILogger<ProviderHealthChecker> logger,
        IConfiguration configuration)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
    }

    public async Task<bool> IsHealthyAsync(
        string providerName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(providerName))
            throw new ArgumentException("Provider name cannot be empty", nameof(providerName));

        try
        {
            // For MVP, only OpenRouter is supported
            if (!string.Equals(providerName, "OpenRouter", StringComparison.OrdinalIgnoreCase))
            {
                _logger.LogWarning("Unknown provider: {ProviderName}", providerName);
                return false;
            }

            // Call OpenRouter /models endpoint with configurable timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            
            // Get configuration for health check timeout
            var config = GetOpenRouterConfig();
            cts.CancelAfter(TimeSpan.FromSeconds(config?.HealthCheckTimeoutSeconds ?? 5));

            var response = await _httpClient.GetAsync("models", cts.Token);

            if (response.IsSuccessStatusCode)
            {
                _logger.LogInformation("Provider {ProviderName} is healthy", providerName);
                return true;
            }
            else
            {
                _logger.LogWarning("Provider {ProviderName} returned status {StatusCode}",
                    providerName, response.StatusCode);
                return false;
            }
        }
        catch (TaskCanceledException)
        {
            _logger.LogWarning("Provider {ProviderName} health check timed out", providerName);
            return false;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Provider {ProviderName} health check failed", providerName);
            return false;
        }
    }

    public async Task<Dictionary<string, bool>> CheckAllProvidersAsync(
        CancellationToken cancellationToken = default)
    {
        var results = new Dictionary<string, bool>();

        // For MVP, only check OpenRouter
        var isHealthy = await IsHealthyAsync("OpenRouter", cancellationToken);
        results["OpenRouter"] = isHealthy;

        return results;
    }

    /// <summary>
    /// Gets the OpenRouter configuration from the application settings
    /// </summary>
    private OpenRouterConfig? GetOpenRouterConfig()
    {
        return _configuration.GetSection("OpenRouter").Get<OpenRouterConfig>();
    }
}