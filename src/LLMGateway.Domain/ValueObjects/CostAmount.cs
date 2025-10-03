namespace LLMGateway.Domain.ValueObjects;

public sealed class CostAmount : ValueObject
{
    public decimal ValueUsd { get; }

    private CostAmount(decimal valueUsd)
    {
        if (valueUsd < 0)
            throw new ArgumentException(
                "Cost cannot be negative",
                nameof(valueUsd));

        // Round to 6 decimal places for micro-cost precision
        ValueUsd = Math.Round(valueUsd, 6);
    }

    public static CostAmount FromUsd(decimal value) => new(value);

    public static CostAmount Zero => new(0);

    public CostAmount Add(CostAmount other)
        => new(ValueUsd + other.ValueUsd);

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return ValueUsd;
    }

    public override string ToString() => $"${ValueUsd:F6}";
}