using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Enums;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Administration.Domain;

/// <summary>
/// Unit tests for TokenTier aggregate (Issue #3786)
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Administration")]
public sealed class TokenTierTests
{
    [Fact]
    public void Create_WithValidData_ShouldCreateTokenTier()
    {
        // Arrange
        var limits = TierLimits.FreeTier();
        var pricing = TierPricing.FreeTier();

        // Act
        var tier = TokenTier.Create(TierName.Free, limits, pricing);

        // Assert
        tier.Should().NotBeNull();
        tier.Id.Should().NotBe(Guid.Empty);
        tier.Name.Should().Be(TierName.Free);
        tier.Limits.Should().Be(limits);
        tier.Pricing.Should().Be(pricing);
        tier.IsActive.Should().BeTrue();
        tier.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WithNullLimits_ShouldThrowArgumentNullException()
    {
        // Arrange
        var pricing = TierPricing.FreeTier();

        // Act
        var act = () => TokenTier.Create(TierName.Free, null!, pricing);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("limits");
    }

    [Fact]
    public void Create_WithNullPricing_ShouldThrowArgumentNullException()
    {
        // Arrange
        var limits = TierLimits.FreeTier();

        // Act
        var act = () => TokenTier.Create(TierName.Free, limits, null!);

        // Assert
        act.Should().Throw<ArgumentNullException>().WithParameterName("pricing");
    }

    [Fact]
    public void UpdateLimits_WithValidData_ShouldUpdateLimitsAndTimestamp()
    {
        // Arrange
        var tier = TokenTier.CreateFreeTier();
        var originalUpdatedAt = tier.UpdatedAt;
        var newLimits = TierLimits.BasicTier();

        // Act
        Thread.Sleep(10); // Ensure timestamp difference
        tier.UpdateLimits(newLimits);

        // Assert
        tier.Limits.Should().Be(newLimits);
        tier.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void UpdatePricing_WithValidData_ShouldUpdatePricingAndTimestamp()
    {
        // Arrange
        var tier = TokenTier.CreateFreeTier();
        var originalUpdatedAt = tier.UpdatedAt;
        var newPricing = TierPricing.BasicTier();

        // Act
        Thread.Sleep(10); // Ensure timestamp difference
        tier.UpdatePricing(newPricing);

        // Assert
        tier.Pricing.Should().Be(newPricing);
        tier.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Fact]
    public void Deactivate_ShouldSetIsActiveToFalseAndUpdateTimestamp()
    {
        // Arrange
        var tier = TokenTier.CreateFreeTier();
        var originalUpdatedAt = tier.UpdatedAt;

        // Act
        Thread.Sleep(10);
        tier.Deactivate();

        // Assert
        tier.IsActive.Should().BeFalse();
        tier.UpdatedAt.Should().BeAfter(originalUpdatedAt);
    }

    [Theory]
    [InlineData(TierName.Free)]
    [InlineData(TierName.Basic)]
    [InlineData(TierName.Pro)]
    [InlineData(TierName.Enterprise)]
    public void CreatePredefinedTiers_ShouldHaveCorrectDefaults(TierName tierName)
    {
        // Act
        var tier = tierName switch
        {
            TierName.Free => TokenTier.CreateFreeTier(),
            TierName.Basic => TokenTier.CreateBasicTier(),
            TierName.Pro => TokenTier.CreateProTier(),
            TierName.Enterprise => TokenTier.CreateEnterpriseTier(),
            _ => throw new ArgumentException($"Unexpected tier: {tierName}")
        };

        // Assert
        tier.Should().NotBeNull();
        tier.Name.Should().Be(tierName);
        tier.IsActive.Should().BeTrue();
        tier.Limits.TokensPerMonth.Should().BeGreaterThan(0);
    }
}
