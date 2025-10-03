using FluentAssertions;
using LLMGateway.Application.Plugins;
using LLMGateway.Domain.Constants;
using LLMGateway.Domain.Exceptions;
using Microsoft.Extensions.Logging;
using Moq;

namespace LLMGateway.Application.Tests.Plugins;

public class ModelSelectionPluginTests
{
    private readonly ModelSelectionPlugin _plugin;

    public ModelSelectionPluginTests()
    {
        var mockLogger = new Mock<ILogger<ModelSelectionPlugin>>();
        _plugin = new ModelSelectionPlugin(mockLogger.Object);
    }

    [Fact]
    public async Task SelectModel_UserSpecifiedModel_ReturnsUserModel()
    {
        // Arrange
        var userModel = "custom-model/test";

        // Act
        var result = await _plugin.SelectModelAsync(5000, userModel);

        // Assert
        result.Should().Be(userModel);
    }

    [Fact]
    public async Task SelectModel_LargeContext_ReturnsKimiModel()
    {
        // Arrange
        var tokenCount = 15000; // Exceeds StandardContextLimit (10,000)

        // Act
        var result = await _plugin.SelectModelAsync(tokenCount, null);

        // Assert
        result.Should().Be(ModelDefaults.LargeContextModel);
    }

    [Fact]
    public async Task SelectModel_StandardContext_ReturnsDefaultModel()
    {
        // Arrange
        var tokenCount = 5000; // Below StandardContextLimit

        // Act
        var result = await _plugin.SelectModelAsync(tokenCount, null);

        // Assert
        result.Should().Be(ModelDefaults.DefaultModel);
    }

    [Fact]
    public async Task SelectModel_ExceedsMaxLimit_ThrowsTokenLimitExceededException()
    {
        // Arrange
        var tokenCount = 250000; // Exceeds LargeContextLimit (200,000)

        // Act
        var act = () => _plugin.SelectModelAsync(tokenCount, null);

        // Assert
        await act.Should().ThrowAsync<TokenLimitExceededException>()
            .WithMessage("*exceeds limit*");
    }

    [Fact]
    public async Task SelectModel_AtStandardLimit_ReturnsDefaultModel()
    {
        // Arrange
        var tokenCount = ModelDefaults.StandardContextLimit; // Exactly at limit

        // Act
        var result = await _plugin.SelectModelAsync(tokenCount, null);

        // Assert
        result.Should().Be(ModelDefaults.DefaultModel);
    }
}