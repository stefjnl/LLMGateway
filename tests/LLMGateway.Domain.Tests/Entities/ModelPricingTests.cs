using FluentAssertions;
using LLMGateway.Domain.Entities;
using LLMGateway.Domain.ValueObjects;

namespace LLMGateway.Domain.Tests.Entities;

public class ModelPricingTests
{
    [Fact]
    public void Create_ValidParameters_CreatesModelPricing()
    {
        // Arrange
        var model = ModelName.From("z-ai/glm-4.6");

        // Act
        var pricing = ModelPricing.Create(
            model,
            "OpenRouter",
            inputCost: 0.0001m,
            outputCost: 0.0002m,
            maxTokens: 128_000);

        // Assert
        pricing.Id.Should().NotBeEmpty();
        pricing.Model.Should().Be(model);
        pricing.ProviderName.Should().Be("OpenRouter");
        pricing.InputCostPer1MTokens.Should().Be(0.0001m);
        pricing.OutputCostPer1MTokens.Should().Be(0.0002m);
        pricing.MaxContextTokens.Should().Be(128_000);
        pricing.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void CalculateCost_ValidTokens_ReturnsCorrectCost()
    {
        // Arrange
        var pricing = ModelPricing.Create(
            ModelName.From("z-ai/glm-4.6"),
            "OpenRouter",
            inputCost: 0.0001m,   // $0.0001 per 1M tokens
            outputCost: 0.0002m,  // $0.0002 per 1M tokens
            maxTokens: 128_000);

        var inputTokens = TokenCount.From(1_000_000);  // 1M tokens
        var outputTokens = TokenCount.From(1_000_000); // 1M tokens

        // Act
        var cost = pricing.CalculateCost(inputTokens, outputTokens);

        // Assert
        // Input: (1,000,000 / 1,000,000) * 0.0001 = $0.0001
        // Output: (1,000,000 / 1,000,000) * 0.0002 = $0.0002
        // Total: $0.0003
        cost.ValueUsd.Should().Be(0.0003m);
    }

    [Fact]
    public void CalculateCost_SmallTokenCount_CalculatesMicroCost()
    {
        // Arrange
        var pricing = ModelPricing.Create(
            ModelName.From("z-ai/glm-4.6"),
            "OpenRouter",
            inputCost: 0.0001m,
            outputCost: 0.0002m,
            maxTokens: 128_000);

        var inputTokens = TokenCount.From(5000);    // Small but realistic request
        var outputTokens = TokenCount.From(10000);

        // Act
        var cost = pricing.CalculateCost(inputTokens, outputTokens);

        // Assert
        // Input: (5000 / 1,000,000) * 0.0001 = $0.0000005
        // Output: (10000 / 1,000,000) * 0.0002 = $0.000002
        // Total: $0.0000025 → rounds to $0.000003 (6 decimals)
        cost.ValueUsd.Should().BeGreaterThan(0);
        cost.ValueUsd.Should().BeLessThan(0.00001m); // Micro cost
    }
}