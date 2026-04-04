using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Tests for the TierDefinition entity.
/// D3: Game Night Flow - tier system definitions.
/// </summary>
[Trait("Category", "Unit")]
public sealed class TierDefinitionTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidValues_ReturnsInstance()
    {
        // Arrange
        var limits = TierLimits.FreeTier;

        // Act
        var tier = TierDefinition.Create("free", "Free Tier", limits, "free");

        // Assert
        tier.Id.Should().NotBeEmpty();
        tier.Name.Should().Be("free");
        tier.DisplayName.Should().Be("Free Tier");
        tier.Limits.Should().Be(limits);
        tier.LlmModelTier.Should().Be("free");
        tier.IsDefault.Should().BeFalse();
        tier.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        tier.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_NormalizesNameToLowercase()
    {
        var tier = TierDefinition.Create("PREMIUM", "Premium", TierLimits.PremiumTier, "standard");

        tier.Name.Should().Be("premium");
    }

    [Fact]
    public void Create_WithMixedCaseName_NormalizesToLowercase()
    {
        var tier = TierDefinition.Create("FrEe", "Free Tier", TierLimits.FreeTier, "free");

        tier.Name.Should().Be("free");
    }

    [Fact]
    public void Create_WithIsDefaultTrue_SetsIsDefault()
    {
        var tier = TierDefinition.Create("free", "Free Tier", TierLimits.FreeTier, "free", isDefault: true);

        tier.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void Create_WithEmptyName_ThrowsArgumentException()
    {
        var act = () => TierDefinition.Create("", "Free Tier", TierLimits.FreeTier, "free");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Fact]
    public void Create_WithWhitespaceName_ThrowsArgumentException()
    {
        var act = () => TierDefinition.Create("   ", "Free Tier", TierLimits.FreeTier, "free");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("name");
    }

    [Fact]
    public void Create_WithEmptyDisplayName_ThrowsArgumentException()
    {
        var act = () => TierDefinition.Create("free", "", TierLimits.FreeTier, "free");

        act.Should().Throw<ArgumentException>()
            .WithParameterName("displayName");
    }

    #endregion

    #region UpdateLimits Tests

    [Fact]
    public async Task UpdateLimits_ReplacesLimitsAndUpdatesTimestamp()
    {
        // Arrange
        var tier = TierDefinition.Create("free", "Free Tier", TierLimits.FreeTier, "free");
        var originalUpdatedAt = tier.UpdatedAt;

        // Tiny delay to ensure timestamp difference
        await Task.Delay(50);

        // Act
        tier.UpdateLimits(TierLimits.PremiumTier);

        // Assert
        tier.Limits.Should().Be(TierLimits.PremiumTier);
        tier.UpdatedAt.Should().BeOnOrAfter(originalUpdatedAt);
    }

    #endregion

    #region UpdateLlmModelTier Tests

    [Fact]
    public async Task UpdateLlmModelTier_ChangesValueAndUpdatesTimestamp()
    {
        // Arrange
        var tier = TierDefinition.Create("free", "Free Tier", TierLimits.FreeTier, "free");
        var originalUpdatedAt = tier.UpdatedAt;

        await Task.Delay(50);

        // Act
        tier.UpdateLlmModelTier("standard");

        // Assert
        tier.LlmModelTier.Should().Be("standard");
        tier.UpdatedAt.Should().BeOnOrAfter(originalUpdatedAt);
    }

    #endregion

    #region SetDefault Tests

    [Fact]
    public async Task SetDefault_ChangesIsDefaultAndUpdatesTimestamp()
    {
        // Arrange
        var tier = TierDefinition.Create("free", "Free Tier", TierLimits.FreeTier, "free");

        await Task.Delay(50);

        // Act
        tier.SetDefault(true);

        // Assert
        tier.IsDefault.Should().BeTrue();
    }

    [Fact]
    public void SetDefault_WithFalse_DisablesDefault()
    {
        var tier = TierDefinition.Create("free", "Free Tier", TierLimits.FreeTier, "free", isDefault: true);

        tier.SetDefault(false);

        tier.IsDefault.Should().BeFalse();
    }

    #endregion

    #region Unique Id Tests

    [Fact]
    public void Create_GeneratesUniqueIds()
    {
        var tier1 = TierDefinition.Create("free", "Free", TierLimits.FreeTier, "free");
        var tier2 = TierDefinition.Create("premium", "Premium", TierLimits.PremiumTier, "standard");

        tier1.Id.Should().NotBe(tier2.Id);
    }

    #endregion
}
