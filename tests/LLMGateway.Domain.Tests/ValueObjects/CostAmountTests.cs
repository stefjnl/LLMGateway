using FluentAssertions;
using LLMGateway.Domain.ValueObjects;

namespace LLMGateway.Domain.Tests.ValueObjects;

public class CostAmountTests
{
    [Fact]
    public void FromUsd_ValidAmount_CreatesInstance()
    {
        // Act
        var cost = CostAmount.FromUsd(0.123456m);

        // Assert
        cost.ValueUsd.Should().Be(0.123456m);
    }

    [Fact]
    public void FromUsd_NegativeAmount_ThrowsArgumentException()
    {
        // Act
        var act = () => CostAmount.FromUsd(-0.01m);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public void FromUsd_RoundsToSixDecimalPlaces()
    {
        // Act
        var cost = CostAmount.FromUsd(0.1234567890m);

        // Assert
        cost.ValueUsd.Should().Be(0.123457m); // Rounded
    }

    [Fact]
    public void Zero_ReturnsZeroCost()
    {
        // Act
        var cost = CostAmount.Zero;

        // Assert
        cost.ValueUsd.Should().Be(0m);
    }

    [Fact]
    public void Add_TwoCosts_ReturnsSum()
    {
        // Arrange
        var cost1 = CostAmount.FromUsd(0.000001m);
        var cost2 = CostAmount.FromUsd(0.000002m);

        // Act
        var total = cost1.Add(cost2);

        // Assert
        total.ValueUsd.Should().Be(0.000003m);
    }
}