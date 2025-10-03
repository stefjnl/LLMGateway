using FluentAssertions;
using LLMGateway.Domain.Entities;
using LLMGateway.Domain.ValueObjects;

namespace LLMGateway.Domain.Tests.Entities;

public class RequestLogTests
{
    [Fact]
    public void Create_ValidParameters_CreatesRequestLog()
    {
        // Arrange
        var model = ModelName.From("google/gemini-2.5-flash-lite-preview-09-2025");
        var inputTokens = TokenCount.From(10);
        var outputTokens = TokenCount.From(50);
        var cost = CostAmount.FromUsd(0.00001m);
        var responseTime = TimeSpan.FromMilliseconds(1234);

        // Act
        var log = RequestLog.Create(
            model,
            inputTokens,
            outputTokens,
            cost,
            "OpenRouter",
            responseTime);

        // Assert
        log.Id.Should().NotBeEmpty();
        log.Timestamp.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
        log.ModelUsed.Should().Be(model);
        log.InputTokens.Should().Be(inputTokens);
        log.OutputTokens.Should().Be(outputTokens);
        log.EstimatedCost.Should().Be(cost);
        log.ProviderName.Should().Be("OpenRouter");
        log.ResponseTime.Should().Be(responseTime);
        log.WasFallback.Should().BeFalse();
    }

    [Fact]
    public void Create_NullModel_ThrowsArgumentNullException()
    {
        // Act
        var act = () => RequestLog.Create(
            null!,
            TokenCount.From(10),
            TokenCount.From(50),
            CostAmount.FromUsd(0.01m),
            "OpenRouter",
            TimeSpan.FromSeconds(1));

        // Assert
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void TotalTokens_ReturnsInputPlusOutput()
    {
        // Arrange
        var log = RequestLog.Create(
            ModelName.From("google/gemini-2.5-flash-lite-preview-09-2025"),
            TokenCount.From(10),
            TokenCount.From(50),
            CostAmount.FromUsd(0.01m),
            "OpenRouter",
            TimeSpan.FromSeconds(1));

        // Act
        var total = log.TotalTokens();

        // Assert
        total.Value.Should().Be(60);
    }
}