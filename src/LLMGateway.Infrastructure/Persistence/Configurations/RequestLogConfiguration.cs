using LLMGateway.Domain.Entities;
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

        // Map ModelUsed value object
        builder.OwnsOne(rl => rl.ModelUsed, mb =>
        {
            mb.Property(m => m.Value)
                .HasColumnName("model_used")
                .HasMaxLength(200)
                .IsRequired();

            mb.Property(m => m.Provider)
                .HasColumnName("model_provider")
                .HasMaxLength(100)
                .IsRequired();
        });

        // Map InputTokens value object
        builder.OwnsOne(rl => rl.InputTokens, mb =>
        {
            mb.Property(t => t.Value)
                .HasColumnName("input_tokens")
                .IsRequired();
        });

        // Map OutputTokens value object
        builder.OwnsOne(rl => rl.OutputTokens, mb =>
        {
            mb.Property(t => t.Value)
                .HasColumnName("output_tokens")
                .IsRequired();
        });

        // Map EstimatedCost value object
        builder.OwnsOne(rl => rl.EstimatedCost, mb =>
        {
            mb.Property(c => c.ValueUsd)
                .HasColumnName("estimated_cost_usd")
                .HasColumnType("decimal(18,6)")
                .IsRequired();
        });

        // Indexes for performance
        builder.HasIndex(rl => rl.Timestamp)
            .HasDatabaseName("ix_request_logs_timestamp");

        builder.HasIndex(rl => rl.ProviderName)
            .HasDatabaseName("ix_request_logs_provider_name");
    }
}