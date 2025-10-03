namespace LLMGateway.Domain.ValueObjects;

public sealed class TokenCount : ValueObject
{
    public int Value { get; }

    private TokenCount(int value)
    {
        if (value < 0)
            throw new ArgumentException(
                "Token count cannot be negative",
                nameof(value));

        Value = value;
    }

    public static TokenCount From(int value) => new(value);

    public static TokenCount EstimateFromText(string text)
    {
        if (string.IsNullOrEmpty(text))
            return new TokenCount(0);

        // Simple estimation: ~4 characters per token
        // This is approximate - real tokenizers vary by model
        var estimatedTokens = text.Length / 4;
        return new TokenCount(estimatedTokens);
    }

    public bool ExceedsLimit(int maxTokens) => Value > maxTokens;

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value.ToString();
}