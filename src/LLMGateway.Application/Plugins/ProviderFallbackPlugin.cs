using LLMGateway.Domain.Constants;
using LLMGateway.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace LLMGateway.Application.Plugins;

public class ProviderFallbackPlugin
{
    private readonly ILogger<ProviderFallbackPlugin> _logger;

    public ProviderFallbackPlugin(ILogger<ProviderFallbackPlugin> logger)
    {
        _logger = logger;
    }

    [KernelFunction("get_fallback_model")]
    public Task<string> GetFallbackModelAsync(string failedModel)
    {
        var fallbackChain = ModelDefaults.FallbackChain;
        var currentIndex = Array.IndexOf(fallbackChain, failedModel);

        // Model not in chain or reached end of chain
        if (currentIndex == -1 || currentIndex >= fallbackChain.Length - 1)
        {
            _logger.LogError(
                "All providers failed. Attempted models: {Models}",
                string.Join(", ", fallbackChain));

            throw new AllProvidersFailedException(fallbackChain);
        }

        var nextModel = fallbackChain[currentIndex + 1];

        _logger.LogWarning(
            "Falling back from {FailedModel} to {NextModel}",
            failedModel,
            nextModel);

        return Task.FromResult(nextModel);
    }
}