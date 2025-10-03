using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;
using Microsoft.SemanticKernel;

namespace LLMGateway.Infrastructure.ChatCompletion;

/// <summary>
/// OpenRouter implementation of IChatCompletionService for Semantic Kernel.
/// Makes HTTP calls to OpenRouter API and populates metadata for cost tracking.
/// </summary>
public class OpenRouterChatCompletionService : IChatCompletionService
{
    private readonly HttpClient _httpClient;
    private readonly ILogger<OpenRouterChatCompletionService> _logger;
    private readonly OpenRouterConfig _config;

    public OpenRouterChatCompletionService(
        HttpClient httpClient,
        ILogger<OpenRouterChatCompletionService> logger,
        OpenRouterConfig config)
    {
        _httpClient = httpClient ?? throw new ArgumentNullException(nameof(httpClient));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config ?? throw new ArgumentNullException(nameof(config));
    }

    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>
    {
        ["ProviderName"] = "OpenRouter",
        ["ModelProvider"] = "OpenRouter"
    };

    public async Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting OpenRouter chat completion request");

        // Extract model and temperature from execution settings
        var model = "z-ai/glm-4.6";
        var temperature = 0.7;

        if (executionSettings?.ExtensionData != null)
        {
            if (executionSettings.ExtensionData.TryGetValue("model", out var modelValue) && modelValue is string modelStr)
            {
                model = modelStr;
            }
            if (executionSettings.ExtensionData.TryGetValue("temperature", out var tempValue) && tempValue is double tempDouble)
            {
                temperature = tempDouble;
            }
        }

        _logger.LogInformation("Using model: {Model}, temperature: {Temperature}", model, temperature);

        // Build OpenRouter request
        var request = new OpenRouterRequest
        {
            Model = model,
            Messages = chatHistory.Select(m => new OpenRouterMessage
            {
                Role = m.Role.ToString().ToLower(),
                Content = m.Content ?? string.Empty
            }).ToArray(),
            Temperature = temperature,
            MaxTokens = 2000 // Default max tokens
        };

        // Make HTTP request
        var response = await _httpClient.PostAsJsonAsync("chat/completions", request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var responseData = await response.Content.ReadFromJsonAsync<OpenRouterResponse>(cancellationToken: cancellationToken);
        if (responseData == null)
        {
            throw new InvalidOperationException("Failed to deserialize OpenRouter response");
        }

        _logger.LogInformation("Received response with {ChoiceCount} choices, usage: prompt={PromptTokens}, completion={CompletionTokens}",
            responseData.Choices?.Length ?? 0,
            responseData.Usage?.PromptTokens ?? 0,
            responseData.Usage?.CompletionTokens ?? 0);

        // Convert to SK format
        var result = new ChatMessageContent(
            role: AuthorRole.Assistant,
            content: responseData.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty);

        // CRITICAL: Populate metadata for cost tracking
        result.Metadata = new Dictionary<string, object?>
        {
            ["input_tokens"] = responseData.Usage?.PromptTokens ?? 0,
            ["output_tokens"] = responseData.Usage?.CompletionTokens ?? 0
        };

        _logger.LogInformation("Populated metadata: input_tokens={InputTokens}, output_tokens={OutputTokens}",
            result.Metadata["input_tokens"], result.Metadata["output_tokens"]);

        return new[] { result };
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        // For MVP, implement non-streaming version first
        // Streaming can be added later if needed
        var results = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
        foreach (var result in results)
        {
            yield return new StreamingChatMessageContent(result.Role, result.Content ?? string.Empty);
        }
    }
}

/// <summary>
/// Configuration for OpenRouter service
/// </summary>
public class OpenRouterConfig
{
    public string BaseUrl { get; set; } = "https://openrouter.ai/api/v1/";
    public string ApiKey { get; set; } = string.Empty;
    public int TimeoutSeconds { get; set; } = 30;
    public int MaxRetries { get; set; } = 3;
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public int CircuitBreakerCooldownSeconds { get; set; } = 30;
}

/// <summary>
/// OpenRouter API request format
/// </summary>
internal class OpenRouterRequest
{
    public required string Model { get; set; }
    public required OpenRouterMessage[] Messages { get; set; }
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;
}

/// <summary>
/// OpenRouter message format
/// </summary>
internal class OpenRouterMessage
{
    public required string Role { get; set; }
    public required string Content { get; set; }
}

/// <summary>
/// OpenRouter API response format
/// </summary>
internal class OpenRouterResponse
{
    public string? Id { get; set; }
    public string? Model { get; set; }
    public OpenRouterChoice[]? Choices { get; set; }
    public OpenRouterUsage? Usage { get; set; }
}

/// <summary>
/// OpenRouter choice format
/// </summary>
internal class OpenRouterChoice
{
    public OpenRouterMessage? Message { get; set; }
    public string? FinishReason { get; set; }
}

/// <summary>
/// OpenRouter usage format
/// </summary>
internal class OpenRouterUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}