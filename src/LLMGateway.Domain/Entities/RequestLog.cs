using LLMGateway.Domain.ValueObjects;

namespace LLMGateway.Domain.Entities;

public class RequestLog
{
    public Guid Id { get; private set; }
    public DateTime Timestamp { get; private set; }
    public ModelName ModelUsed { get; private set; }
    public TokenCount InputTokens { get; private set; }
    public TokenCount OutputTokens { get; private set; }
    public CostAmount EstimatedCost { get; private set; }
    public string ProviderName { get; private set; }
    public TimeSpan ResponseTime { get; private set; }
    public bool WasFallback { get; private set; }

    // Private constructor for EF Core
    private RequestLog()
    {
        ModelUsed = null!;
        ProviderName = null!;
    }

    // Factory method enforces invariants
    public static RequestLog Create(
        ModelName model,
        TokenCount inputTokens,
        TokenCount outputTokens,
        CostAmount cost,
        string provider,
        TimeSpan responseTime,
        bool wasFallback = false)
    {
        if (model == null)
            throw new ArgumentNullException(nameof(model));

        if (inputTokens == null)
            throw new ArgumentNullException(nameof(inputTokens));

        if (outputTokens == null)
            throw new ArgumentNullException(nameof(outputTokens));

        if (cost == null)
            throw new ArgumentNullException(nameof(cost));

        if (string.IsNullOrWhiteSpace(provider))
            throw new ArgumentException(
                "Provider name cannot be empty",
                nameof(provider));

        if (responseTime < TimeSpan.Zero)
            throw new ArgumentException(
                "Response time cannot be negative",
                nameof(responseTime));

        return new RequestLog
        {
            Id = Guid.NewGuid(),
            Timestamp = DateTime.UtcNow,
            ModelUsed = model,
            InputTokens = inputTokens,
            OutputTokens = outputTokens,
            EstimatedCost = cost,
            ProviderName = provider,
            ResponseTime = responseTime,
            WasFallback = wasFallback
        };
    }

    public TokenCount TotalTokens()
        => TokenCount.From(InputTokens.Value + OutputTokens.Value);
}