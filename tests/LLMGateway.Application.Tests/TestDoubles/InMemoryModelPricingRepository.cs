using LLMGateway.Domain.Constants;
using LLMGateway.Domain.Entities;
using LLMGateway.Domain.Interfaces;
using LLMGateway.Domain.ValueObjects;

namespace LLMGateway.Application.Tests.TestDoubles;

public class InMemoryModelPricingRepository : IModelPricingRepository
{
    private readonly List<ModelPricing> _pricing;

    public InMemoryModelPricingRepository()
    {
        // Seed with default pricing data
        _pricing = new List<ModelPricing>
        {
            ModelPricing.Create(
                ModelName.From(ModelDefaults.DefaultModel),
                "z-ai",
                inputCost: 0.0001m,
                outputCost: 0.0002m,
                maxTokens: 128_000),

            ModelPricing.Create(
                ModelName.From(ModelDefaults.BalancedModel),
                "deepseek-ai",
                inputCost: 0.0003m,
                outputCost: 0.0005m,
                maxTokens: 64_000),

            ModelPricing.Create(
                ModelName.From(ModelDefaults.LargeContextModel),
                "moonshotai",
                inputCost: 0.0005m,
                outputCost: 0.0010m,
                maxTokens: 200_000)
        };
    }

    public Task<ModelPricing?> GetByModelAsync(
        ModelName model,
        CancellationToken cancellationToken = default)
    {
        var pricing = _pricing.FirstOrDefault(p => p.Model == model);
        return Task.FromResult(pricing);
    }

    public Task<IEnumerable<ModelPricing>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(_pricing.AsEnumerable());
    }
}