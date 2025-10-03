using LLMGateway.Domain.Interfaces;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace LLMGateway.Api.HealthChecks;

public class OpenRouterHealthCheck : IHealthCheck
{
    private readonly IProviderHealthChecker _healthChecker;

    public OpenRouterHealthCheck(IProviderHealthChecker healthChecker)
    {
        _healthChecker = healthChecker ?? throw new ArgumentNullException(nameof(healthChecker));
    }

    public async Task<HealthCheckResult> CheckHealthAsync(
        HealthCheckContext context,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var isHealthy = await _healthChecker.IsHealthyAsync("OpenRouter", cancellationToken);

            return isHealthy
                ? HealthCheckResult.Healthy("OpenRouter service is healthy")
                : HealthCheckResult.Unhealthy("OpenRouter service is unhealthy");
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy(
                "OpenRouter health check failed",
                ex);
        }
    }
}