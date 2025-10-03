using LLMGateway.Domain.Entities;
using LLMGateway.Domain.Interfaces;
using LLMGateway.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace LLMGateway.Application.Plugins;

public class CostTrackingPlugin
{
    private readonly IRequestLogRepository _logRepository;
    private readonly IModelPricingRepository _pricingRepository;
    private readonly ILogger<CostTrackingPlugin> _logger;

    public CostTrackingPlugin(
        IRequestLogRepository logRepository,
        IModelPricingRepository pricingRepository,
        ILogger<CostTrackingPlugin> logger)
    {
        _logRepository = logRepository;
        _pricingRepository = pricingRepository;
        _logger = logger;
    }

    [KernelFunction("track_cost")]
    public async Task TrackCostAsync(
        string modelName,
        int inputTokens,
        int outputTokens,
        string providerName,
        long responseTimeMs,
        bool wasFallback,
        CancellationToken cancellationToken = default)
    {
        try
        {
            var model = ModelName.From(modelName);
            var inputTokenCount = TokenCount.From(inputTokens);
            var outputTokenCount = TokenCount.From(outputTokens);

            // Fetch pricing information
            var pricing = await _pricingRepository.GetByModelAsync(model, cancellationToken);

            CostAmount cost;
            if (pricing == null)
            {
                _logger.LogWarning(
                    "No pricing information found for model {Model}. Using zero cost.",
                    modelName);
                cost = CostAmount.Zero;
            }
            else
            {
                cost = pricing.CalculateCost(inputTokenCount, outputTokenCount);
            }

            // Create and persist request log
            var log = RequestLog.Create(
                model,
                inputTokenCount,
                outputTokenCount,
                cost,
                providerName,
                TimeSpan.FromMilliseconds(responseTimeMs),
                wasFallback);

            await _logRepository.SaveAsync(log, cancellationToken);

            _logger.LogInformation(
                "Tracked request: Model={Model}, Tokens={Tokens}, Cost={Cost:F6}, Fallback={Fallback}",
                modelName,
                inputTokens + outputTokens,
                cost.ValueUsd,
                wasFallback);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Failed to track cost for model {Model}",
                modelName);
            // Don't throw - cost tracking failure shouldn't break the response
        }
    }
}