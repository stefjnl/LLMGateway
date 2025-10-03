namespace LLMGateway.Domain.ValueObjects;

public sealed class ModelName : ValueObject
{
    public string Value { get; }
    public string Provider { get; }

    private ModelName(string value)
    {
        Value = value;
        Provider = ExtractProvider(value);
    }

    public static ModelName From(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException(
                "Model name cannot be null or empty",
                nameof(value));

        return new ModelName(value);
    }

    private static string ExtractProvider(string modelName)
    {
        // Format: "provider/model-name" (e.g., "z-ai/glm-4.6")
        var parts = modelName.Split('/', 2);
        return parts.Length > 1 ? parts[0] : "unknown";
    }

    protected override IEnumerable<object> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}