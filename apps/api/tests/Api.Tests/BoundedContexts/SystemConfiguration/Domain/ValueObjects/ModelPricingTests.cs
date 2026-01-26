using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Tests for the ModelPricing value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 9
/// </summary>
[Trait("Category", "Unit")]
public sealed class ModelPricingTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidData_CreatesInstance()
    {
        // Arrange & Act
        var pricing = new ModelPricing(
            inputPricePerMillion: 2.5m,
            outputPricePerMillion: 10m,
            currency: "USD");

        // Assert
        pricing.InputPricePerMillion.Should().Be(2.5m);
        pricing.OutputPricePerMillion.Should().Be(10m);
        pricing.Currency.Should().Be("USD");
    }

    [Fact]
    public void Constructor_WithDefaultValues_UsesDefaults()
    {
        // Act
        var pricing = new ModelPricing();

        // Assert
        pricing.InputPricePerMillion.Should().Be(0m);
        pricing.OutputPricePerMillion.Should().Be(0m);
        pricing.Currency.Should().Be("USD");
    }

    [Fact]
    public void Constructor_WithZeroPrices_Succeeds()
    {
        // Act
        var pricing = new ModelPricing(0m, 0m, "EUR");

        // Assert
        pricing.InputPricePerMillion.Should().Be(0m);
        pricing.OutputPricePerMillion.Should().Be(0m);
        pricing.Currency.Should().Be("EUR");
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Constructor_WithNegativeInputPrice_ThrowsArgumentException()
    {
        // Act
        var action = () => new ModelPricing(
            inputPricePerMillion: -1m,
            outputPricePerMillion: 10m,
            currency: "USD");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Input price cannot be negative*");
    }

    [Fact]
    public void Constructor_WithNegativeOutputPrice_ThrowsArgumentException()
    {
        // Act
        var action = () => new ModelPricing(
            inputPricePerMillion: 2.5m,
            outputPricePerMillion: -1m,
            currency: "USD");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Output price cannot be negative*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyCurrency_ThrowsArgumentException(string? currency)
    {
        // Act
        var action = () => new ModelPricing(
            inputPricePerMillion: 2.5m,
            outputPricePerMillion: 10m,
            currency: currency!);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Currency cannot be empty*");
    }

    #endregion

    #region Free Static Property Tests

    [Fact]
    public void Free_ReturnsZeroPricing()
    {
        // Act
        var pricing = ModelPricing.Free;

        // Assert
        pricing.InputPricePerMillion.Should().Be(0m);
        pricing.OutputPricePerMillion.Should().Be(0m);
    }

    #endregion

    #region CalculateCost Tests

    [Fact]
    public void CalculateCost_WithZeroTokens_ReturnsZero()
    {
        // Arrange
        var pricing = new ModelPricing(2.5m, 10m, "USD");

        // Act
        var cost = pricing.CalculateCost(0, 0);

        // Assert
        cost.Should().Be(0m);
    }

    [Fact]
    public void CalculateCost_WithOnlyInputTokens_CalculatesCorrectly()
    {
        // Arrange
        var pricing = new ModelPricing(
            inputPricePerMillion: 2.5m,
            outputPricePerMillion: 10m,
            currency: "USD");

        // Act - 1 million input tokens at $2.50/million = $2.50
        var cost = pricing.CalculateCost(1_000_000, 0);

        // Assert
        cost.Should().Be(2.5m);
    }

    [Fact]
    public void CalculateCost_WithOnlyOutputTokens_CalculatesCorrectly()
    {
        // Arrange
        var pricing = new ModelPricing(
            inputPricePerMillion: 2.5m,
            outputPricePerMillion: 10m,
            currency: "USD");

        // Act - 1 million output tokens at $10/million = $10
        var cost = pricing.CalculateCost(0, 1_000_000);

        // Assert
        cost.Should().Be(10m);
    }

    [Fact]
    public void CalculateCost_WithBothTokenTypes_CalculatesSum()
    {
        // Arrange
        var pricing = new ModelPricing(
            inputPricePerMillion: 2.5m,
            outputPricePerMillion: 10m,
            currency: "USD");

        // Act - 1M input ($2.50) + 1M output ($10) = $12.50
        var cost = pricing.CalculateCost(1_000_000, 1_000_000);

        // Assert
        cost.Should().Be(12.5m);
    }

    [Fact]
    public void CalculateCost_WithSmallTokenCount_CalculatesFractionalCost()
    {
        // Arrange
        var pricing = new ModelPricing(
            inputPricePerMillion: 2.5m,
            outputPricePerMillion: 10m,
            currency: "USD");

        // Act - 1000 input tokens = $0.0025, 500 output tokens = $0.005
        var cost = pricing.CalculateCost(1000, 500);

        // Assert
        cost.Should().Be(0.0025m + 0.005m);
    }

    [Fact]
    public void CalculateCost_WithFreePricing_ReturnsZero()
    {
        // Arrange
        var pricing = ModelPricing.Free;

        // Act
        var cost = pricing.CalculateCost(1_000_000, 1_000_000);

        // Assert
        cost.Should().Be(0m);
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void TwoPricings_WithSameValues_AreEqual()
    {
        // Arrange
        var pricing1 = new ModelPricing(2.5m, 10m, "USD");
        var pricing2 = new ModelPricing(2.5m, 10m, "USD");

        // Assert
        pricing1.Should().Be(pricing2);
    }

    [Fact]
    public void TwoPricings_WithDifferentInputPrice_AreNotEqual()
    {
        // Arrange
        var pricing1 = new ModelPricing(2.5m, 10m, "USD");
        var pricing2 = new ModelPricing(3.0m, 10m, "USD");

        // Assert
        pricing1.Should().NotBe(pricing2);
    }

    [Fact]
    public void TwoPricings_WithDifferentCurrency_AreNotEqual()
    {
        // Arrange
        var pricing1 = new ModelPricing(2.5m, 10m, "USD");
        var pricing2 = new ModelPricing(2.5m, 10m, "EUR");

        // Assert
        pricing1.Should().NotBe(pricing2);
    }

    #endregion
}
