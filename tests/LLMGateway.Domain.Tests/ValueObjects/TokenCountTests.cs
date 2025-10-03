using FluentAssertions;
using LLMGateway.Domain.ValueObjects;

namespace LLMGateway.Domain.Tests.ValueObjects;

public class TokenCountTests
{
    [Fact]
    public void From_ValidCount_CreatesInstance()
    {
        // Act
        var tokens = TokenCount.From(100);

        // Assert
        tokens.Value.Should().Be(100);
    }

    [Fact]
    public void From_NegativeCount_ThrowsArgumentException()
    {
        // Act
        var act = () => TokenCount.From(-1);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be negative*");
    }

    [Theory]
    [InlineData("Hello world", 2)]      // 11 chars / 4 = 2
    [InlineData("Test", 1)]              // 4 chars / 4 = 1
    [InlineData("", 0)]                  // Empty = 0
    [InlineData("A very long text with many words", 8)] // 34 chars / 4 = 8
    public void EstimateFromText_CalculatesApproximateTokens(
        string text,
        int expectedTokens)
    {
        // Act
        var tokens = TokenCount.EstimateFromText(text);

        // Assert
        tokens.Value.Should().Be(expectedTokens);
    }

    [Fact]
    public void ExceedsLimit_TokensAboveLimit_ReturnsTrue()
    {
        // Arrange
        var tokens = TokenCount.From(15000);

        // Act
        var exceeds = tokens.ExceedsLimit(10000);

        // Assert
        exceeds.Should().BeTrue();
    }

    [Fact]
    public void ExceedsLimit_TokensBelowLimit_ReturnsFalse()
    {
        // Arrange
        var tokens = TokenCount.From(5000);

        // Act
        var exceeds = tokens.ExceedsLimit(10000);

        // Assert
        exceeds.Should().BeFalse();
    }
}