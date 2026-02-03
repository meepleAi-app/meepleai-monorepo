using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Tests for the ModelSettings value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 9
/// </summary>
[Trait("Category", "Unit")]
public sealed class ModelSettingsTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidData_CreatesInstance()
    {
        // Arrange
        var pricing = new ModelPricing(2.5m, 10m, "USD");

        // Act
        var settings = new ModelSettings(
            maxTokens: 4096,
            temperature: 0.7m,
            pricing: pricing);

        // Assert
        settings.MaxTokens.Should().Be(4096);
        settings.Temperature.Should().Be(0.7m);
        settings.Pricing.Should().Be(pricing);
    }

    [Fact]
    public void Constructor_WithMinimumMaxTokens_Succeeds()
    {
        // Arrange
        var pricing = ModelPricing.Free;

        // Act
        var settings = new ModelSettings(512, 0.5m, pricing);

        // Assert
        settings.MaxTokens.Should().Be(512);
    }

    [Fact]
    public void Constructor_WithMaximumMaxTokens_Succeeds()
    {
        // Arrange
        var pricing = ModelPricing.Free;

        // Act
        var settings = new ModelSettings(8192, 0.5m, pricing);

        // Assert
        settings.MaxTokens.Should().Be(8192);
    }

    [Fact]
    public void Constructor_WithZeroTemperature_Succeeds()
    {
        // Arrange
        var pricing = ModelPricing.Free;

        // Act
        var settings = new ModelSettings(4096, 0m, pricing);

        // Assert
        settings.Temperature.Should().Be(0m);
    }

    [Fact]
    public void Constructor_WithMaxTemperature_Succeeds()
    {
        // Arrange
        var pricing = ModelPricing.Free;

        // Act
        var settings = new ModelSettings(4096, 2m, pricing);

        // Assert
        settings.Temperature.Should().Be(2m);
    }

    #endregion

    #region Validation Tests

    [Fact]
    public void Constructor_WithMaxTokensBelowMinimum_ThrowsArgumentException()
    {
        // Arrange
        var pricing = ModelPricing.Free;

        // Act
        var action = () => new ModelSettings(511, 0.7m, pricing);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*MaxTokens must be between 512 and 8192*");
    }

    [Fact]
    public void Constructor_WithMaxTokensAboveMaximum_ThrowsArgumentException()
    {
        // Arrange
        var pricing = ModelPricing.Free;

        // Act
        var action = () => new ModelSettings(8193, 0.7m, pricing);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*MaxTokens must be between 512 and 8192*");
    }

    [Fact]
    public void Constructor_WithNegativeTemperature_ThrowsArgumentException()
    {
        // Arrange
        var pricing = ModelPricing.Free;

        // Act
        var action = () => new ModelSettings(4096, -0.1m, pricing);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Temperature must be between 0 and 2*");
    }

    [Fact]
    public void Constructor_WithTemperatureAboveMax_ThrowsArgumentException()
    {
        // Arrange
        var pricing = ModelPricing.Free;

        // Act
        var action = () => new ModelSettings(4096, 2.1m, pricing);

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithMessage("*Temperature must be between 0 and 2*");
    }

    [Fact]
    public void Constructor_WithNullPricing_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new ModelSettings(4096, 0.7m, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("pricing");
    }

    #endregion

    #region Default Static Property Tests

    [Fact]
    public void Default_ReturnsExpectedValues()
    {
        // Act
        var settings = ModelSettings.Default;

        // Assert
        settings.MaxTokens.Should().Be(4096);
        settings.Temperature.Should().Be(0.7m);
        settings.Pricing.Should().Be(ModelPricing.Free);
    }

    #endregion

    #region Record Equality Tests

    [Fact]
    public void TwoSettings_WithSameValues_AreEqual()
    {
        // Arrange
        var pricing = new ModelPricing(2.5m, 10m, "USD");
        var settings1 = new ModelSettings(4096, 0.7m, pricing);
        var settings2 = new ModelSettings(4096, 0.7m, pricing);

        // Assert
        settings1.Should().Be(settings2);
    }

    [Fact]
    public void TwoSettings_WithDifferentMaxTokens_AreNotEqual()
    {
        // Arrange
        var pricing = ModelPricing.Free;
        var settings1 = new ModelSettings(4096, 0.7m, pricing);
        var settings2 = new ModelSettings(2048, 0.7m, pricing);

        // Assert
        settings1.Should().NotBe(settings2);
    }

    [Fact]
    public void TwoSettings_WithDifferentTemperature_AreNotEqual()
    {
        // Arrange
        var pricing = ModelPricing.Free;
        var settings1 = new ModelSettings(4096, 0.7m, pricing);
        var settings2 = new ModelSettings(4096, 0.5m, pricing);

        // Assert
        settings1.Should().NotBe(settings2);
    }

    [Fact]
    public void TwoSettings_WithDifferentPricing_AreNotEqual()
    {
        // Arrange
        var pricing1 = new ModelPricing(2.5m, 10m, "USD");
        var pricing2 = new ModelPricing(5.0m, 15m, "USD");
        var settings1 = new ModelSettings(4096, 0.7m, pricing1);
        var settings2 = new ModelSettings(4096, 0.7m, pricing2);

        // Assert
        settings1.Should().NotBe(settings2);
    }

    #endregion
}
