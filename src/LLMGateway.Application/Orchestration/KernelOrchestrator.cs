using System.Diagnostics;
using System.Net;
using LLMGateway.Application.Commands;
using LLMGateway.Application.DTOs;
using LLMGateway.Application.Plugins;
using LLMGateway.Domain.Constants;
using LLMGateway.Domain.Exceptions;
using LLMGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LLMGateway.Application.Orchestration;

public class KernelOrchestrator
{
    private readonly KernelFactory _kernelFactory;
    private readonly ModelSelectionPlugin _modelSelection;
    private readonly CostTrackingPlugin _costTracking;
    private readonly ProviderFallbackPlugin _providerFallback;
    private readonly IProviderConfig _providerConfig;
    private readonly ILogger<KernelOrchestrator> _logger;

    public KernelOrchestrator(
        KernelFactory kernelFactory,
        ModelSelectionPlugin modelSelection,
        CostTrackingPlugin costTracking,
        ProviderFallbackPlugin providerFallback,
        IProviderConfig providerConfig,
        ILogger<KernelOrchestrator> logger)
    {
        _kernelFactory = kernelFactory;
        _modelSelection = modelSelection;
        _costTracking = costTracking;
        _providerFallback = providerFallback;
        _providerConfig = providerConfig;
        _logger = logger;
    }

    public async Task<ChatResponse> SendChatCompletionAsync(
        SendChatCompletionCommand command,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate command
            command.Validate();

            var kernel = _kernelFactory.CreateWithPlugins();

            // Step 1: Estimate tokens and select model
            var estimatedTokens = EstimateTokens(command.Messages);
            var selectedModel = await _modelSelection.SelectModelAsync(
                estimatedTokens,
                command.Model);

            // Step 2: Call completion service with retry/fallback
            var (result, finalModel, attempts) = await ExecuteWithFallbackAsync(
                kernel,
                command,
                selectedModel.Value,
                cancellationToken);

            stopwatch.Stop();

            // Step 3: Extract metadata and track cost
            var inputTokens = ExtractInputTokens(result);
            var outputTokens = ExtractOutputTokens(result);

            var estimatedCost = await _costTracking.TrackCostAsync(
                finalModel,
                inputTokens,
                outputTokens,
                _providerConfig.ProviderName,
                stopwatch.ElapsedMilliseconds,
                wasFallback: attempts > 1,
                cancellationToken);

            // Step 4: Map to response
            return new ChatResponse
            {
                Content = result.Content ?? string.Empty,
                Model = finalModel,
                TokensUsed = inputTokens + outputTokens,
                EstimatedCostUsd = estimatedCost,
                ResponseTime = stopwatch.Elapsed
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to complete chat request");
            throw;
        }
    }

    private async Task<(ChatMessageContent result, string model, int attempts)> ExecuteWithFallbackAsync(
        Kernel kernel,
        SendChatCompletionCommand command,
        string initialModel,
        CancellationToken cancellationToken)
    {
        var currentModel = initialModel;
        var attempts = 0;
        var maxAttempts = 3;
        var attemptedModels = new List<string>();

        while (attempts < maxAttempts)
        {
            attempts++;
            attemptedModels.Add(currentModel);

            try
            {
                var chatHistory = BuildChatHistory(command.Messages);
                var completionService = kernel.GetRequiredService<IChatCompletionService>();

                var executionSettings = new PromptExecutionSettings
                {
                    ModelId = currentModel,
                    ExtensionData = new Dictionary<string, object>
                    {
                        ["temperature"] = (double)(command.Temperature ?? ModelDefaults.DefaultTemperature),
                        ["max_tokens"] = command.MaxTokens ?? ModelDefaults.DefaultMaxTokens
                    }
                };

                var results = await completionService.GetChatMessageContentsAsync(
                    chatHistory,
                    executionSettings,
                    kernel,
                    cancellationToken);

                return (results.First(), currentModel, attempts);
            }
            catch (Exception ex) when (IsTransientError(ex) && attempts < maxAttempts)
            {
                _logger.LogWarning(
                    ex,
                    "Transient error on attempt {Attempt}/{MaxAttempts} for model {Model}",
                    attempts,
                    maxAttempts,
                    currentModel);

                // Get fallback model
                currentModel = await _providerFallback.GetFallbackModelAsync(currentModel);
            }
        }

        throw new AllProvidersFailedException(attemptedModels);
    }

    private bool IsTransientError(Exception ex)
    {
        return ex switch
        {
            HttpRequestException httpEx =>
                httpEx.StatusCode == HttpStatusCode.TooManyRequests ||
                (int?)httpEx.StatusCode >= 500,

            TaskCanceledException => true,

            _ => false
        };
    }

    private int EstimateTokens(IEnumerable<Message> messages)
    {
        var totalChars = messages.Sum(m => m.Content.Length);
        return totalChars / ModelDefaults.CharsPerToken; // Simple estimation: ~4 chars per token
    }

    private ChatHistory BuildChatHistory(IEnumerable<Message> messages)
    {
        var chatHistory = new ChatHistory();

        foreach (var message in messages)
        {
            chatHistory.AddMessage(
                new AuthorRole(message.Role),
                message.Content);
        }

        return chatHistory;
    }

    private int ExtractInputTokens(ChatMessageContent result)
    {
        // Try to extract from metadata if available
        if (result.Metadata?.TryGetValue("input_tokens", out var inputTokens) == true)
        {
            return Convert.ToInt32(inputTokens);
        }

        // Fallback: estimate from result (will be more accurate in Infrastructure layer)
        return 0;
    }

    private int ExtractOutputTokens(ChatMessageContent result)
    {
        // Try to extract from metadata if available
        if (result.Metadata?.TryGetValue("output_tokens", out var outputTokens) == true)
        {
            return Convert.ToInt32(outputTokens);
        }

        // Fallback: estimate from content
        return (result.Content?.Length ?? 0) / 4;
    }
}