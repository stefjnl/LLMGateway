using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace LLMGateway.Application.Tests.TestDoubles;

/// <summary>
/// Mock implementation of IChatCompletionService for testing purposes.
/// Returns predefined responses without making actual API calls.
/// </summary>
public class MockChatCompletionService : IChatCompletionService
{
    private readonly string _mockResponse;
    private readonly int _inputTokens;
    private readonly int _outputTokens;

    public MockChatCompletionService(string mockResponse = "Mock response from AI", int inputTokens = 10, int outputTokens = 20)
    {
        _mockResponse = mockResponse;
        _inputTokens = inputTokens;
        _outputTokens = outputTokens;
    }

    public IReadOnlyDictionary<string, object?> Attributes => new Dictionary<string, object?>
    {
        ["ProviderName"] = "MockProvider",
        ["ModelProvider"] = "MockProvider"
    };

    public Task<IReadOnlyList<ChatMessageContent>> GetChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        CancellationToken cancellationToken = default)
    {
        var result = new ChatMessageContent(
            role: AuthorRole.Assistant,
            content: _mockResponse);

        // Populate metadata for cost tracking
        result.Metadata = new Dictionary<string, object?>
        {
            ["input_tokens"] = _inputTokens,
            ["output_tokens"] = _outputTokens
        };

        return Task.FromResult<IReadOnlyList<ChatMessageContent>>(new[] { result });
    }

    public async IAsyncEnumerable<StreamingChatMessageContent> GetStreamingChatMessageContentsAsync(
        ChatHistory chatHistory,
        PromptExecutionSettings? executionSettings = null,
        Kernel? kernel = null,
        [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        // For MVP, implement non-streaming version first
        var results = await GetChatMessageContentsAsync(chatHistory, executionSettings, kernel, cancellationToken);
        foreach (var result in results)
        {
            yield return new StreamingChatMessageContent(result.Role, result.Content ?? string.Empty);
        }
    }
}