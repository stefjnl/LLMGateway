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
    public async Task GetFallbackModel_DefaultModelFails_ReturnsBalancedModel()
    {
        // Act
        var result = await _plugin.GetFallbackModelAsync(ModelDefaults.DefaultModel);

        // Assert
        result.Should().Be(ModelDefaults.BalancedModel);
    }

    [Fact]
    public async Task GetFallbackModel_BalancedModelFails_ReturnsLargeContextModel()
    {
        // Act
        var result = await _plugin.GetFallbackModelAsync(ModelDefaults.BalancedModel);

        // Assert
        result.Should().Be(ModelDefaults.LargeContextModel);
    }

    [Fact]
    public async Task GetFallbackModel_AllModelsFailed_ThrowsAllProvidersFailedException()
    {
        // Act
        var act = () => _plugin.GetFallbackModelAsync(ModelDefaults.LargeContextModel);

        // Assert
        await act.Should().ThrowAsync<AllProvidersFailedException>()
            .WithMessage("*All providers failed*");
    }

    [Fact]
    public async Task GetFallbackModel_UnknownModel_ThrowsAllProvidersFailedException()
    {
        // Act
        var act = () => _plugin.GetFallbackModelAsync("unknown-model");

        // Assert
        await act.Should().ThrowAsync<AllProvidersFailedException>();
    }
}