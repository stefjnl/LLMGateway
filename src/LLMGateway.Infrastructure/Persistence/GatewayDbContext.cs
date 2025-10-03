using LLMGateway.Domain.Entities;
using LLMGateway.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LLMGateway.Infrastructure.Persistence;

/// <summary>
/// EF Core DbContext for the LLM Gateway database.
/// Manages RequestLog and ModelPricing entities with PostgreSQL.
/// </summary>
public class GatewayDbContext : DbContext
{
    public DbSet<RequestLog> RequestLogs { get; set; }
    public DbSet<ModelPricing> ModelPricings { get; set; }

    public GatewayDbContext(DbContextOptions<GatewayDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Apply all entity configurations from this assembly
        modelBuilder.ApplyConfigurationsFromAssembly(typeof(GatewayDbContext).Assembly);
    }
}