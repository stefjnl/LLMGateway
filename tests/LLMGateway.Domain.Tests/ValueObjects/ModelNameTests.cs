using FluentAssertions;
using LLMGateway.Domain.ValueObjects;

namespace LLMGateway.Domain.Tests.ValueObjects;

public class ModelNameTests
{
    [Fact]
    public void From_ValidModelName_CreatesInstance()
    {
        // Act
        var modelName = ModelName.From("z-ai/glm-4.6");

        // Assert
        modelName.Value.Should().Be("z-ai/glm-4.6");
        modelName.Provider.Should().Be("z-ai");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void From_InvalidModelName_ThrowsArgumentException(string? invalid)
    {
        // Act
        var act = () => ModelName.From(invalid!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be null or empty*");
    }

    [Fact]
    public void From_ModelNameWithoutSlash_ExtractsUnknownProvider()
    {
        // Act
        var modelName = ModelName.From("gpt-4");

        // Assert
        modelName.Value.Should().Be("gpt-4");
        modelName.Provider.Should().Be("unknown");
    }

    [Fact]
    public void Equals_SameValue_ReturnsTrue()
    {
        // Arrange
        var model1 = ModelName.From("z-ai/glm-4.6");
        var model2 = ModelName.From("z-ai/glm-4.6");

        // Act & Assert
        model1.Should().Be(model2);
        (model1 == model2).Should().BeTrue();
    }

    [Fact]
    public void Equals_DifferentValue_ReturnsFalse()
    {
        // Arrange
        var model1 = ModelName.From("z-ai/glm-4.6");
        var model2 = ModelName.From("moonshotai/Kimi-K2");

        // Act & Assert
        model1.Should().NotBe(model2);
        (model1 != model2).Should().BeTrue();
    }
}