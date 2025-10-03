using LLMGateway.Domain.Constants;
using LLMGateway.Domain.Exceptions;
using LLMGateway.Domain.ValueObjects;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;

namespace LLMGateway.Application.Plugins;

public class ModelSelectionPlugin
{
    private readonly ILogger<ModelSelectionPlugin> _logger;

    public ModelSelectionPlugin(ILogger<ModelSelectionPlugin> logger)
    {
        _logger = logger;
    }

    [KernelFunction("select_model")]
    public Task<ModelName> SelectModelAsync(int tokenCount, string? userModel)
    {
        // Validate token limit
        if (tokenCount > ModelDefaults.LargeContextLimit)
        {
            _logger.LogWarning(
                "Token count {TokenCount} exceeds maximum limit {MaxLimit}",
                tokenCount,
                ModelDefaults.LargeContextLimit);

            throw new TokenLimitExceededException(
                TokenCount.From(tokenCount),
                ModelDefaults.LargeContextLimit);
        }

        // Rule 1: User-specified model takes precedence
        if (!string.IsNullOrWhiteSpace(userModel))
        {
            _logger.LogInformation(
                "Using user-specified model: {Model}",
                userModel);
            return Task.FromResult(ModelName.From(userModel));
        }

        // Rule 2: Large context detection
        if (tokenCount > ModelDefaults.StandardContextLimit)
        {
            _logger.LogInformation(
                "Token count {TokenCount} exceeds standard limit, selecting large context model: {Model}",
                tokenCount,
                ModelDefaults.LargeContextModel);
            return Task.FromResult(ModelName.From(ModelDefaults.LargeContextModel));
        }

        // Rule 3: Default to fast/cheap model
        _logger.LogInformation(
            "Using default model for {TokenCount} tokens: {Model}",
            tokenCount,
            ModelDefaults.DefaultModel);
        return Task.FromResult(ModelName.From(ModelDefaults.DefaultModel));
    }
}