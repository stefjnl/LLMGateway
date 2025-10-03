namespace LLMGateway.Domain.Constants;

public static class ModelDefaults
{
    // Model identifiers (OpenRouter format)
    public const string DefaultModel = "z-ai/glm-4.6";
    public const string LargeContextModel = "moonshotai/Kimi-K2-Instruct-0905";
    public const string BalancedModel = "deepseek-ai/DeepSeek-V3.1-Terminus";

    // Context thresholds for routing
    public const int StandardContextLimit = 10_000;
    public const int LargeContextLimit = 200_000;

    // Fallback chain (circular with cycle detection)
    public static readonly string[] FallbackChain = new[]
    {
        DefaultModel,
        BalancedModel,
        LargeContextModel
    };
}