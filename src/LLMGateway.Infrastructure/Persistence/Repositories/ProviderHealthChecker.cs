using LLMGateway.Domain.Interfaces;
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

    public ProviderHealthChecker(
        HttpClient httpClient,
        ILogger<ProviderHealthChecker> logger)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
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

            // Call OpenRouter /models endpoint with 5s timeout
            using var cts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
            cts.CancelAfter(TimeSpan.FromSeconds(5));

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
}