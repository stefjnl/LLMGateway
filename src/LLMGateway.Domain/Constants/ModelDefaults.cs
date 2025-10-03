namespace LLMGateway.Domain.Constants;

public static class ModelDefaults
{
    // Model identifiers (OpenRouter format)
    public const string DefaultModel = "google/gemini-2.5-flash-lite-preview-09-2025";
    public const string LargeContextModel = "moonshotai/Kimi-K2-Instruct-0905";
    public const string BalancedModel = "deepseek-ai/DeepSeek-V3.1-Terminus";

    // Context thresholds for routing
    public const int StandardContextLimit = 10_000;
    public const int LargeContextLimit = 200_000;

    // Fallback chain (ordered from slow/large to fast/small for fallback priority)
    public static readonly string[] FallbackChain = new[]
    {
        LargeContextModel,
        BalancedModel,
        DefaultModel
    };

    // Default request parameters
    public const decimal DefaultTemperature = 0.7m;
    public const int DefaultMaxTokens = 2000;
    public const int CharsPerToken = 4;
}