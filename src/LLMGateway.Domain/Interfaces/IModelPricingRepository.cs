using LLMGateway.Domain.Entities;
using LLMGateway.Domain.ValueObjects;

namespace LLMGateway.Domain.Interfaces;

public interface IModelPricingRepository
{
    Task<ModelPricing?> GetByModelAsync(
        ModelName model,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<ModelPricing>> GetAllAsync(
        CancellationToken cancellationToken = default);
}