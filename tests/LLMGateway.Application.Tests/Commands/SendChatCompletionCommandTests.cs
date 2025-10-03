using FluentAssertions;
using LLMGateway.Application.Commands;
using LLMGateway.Application.DTOs;

namespace LLMGateway.Application.Tests.Commands;

public class SendChatCompletionCommandTests
{
    [Fact]
    public void Validate_ValidCommand_DoesNotThrow()
    {
        // Arrange
        var command = new SendChatCompletionCommand(
            Messages: new[]
            {
                new Message { Role = "user", Content = "Hello" }
            });

        // Act
        var act = () => command.Validate();

        // Assert
        act.Should().NotThrow();
    }

    [Fact]
    public void Validate_EmptyMessages_ThrowsArgumentException()
    {
        // Arrange
        var command = new SendChatCompletionCommand(
            Messages: Enumerable.Empty<Message>());

        // Act
        var act = () => command.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Messages cannot be empty*");
    }

    [Fact]
    public void Validate_EmptyMessageContent_ThrowsArgumentException()
    {
        // Arrange
        var command = new SendChatCompletionCommand(
            Messages: new[]
            {
                new Message { Role = "user", Content = "" }
            });

        // Act
        var act = () => command.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*content cannot be empty*");
    }

    [Theory]
    [InlineData(-0.1)]
    [InlineData(2.1)]
    [InlineData(5.0)]
    public void Validate_InvalidTemperature_ThrowsArgumentException(decimal temperature)
    {
        // Arrange
        var command = new SendChatCompletionCommand(
            Messages: new[]
            {
                new Message { Role = "user", Content = "Hello" }
            },
            Temperature: temperature);

        // Act
        var act = () => command.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*Temperature must be between 0 and 2*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void Validate_InvalidMaxTokens_ThrowsArgumentException(int maxTokens)
    {
        // Arrange
        var command = new SendChatCompletionCommand(
            Messages: new[]
            {
                new Message { Role = "user", Content = "Hello" }
            },
            MaxTokens: maxTokens);

        // Act
        var act = () => command.Validate();

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*MaxTokens must be positive*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(0.7)]
    [InlineData(1.5)]
    [InlineData(2.0)]
    public void Validate_ValidTemperature_DoesNotThrow(decimal temperature)
    {
        // Arrange
        var command = new SendChatCompletionCommand(
            Messages: new[]
            {
                new Message { Role = "user", Content = "Hello" }
            },
            Temperature: temperature);

        // Act
        var act = () => command.Validate();

        // Assert
        act.Should().NotThrow();
    }
}