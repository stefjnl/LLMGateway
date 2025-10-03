using LLMGateway.Domain.ValueObjects;

namespace LLMGateway.Domain.Entities;

public class ModelPricing
{
    public Guid Id { get; private set; }
    public ModelName Model { get; private set; }
    public string ProviderName { get; private set; }
    public decimal InputCostPer1MTokens { get; private set; }
    public decimal OutputCostPer1MTokens { get; private set; }
    public int MaxContextTokens { get; private set; }
    public DateTime UpdatedAt { get; private set; }

    // Private constructor for EF Core
    private ModelPricing()
    {
        Model = null!;
        ProviderName = null!;
    }

    public static ModelPricing Create(
        ModelName model,
        string provider,
        decimal inputCost,
        decimal outputCost,
        int maxTokens)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException(
                "Provider name cannot be empty",
                nameof(provider));

        if (inputCost < 0)
            throw new ArgumentException(
                "Input cost cannot be negative",
                nameof(inputCost));

        if (outputCost < 0)
            throw new ArgumentException(
                "Output cost cannot be negative",
                nameof(outputCost));

        if (maxTokens <= 0)
            throw new ArgumentException(
                "Max tokens must be positive",
                nameof(maxTokens));

        return new ModelPricing
        {
            Id = Guid.NewGuid(),
            Model = model,
            ProviderName = provider,
            InputCostPer1MTokens = inputCost,
            OutputCostPer1MTokens = outputCost,
            MaxContextTokens = maxTokens,
            UpdatedAt = DateTime.UtcNow
        };
    }

    public CostAmount CalculateCost(TokenCount input, TokenCount output)
    {
        if (input == null)
            throw new ArgumentNullException(nameof(input));

        if (output == null)
            throw new ArgumentNullException(nameof(output));

        var inputCost = (input.Value / 1_000_000m) * InputCostPer1MTokens;
        var outputCost = (output.Value / 1_000_000m) * OutputCostPer1MTokens;

        return CostAmount.FromUsd(inputCost + outputCost);
    }
}