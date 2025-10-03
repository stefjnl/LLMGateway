using LLMGateway.Domain.Entities;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using static Microsoft.EntityFrameworkCore.EF;

namespace LLMGateway.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for ModelPricing entity.
/// Maps value objects using OwnsOne with snake_case column names.
/// </summary>
public class ModelPricingConfiguration : IEntityTypeConfiguration<ModelPricing>
{
    public void Configure(EntityTypeBuilder<ModelPricing> builder)
    {
        builder.ToTable("model_pricing");

        builder.HasKey(mp => mp.Id);
        builder.Property(mp => mp.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(mp => mp.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(mp => mp.InputCostPer1MTokens)
            .HasColumnName("input_cost_per_1m_tokens")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(mp => mp.OutputCostPer1MTokens)
            .HasColumnName("output_cost_per_1m_tokens")
            .HasColumnType("decimal(18,6)")
            .IsRequired();

        builder.Property(mp => mp.MaxContextTokens)
            .HasColumnName("max_context_tokens")
            .IsRequired();

        builder.Property(mp => mp.UpdatedAt)
            .HasColumnName("updated_at")
            .IsRequired();

        // Map Model value object
        builder.OwnsOne(mp => mp.Model, mb =>
        {
            mb.Property(m => m.Value)
                .HasColumnName("model_name")
                .HasMaxLength(200)
                .IsRequired();

            mb.Property(m => m.Provider)
                .HasColumnName("model_provider")
                .HasMaxLength(100)
                .IsRequired();
        });

        // Note: Unique constraint on model_name could be added later if needed
        // For MVP, we'll rely on application logic to prevent duplicates
    }
}