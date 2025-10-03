using FluentAssertions;
using LLMGateway.Application.Plugins;
using LLMGateway.Domain.Constants;
using LLMGateway.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace LLMGateway.Application.Tests.Plugins;

public class ProviderFallbackPluginTests
{
    private readonly ProviderFallbackPlugin _plugin;

    public ProviderFallbackPluginTests()
    {
        var mockLogger = new Mock<ILogger<ProviderFallbackPlugin>>();
        _plugin = new ProviderFallbackPlugin(mockLogger.Object);
    }

    [Fact]
    public async Task GetFallbackModel_LargeContextModelFails_ReturnsBalancedModel()
    {
        // Arrange
        var attemptedModels = new List<string> { ModelDefaults.LargeContextModel };

        // Act
        var result = await _plugin.GetFallbackModelAsync(ModelDefaults.LargeContextModel, attemptedModels);

        // Assert
        result.Should().Be(ModelDefaults.BalancedModel);
    }

    [Fact]
    public async Task GetFallbackModel_BalancedModelFails_ReturnsDefaultModel()
    {
        // Arrange
        var attemptedModels = new List<string> { ModelDefaults.BalancedModel };

        // Act
        var result = await _plugin.GetFallbackModelAsync(ModelDefaults.BalancedModel, attemptedModels);

        // Assert
        result.Should().Be(ModelDefaults.DefaultModel);
    }

    [Fact]
    public async Task GetFallbackModel_DefaultModelFails_ReturnsLargeContextModel()
    {
        // Arrange
        var attemptedModels = new List<string> { ModelDefaults.DefaultModel };

        // Act
        var result = await _plugin.GetFallbackModelAsync(ModelDefaults.DefaultModel, attemptedModels);

        // Assert
        result.Should().Be(ModelDefaults.LargeContextModel);
    }

    [Fact]
    public async Task GetFallbackModel_StartingFromKimiK2_TriesDeepSeek()
    {
        // Arrange
        var attemptedModels = new List<string> { ModelDefaults.LargeContextModel };

        // Act
        var result = await _plugin.GetFallbackModelAsync(
            ModelDefaults.LargeContextModel,
            attemptedModels);

        // Assert
        result.Should().Be(ModelDefaults.BalancedModel);
    }

    [Fact]
    public async Task GetFallbackModel_AllModelsAttempted_ThrowsException()
    {
        // Arrange
        var attemptedModels = new List<string>
        {
            ModelDefaults.LargeContextModel,
            ModelDefaults.BalancedModel,
            ModelDefaults.DefaultModel
        };

        // Act
        var act = () => _plugin.GetFallbackModelAsync(
            ModelDefaults.DefaultModel,
            attemptedModels);

        // Assert
        await act.Should().ThrowAsync<AllProvidersFailedException>()
            .Where(ex => ex.AttemptedProviders.Count == 3);
    }

    [Fact]
    public async Task GetFallbackModel_CircularFallback_SkipsAttemptedModels()
    {
        // Arrange - already tried default and balanced
        var attemptedModels = new List<string>
        {
            ModelDefaults.DefaultModel,
            ModelDefaults.BalancedModel
        };

        // Act - failed on balanced, should circle back to Kimi-K2
        var result = await _plugin.GetFallbackModelAsync(
            ModelDefaults.BalancedModel,
            attemptedModels);

        // Assert
        result.Should().Be(ModelDefaults.LargeContextModel);
    }

    [Fact]
    public async Task GetFallbackModel_UnknownModel_ThrowsAllProvidersFailedException()
    {
        // Arrange
        var attemptedModels = new List<string> { "unknown-model" };

        // Act
        var act = () => _plugin.GetFallbackModelAsync("unknown-model", attemptedModels);

        // Assert
        await act.Should().ThrowAsync<AllProvidersFailedException>();
    }
}