using FluentAssertions;
using LLMGateway.Application.Plugins;
using LLMGateway.Application.Tests.TestDoubles;
using LLMGateway.Domain.Constants;
using LLMGateway.Domain.Entities;
using LLMGateway.Domain.Interfaces;
using Microsoft.Extensions.Logging;
using Moq;

namespace LLMGateway.Application.Tests.Plugins;

public class CostTrackingPluginTests
{
    private readonly InMemoryRequestLogRepository _logRepository;
    private readonly InMemoryModelPricingRepository _pricingRepository;
    private readonly CostTrackingPlugin _plugin;

    public CostTrackingPluginTests()
    {
        _logRepository = new InMemoryRequestLogRepository();
        _pricingRepository = new InMemoryModelPricingRepository();
        var mockLogger = new Mock<ILogger<CostTrackingPlugin>>();

        _plugin = new CostTrackingPlugin(
            _logRepository,
            _pricingRepository,
            mockLogger.Object);
    }

    [Fact]
    public async Task TrackCost_ValidRequest_PersistsLog()
    {
        // Arrange
        var modelName = ModelDefaults.DefaultModel;
        var inputTokens = 100;
        var outputTokens = 200;

        // Act
        await _plugin.TrackCostAsync(
            modelName,
            inputTokens,
            outputTokens,
            "OpenRouter",
            1234,
            wasFallback: false);

        // Assert
        var logs = await _logRepository.GetRecentAsync(10);
        logs.Should().HaveCount(1);

        var log = logs.First();
        log.ModelUsed.Value.Should().Be(modelName);
        log.InputTokens.Value.Should().Be(inputTokens);
        log.OutputTokens.Value.Should().Be(outputTokens);
        log.ProviderName.Should().Be("OpenRouter");
        log.WasFallback.Should().BeFalse();
    }

    [Fact]
    public async Task TrackCost_CalculatesCostCorrectly()
    {
        // Arrange
        var modelName = ModelDefaults.DefaultModel;
        var inputTokens = 1_000_000; // 1M tokens
        var outputTokens = 1_000_000; // 1M tokens

        // Expected: (1M / 1M) * 0.0001 + (1M / 1M) * 0.0002 = 0.0003

        // Act
        var cost = await _plugin.TrackCostAsync(
            modelName,
            inputTokens,
            outputTokens,
            "OpenRouter",
            1000,
            wasFallback: false);

        // Assert
        cost.Should().Be(0.0003m);
        var logs = await _logRepository.GetRecentAsync(1);
        var log = logs.First();
        log.EstimatedCost.ValueUsd.Should().Be(0.0003m);
    }

    [Fact]
    public async Task TrackCost_MissingPricing_UsesZeroCost()
    {
        // Arrange
        var unknownModel = "unknown/model";

        // Act
        var cost = await _plugin.TrackCostAsync(
            unknownModel,
            100,
            200,
            "OpenRouter",
            1000,
            wasFallback: false);

        // Assert
        cost.Should().Be(0m);
        var logs = await _logRepository.GetRecentAsync(1);
        var log = logs.First();
        log.EstimatedCost.ValueUsd.Should().Be(0m);
    }

    [Fact]
    public async Task TrackCost_WithFallback_SetsFlagCorrectly()
    {
        // Act
        await _plugin.TrackCostAsync(
            ModelDefaults.BalancedModel,
            50,
            150,
            "OpenRouter",
            2000,
            wasFallback: true);

        // Assert
        var logs = await _logRepository.GetRecentAsync(1);
        logs.First().WasFallback.Should().BeTrue();
    }
}