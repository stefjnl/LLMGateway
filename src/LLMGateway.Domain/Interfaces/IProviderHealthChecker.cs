namespace LLMGateway.Domain.Interfaces;

public interface IProviderHealthChecker
{
    Task<bool> IsHealthyAsync(
        string providerName,
        CancellationToken cancellationToken = default);

    Task<Dictionary<string, bool>> CheckAllProvidersAsync(
        CancellationToken cancellationToken = default);
}