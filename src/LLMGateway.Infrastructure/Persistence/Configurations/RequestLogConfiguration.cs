using LLMGateway.Domain.Entities;
using LLMGateway.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace LLMGateway.Infrastructure.Persistence.Configurations;

/// <summary>
/// EF Core configuration for RequestLog entity.
/// Maps value objects using OwnsOne with snake_case column names.
/// </summary>
public class RequestLogConfiguration : IEntityTypeConfiguration<RequestLog>
{
    public void Configure(EntityTypeBuilder<RequestLog> builder)
    {
        builder.ToTable("request_logs");

        builder.HasKey(rl => rl.Id);
        builder.Property(rl => rl.Id)
            .HasColumnName("id")
            .ValueGeneratedNever();

        builder.Property(rl => rl.Timestamp)
            .HasColumnName("timestamp")
            .IsRequired();

        builder.Property(rl => rl.ProviderName)
            .HasColumnName("provider_name")
            .HasMaxLength(100)
            .IsRequired();

        builder.Property(rl => rl.ResponseTime)
            .HasColumnName("response_time_ms")
            .HasConversion(
                v => (long)v.TotalMilliseconds,
                v => new TimeSpan(0, 0, 0, 0, (int)v))
            .IsRequired();

        builder.Property(rl => rl.WasFallback)
            .HasColumnName("was_fallback")
            .IsRequired();

        // Map ModelUsed value object using conversion
        builder.Property(rl => rl.ModelUsed)
            .HasColumnName("model_used")
            .HasMaxLength(300)
            .HasConversion(
                v => v.Value, // Store the full model name (e.g., "z-ai/glm-4.6")
                v => ModelName.From(v)) // Convert back to ModelName
            .IsRequired();

        // Map value objects using conversions (simpler for owned types)
        builder.Property(rl => rl.InputTokens)
            .HasColumnName("input_tokens")
            .HasConversion(
                v => v.Value,
                v => TokenCount.From(v))
            .IsRequired();

        builder.Property(rl => rl.OutputTokens)
            .HasColumnName("output_tokens")
            .HasConversion(
                v => v.Value,
                v => TokenCount.From(v))
            .IsRequired();

        builder.Property(rl => rl.EstimatedCost)
            .HasColumnName("estimated_cost_usd")
            .HasColumnType("decimal(18,6)")
            .HasConversion(
                v => v.ValueUsd,
                v => CostAmount.FromUsd(v))
            .IsRequired();

        // Indexes for performance
        builder.HasIndex(rl => rl.Timestamp)
            .HasDatabaseName("ix_request_logs_timestamp");

        builder.HasIndex(rl => rl.ProviderName)
            .HasDatabaseName("ix_request_logs_provider_name");
    }
}