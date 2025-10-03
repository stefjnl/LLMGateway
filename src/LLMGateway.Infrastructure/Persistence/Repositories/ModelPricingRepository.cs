using LLMGateway.Domain.Entities;
using LLMGateway.Domain.Interfaces;
using LLMGateway.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using System.Linq;

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

        // Use the actual property access - EF Core should handle the conversion via the configured value converter
        // For InMemory provider, we need to use a different approach
        // Load all records and filter in memory to avoid query translation issues
        var allPricings = await _context.ModelPricings.ToListAsync(cancellationToken);
        return allPricings.FirstOrDefault(mp => mp.Model.Value == model.Value);
    }

    public async Task<IEnumerable<ModelPricing>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        return await _context.ModelPricings
            .OrderBy(mp => mp.Model.Value)
            .ToListAsync(cancellationToken);
    }
}