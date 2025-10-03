using LLMGateway.Application.Commands;
using LLMGateway.Application.DTOs;
using LLMGateway.Application.Plugins;
using LLMGateway.Domain.Constants;
using LLMGateway.Domain.Exceptions;
using LLMGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using System.Diagnostics;
using System.Net;
using System.Runtime.CompilerServices;

namespace LLMGateway.Application.Orchestration;

public class KernelOrchestrator(
    KernelFactory kernelFactory,
    ModelSelectionPlugin modelSelection,
    CostTrackingPlugin costTracking,
    ProviderFallbackPlugin providerFallback,
    IProviderConfig providerConfig,
    ILogger<KernelOrchestrator> logger)
{
    public async Task<ChatResponse> SendChatCompletionAsync(
        SendChatCompletionCommand command,
        CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        try
        {
            // Validate command
            command.Validate();

            var kernel = kernelFactory.CreateWithPlugins();

            // Step 1: Estimate tokens and select model
            var estimatedTokens = EstimateTokens(command.Messages);
            var selectedModel = await modelSelection.SelectModelAsync(
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

            var estimatedCost = await costTracking.TrackCostAsync(
                finalModel,
                inputTokens,
                outputTokens,
                providerConfig.ProviderName,
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
            logger.LogError(ex, "Failed to complete chat request");
            throw;
        }
    }

    public async IAsyncEnumerable<StreamingChatResponse> SendStreamingChatCompletionAsync(
        SendChatCompletionCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();

        // Validate command
        command.Validate();

        var kernel = kernelFactory.CreateWithPlugins();

        // Step 1: Estimate tokens and select model
        var estimatedTokens = EstimateTokens(command.Messages);
        var selectedModel = await modelSelection.SelectModelAsync(
            estimatedTokens,
            command.Model);

        // Step 2: Call streaming completion service with retry/fallback
        var (finalModel, attempts, totalTokens) = await ExecuteStreamingWithFallbackCountAsync(
            kernel,
            command,
            selectedModel.Value,
            cancellationToken);

        stopwatch.Stop();

        // Step 3: Track cost
        var estimatedCost = await costTracking.TrackCostAsync(
            finalModel,
            ExtractInputTokensFromCommand(command),
            totalTokens,
            providerConfig.ProviderName,
            stopwatch.ElapsedMilliseconds,
            wasFallback: attempts > 1,
            cancellationToken);

        // Step 4: Send final metadata
        var responseTimeMs = stopwatch.ElapsedMilliseconds;
        var avgTokensPerSecond = responseTimeMs > 0
            ? (totalTokens * 1000.0) / responseTimeMs
            : 0;

        yield return new StreamingChatResponse
        {
            Type = "complete",
            Metadata = new StreamingMetadata
            {
                Content = string.Empty, // Content is streamed as chunks
                Model = finalModel,
                TotalTokens = totalTokens,
                ResponseTimeMs = responseTimeMs,
                AverageTokensPerSecond = avgTokensPerSecond,
                EstimatedCostUsd = estimatedCost,
                Provider = providerConfig.ProviderName
            }
        };
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

                if (results == null || !results.Any())
                {
                    throw new InvalidOperationException("Chat completion service returned no results");
                }

                return (results.First(), currentModel, attempts);
            }
            catch (Exception ex) when (IsTransientError(ex) && attempts < maxAttempts)
            {
                logger.LogWarning(
                    ex,
                    "Transient error on attempt {Attempt}/{MaxAttempts} for model {Model}",
                    attempts,
                    maxAttempts,
                    currentModel);

                // Get fallback model with attempt history
                currentModel = await providerFallback.GetFallbackModelAsync(
                    currentModel,
                    attemptedModels);
            }
        }

        throw new AllProvidersFailedException(attemptedModels);
    }

    public async IAsyncEnumerable<StreamingChatResponse> SendStreamingChatCompletionWithChunksAsync(
        SendChatCompletionCommand command,
        [EnumeratorCancellation] CancellationToken cancellationToken = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var totalTokens = 0;
        string finalModel = string.Empty;
        var attempts = 0;

        // Validate command
        command.Validate();

        var kernel = kernelFactory.CreateWithPlugins();

        // Step 1: Estimate tokens and select model
        var estimatedTokens = EstimateTokens(command.Messages);
        var selectedModel = await modelSelection.SelectModelAsync(
            estimatedTokens,
            command.Model);

        // Step 2: Call streaming completion service with retry/fallback
        await foreach (var response in ExecuteStreamingWithFallbackAsync(
            kernel,
            command,
            selectedModel.Value,
            cancellationToken))
        {
            if (response.Type == "chunk")
            {
                totalTokens++;
                yield return response;
            }
            else if (response.Type == "complete")
            {
                finalModel = response.Metadata?.Model ?? string.Empty;
                attempts = 1; // Will be updated if fallback occurred
            }
        }

        stopwatch.Stop();

        // Step 3: Track cost
        var estimatedCost = await costTracking.TrackCostAsync(
            finalModel,
            ExtractInputTokensFromCommand(command),
            totalTokens,
            providerConfig.ProviderName,
            stopwatch.ElapsedMilliseconds,
            wasFallback: attempts > 1,
            cancellationToken);

        // Step 4: Send final metadata
        var responseTimeMs = stopwatch.ElapsedMilliseconds;
        var avgTokensPerSecond = responseTimeMs > 0
            ? (totalTokens * 1000.0) / responseTimeMs
            : 0;

        yield return new StreamingChatResponse
        {
            Type = "complete",
            Metadata = new StreamingMetadata
            {
                Content = string.Empty, // Content is streamed as chunks
                Model = finalModel,
                TotalTokens = totalTokens,
                ResponseTimeMs = responseTimeMs,
                AverageTokensPerSecond = avgTokensPerSecond,
                EstimatedCostUsd = estimatedCost,
                Provider = providerConfig.ProviderName
            }
        };
    }

    private async IAsyncEnumerable<StreamingChatResponse> ExecuteStreamingWithFallbackAsync(
        Kernel kernel,
        SendChatCompletionCommand command,
        string initialModel,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        var currentModel = initialModel;
        var attempts = 0;
        var maxAttempts = 3;
        var attemptedModels = new List<string>();

        while (attempts < maxAttempts)
        {
            attempts++;
            attemptedModels.Add(currentModel);

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

            await foreach (var chunk in completionService.GetStreamingChatMessageContentsAsync(
                chatHistory,
                executionSettings,
                kernel,
                cancellationToken))
            {
                if (!string.IsNullOrEmpty(chunk.Content))
                {
                    yield return new StreamingChatResponse
                    {
                        Type = "chunk",
                        Content = chunk.Content
                    };
                }
            }

            // Signal completion with model info
            yield return new StreamingChatResponse
            {
                Type = "complete",
                Metadata = new StreamingMetadata
                {
                    Model = currentModel,
                    Provider = providerConfig.ProviderName
                }
            };

            yield break; // Success, exit the retry loop
        }

        throw new AllProvidersFailedException(attemptedModels);
    }

    private async Task<(string model, int attempts, int totalTokens)> ExecuteStreamingWithFallbackCountAsync(
        Kernel kernel,
        SendChatCompletionCommand command,
        string initialModel,
        CancellationToken cancellationToken)
    {
        var currentModel = initialModel;
        var attempts = 0;
        var maxAttempts = 3;
        var attemptedModels = new List<string>();
        var totalTokens = 0;

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

                await foreach (var chunk in completionService.GetStreamingChatMessageContentsAsync(
                    chatHistory,
                    executionSettings,
                    kernel,
                    cancellationToken))
                {
                    if (!string.IsNullOrEmpty(chunk.Content))
                    {
                        totalTokens++;
                    }
                }

                return (currentModel, attempts, totalTokens);
            }
            catch (Exception ex) when (IsTransientError(ex) && attempts < maxAttempts)
            {
                logger.LogWarning(
                    ex,
                    "Transient error on streaming attempt {Attempt}/{MaxAttempts} for model {Model}",
                    attempts,
                    maxAttempts,
                    currentModel);

                // Get fallback model with attempt history
                currentModel = await providerFallback.GetFallbackModelAsync(
                    currentModel,
                    attemptedModels);
            }
        }

        throw new AllProvidersFailedException(attemptedModels);
    }

    private static bool IsTransientError(Exception ex)
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

    private static int EstimateTokens(IEnumerable<Message> messages)
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
        // Debug: Log all metadata keys
        if (result.Metadata != null)
        {
            logger.LogDebug("Available metadata keys: {Keys}", string.Join(", ", result.Metadata.Keys));
        }

        // Try to extract from metadata if available
        if (result.Metadata?.TryGetValue("input_tokens", out var inputTokens) == true)
        {
            try
            {
                var tokenCount = Convert.ToInt32(inputTokens);
                logger.LogDebug("Extracted input_tokens: {Tokens}", tokenCount);
                return tokenCount;
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to parse input_tokens from metadata: {Value}",
                    inputTokens);
            }
        }

        // Fallback: estimate from prompt (will be refined in US-004)
        logger.LogWarning("Input tokens not found in metadata, using 0");
        return 0; // Will be estimated in Infrastructure layer
    }

    private int ExtractOutputTokens(ChatMessageContent result)
    {
        // Try to extract from metadata if available
        if (result.Metadata?.TryGetValue("output_tokens", out var outputTokens) == true)
        {
            try
            {
                return Convert.ToInt32(outputTokens);
            }
            catch (Exception ex)
            {
                logger.LogWarning(
                    ex,
                    "Failed to parse output_tokens from metadata: {Value}",
                    outputTokens);
            }
        }

        // Fallback: estimate from content length
        var contentLength = result.Content?.Length ?? 0;
        var estimated = contentLength / ModelDefaults.CharsPerToken;

        if (estimated > 0)
        {
            logger.LogDebug(
                "Output tokens not in metadata, using estimation: {Estimated}",
                estimated);
        }

        return estimated;
    }

    private int ExtractInputTokensFromCommand(SendChatCompletionCommand command)
    {
        // For now, estimate from the command messages
        // This will be refined in future iterations
        return EstimateTokens(command.Messages);
    }
}