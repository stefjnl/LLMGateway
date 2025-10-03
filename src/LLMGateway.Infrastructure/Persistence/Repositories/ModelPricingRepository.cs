using LLMGateway.Domain.Entities;
using LLMGateway.Domain.Interfaces;
using LLMGateway.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LLMGateway.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IModelPricingRepository.
/// Queries model pricing data from PostgreSQL database.
/// </summary>
public class ModelPricingRepository : IModelPricingRepository
{
    private readonly GatewayDbContext _context;

    public ModelPricingRepository(GatewayDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<ModelPricing?> GetByModelAsync(
        ModelName model,
        CancellationToken cancellationToken = default)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        return await _context.ModelPricings
            .FirstOrDefaultAsync(mp => mp.Model.Value == model.Value, cancellationToken);
    }

    public async Task<IEnumerable<ModelPricing>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.ModelPricings
            .OrderBy(mp => mp.Model.Value)
            .ToListAsync(cancellationToken);
    }
}