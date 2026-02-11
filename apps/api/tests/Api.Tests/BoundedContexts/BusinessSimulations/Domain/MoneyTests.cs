using Api.BoundedContexts.BusinessSimulations.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Domain;

/// <summary>
/// Unit tests for Money value object (Issue #3720)
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "BusinessSimulations")]
public sealed class MoneyTests
{
    [Fact]
    public void Create_WithValidAmount_ShouldCreateMoney()
    {
        // Act
        var money = Money.Create(100.50m, "EUR");

        // Assert
        money.Amount.Should().Be(100.50m);
        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Create_WithDefaultCurrency_ShouldUseEur()
    {
        // Act
        var money = Money.Create(50m);

        // Assert
        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Create_WithLowercaseCurrency_ShouldNormalize()
    {
        // Act
        var money = Money.Create(10m, "usd");

        // Assert
        money.Currency.Should().Be("USD");
    }

    [Fact]
    public void Create_WithZeroAmount_ShouldSucceed()
    {
        // Act
        var money = Money.Create(0m);

        // Assert
        money.Amount.Should().Be(0m);
    }

    [Fact]
    public void Create_WithNegativeAmount_ShouldThrow()
    {
        // Act
        var act = () => Money.Create(-10m);

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("amount");
    }

    [Fact]
    public void Create_WithEmptyCurrency_ShouldThrow()
    {
        // Act
        var act = () => Money.Create(10m, "");

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("currency");
    }

    [Fact]
    public void Create_WithInvalidCurrencyLength_ShouldThrow()
    {
        // Act
        var act = () => Money.Create(10m, "EU");

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("currency");
    }

    [Fact]
    public void Create_WithTooLongCurrency_ShouldThrow()
    {
        // Act
        var act = () => Money.Create(10m, "EURO");

        // Assert
        act.Should().Throw<ArgumentException>().WithParameterName("currency");
    }

    [Fact]
    public void Zero_ShouldReturnZeroEur()
    {
        // Act
        var money = Money.Zero();

        // Assert
        money.Amount.Should().Be(0m);
        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void InEur_ShouldCreateEurMoney()
    {
        // Act
        var money = Money.InEur(99.99m);

        // Assert
        money.Amount.Should().Be(99.99m);
        money.Currency.Should().Be("EUR");
    }

    [Fact]
    public void Equality_SameMoney_ShouldBeEqual()
    {
        // Arrange
        var money1 = Money.Create(50m, "EUR");
        var money2 = Money.Create(50m, "EUR");

        // Assert
        money1.Should().Be(money2);
    }

    [Fact]
    public void Equality_DifferentAmount_ShouldNotBeEqual()
    {
        // Arrange
        var money1 = Money.Create(50m, "EUR");
        var money2 = Money.Create(100m, "EUR");

        // Assert
        money1.Should().NotBe(money2);
    }

    [Fact]
    public void Equality_DifferentCurrency_ShouldNotBeEqual()
    {
        // Arrange
        var money1 = Money.Create(50m, "EUR");
        var money2 = Money.Create(50m, "USD");

        // Assert
        money1.Should().NotBe(money2);
    }
}
