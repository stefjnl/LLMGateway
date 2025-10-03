using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Net.Http.Json;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json;

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
        var model = "google/gemini-2.5-flash-lite-preview-09-2025";
        var temperature = 0.7;

        if (executionSettings?.ExtensionData != null)
        {
            // Try both "ModelId" (from SK) and "model" (OpenRouter format)
            if (executionSettings.ExtensionData.TryGetValue("ModelId", out var modelValue) && modelValue is string modelStr)
            {
                model = modelStr;
            }
            else if (executionSettings.ExtensionData.TryGetValue("model", out var modelValue2) && modelValue2 is string modelStr2)
            {
                model = modelStr2;
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
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting OpenRouter streaming chat completion request");

        // Extract model and temperature from execution settings
        var model = "google/gemini-2.5-flash-lite-preview-09-2025";
        var temperature = 0.7;

        if (executionSettings?.ExtensionData != null)
        {
            // Try both "ModelId" (from SK) and "model" (OpenRouter format)
            if (executionSettings.ExtensionData.TryGetValue("ModelId", out var modelValue) && modelValue is string modelStr)
            {
                model = modelStr;
            }
            else if (executionSettings.ExtensionData.TryGetValue("model", out var modelValue2) && modelValue2 is string modelStr2)
            {
                model = modelStr2;
            }
            
            if (executionSettings.ExtensionData.TryGetValue("temperature", out var tempValue) && tempValue is double tempDouble)
            {
                temperature = tempDouble;
            }
        }

        _logger.LogInformation("Using streaming model: {Model}, temperature: {Temperature}", model, temperature);

        // Build OpenRouter streaming request
        var request = new OpenRouterRequest
        {
            Model = model,
            Messages = chatHistory.Select(m => new OpenRouterMessage
            {
                Role = m.Role.ToString().ToLower(),
                Content = m.Content ?? string.Empty
            }).ToArray(),
            Temperature = temperature,
            MaxTokens = 2000, // Default max tokens
            Stream = true // Enable streaming
        };

        // Create HTTP request with streaming
        var requestJson = JsonSerializer.Serialize(request, new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase
        });

        var httpRequest = new HttpRequestMessage(HttpMethod.Post, "chat/completions")
        {
            Content = new StringContent(requestJson, Encoding.UTF8, "application/json")
        };

        using var response = await _httpClient.SendAsync(httpRequest, HttpCompletionOption.ResponseHeadersRead, cancellationToken);
        response.EnsureSuccessStatusCode();

        using var stream = await response.Content.ReadAsStreamAsync(cancellationToken);
        using var reader = new StreamReader(stream);

        var totalTokens = 0;
        var buffer = new StringBuilder();

        while (!reader.EndOfStream && !cancellationToken.IsCancellationRequested)
        {
            var line = await reader.ReadLineAsync(cancellationToken);
            if (string.IsNullOrWhiteSpace(line))
                continue;

            if (line.StartsWith("data: "))
            {
                var data = line.Substring(6);
                if (data == "[DONE]")
                    break;

                var chunk = JsonSerializer.Deserialize<OpenRouterStreamResponse>(data, new JsonSerializerOptions
                {
                    PropertyNamingPolicy = JsonNamingPolicy.CamelCase
                });

                if (chunk?.Choices?.Length > 0)
                {
                    var choice = chunk.Choices[0];
                    if (choice.Delta?.Content != null)
                    {
                        totalTokens++;
                        yield return new StreamingChatMessageContent(
                            AuthorRole.Assistant,
                            choice.Delta.Content,
                            choice.Delta.Content,
                            metadata: new Dictionary<string, object?>
                            {
                                ["input_tokens"] = chunk.Usage?.PromptTokens ?? 0,
                                ["output_tokens"] = chunk.Usage?.CompletionTokens ?? totalTokens,
                                ["model"] = chunk.Model
                            });
                    }
                }
            }
        }

        _logger.LogInformation("Streaming completed with {TotalTokens} tokens", totalTokens);
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
    public int HealthCheckTimeoutSeconds { get; set; } = 5;
    public int MaxRetries { get; set; } = 3;
    public int CircuitBreakerFailureThreshold { get; set; } = 5;
    public int CircuitBreakerCooldownSeconds { get; set; } = 30;
    public int MaxConnectionsPerServer { get; set; } = 100;
    public int ConnectionLifetimeMinutes { get; set; } = 5;
    public bool UseHttp2 { get; set; } = true;
}

/// <summary>
/// OpenRouter API request format
/// </summary>
public class OpenRouterRequest
{
    public required string Model { get; set; }
    public required OpenRouterMessage[] Messages { get; set; }
    public double Temperature { get; set; } = 0.7;
    public int MaxTokens { get; set; } = 2000;
    public bool Stream { get; set; } = false;
}

/// <summary>
/// OpenRouter message format
/// </summary>
public class OpenRouterMessage
{
    public required string Role { get; set; }
    public required string Content { get; set; }
}

/// <summary>
/// OpenRouter API response format
/// </summary>
public class OpenRouterResponse
{
    public string? Id { get; set; }
    public string? Model { get; set; }
    public OpenRouterChoice[]? Choices { get; set; }
    public OpenRouterUsage? Usage { get; set; }
}

/// <summary>
/// OpenRouter choice format
/// </summary>
public class OpenRouterChoice
{
    public OpenRouterMessage? Message { get; set; }
    public string? FinishReason { get; set; }
}

/// <summary>
/// OpenRouter usage format
/// </summary>
public class OpenRouterUsage
{
    public int PromptTokens { get; set; }
    public int CompletionTokens { get; set; }
    public int TotalTokens { get; set; }
}

/// <summary>
/// OpenRouter streaming response format
/// </summary>
public class OpenRouterStreamResponse
{
    public string? Id { get; set; }
    public string? Model { get; set; }
    public OpenRouterStreamChoice[]? Choices { get; set; }
    public OpenRouterUsage? Usage { get; set; }
}

/// <summary>
/// OpenRouter streaming choice format
/// </summary>
public class OpenRouterStreamChoice
{
    public int Index { get; set; }
    public OpenRouterStreamDelta? Delta { get; set; }
    public string? FinishReason { get; set; }
}

/// <summary>
/// OpenRouter streaming delta format
/// </summary>
public class OpenRouterStreamDelta
{
    public string? Role { get; set; }
    public string? Content { get; set; }
}