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
    public Task<string> GetFallbackModelAsync(
        string failedModel,
        IReadOnlyList<string> attemptedModels)
    {
        var fallbackChain = ModelDefaults.FallbackChain;

        // Check if we've exhausted all options
        if (attemptedModels.Count >= fallbackChain.Length)
        {
            _logger.LogError(
                "All providers failed. Attempted models: {Models}",
                string.Join(", ", attemptedModels));

            throw new AllProvidersFailedException(attemptedModels.ToArray());
        }

        // Find next model that hasn't been attempted
        string? nextModel = null;
        var currentIndex = Array.IndexOf(fallbackChain, failedModel);

        if (currentIndex == -1)
        {
            // Model not in chain - throw immediately
            _logger.LogError(
                "Unknown model {Model} not in fallback chain",
                failedModel);

            throw new AllProvidersFailedException(attemptedModels.ToArray());
        }
        else
        {
            // Try next models in chain (circular)
            for (int i = 1; i <= fallbackChain.Length; i++)
            {
                var checkIndex = (currentIndex + i) % fallbackChain.Length;
                var candidateModel = fallbackChain[checkIndex];

                if (!attemptedModels.Contains(candidateModel))
                {
                    nextModel = candidateModel;
                    break;
                }
            }
        }

        if (nextModel == null)
        {
            _logger.LogError(
                "No untried models remaining. Attempted: {Models}",
                string.Join(", ", attemptedModels));

            throw new AllProvidersFailedException(attemptedModels.ToArray());
        }

        _logger.LogWarning(
            "Falling back from {FailedModel} to {NextModel}",
            failedModel,
            nextModel);

        return Task.FromResult(nextModel);
    }
}