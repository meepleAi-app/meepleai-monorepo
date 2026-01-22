using Api.BoundedContexts.SystemConfiguration.Domain.Constants;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Constants;

/// <summary>
/// Unit tests for DefaultRateLimitConfigs constants.
/// </summary>
public class DefaultRateLimitConfigsTests
{
    #region Tier Defaults Tests

    [Fact]
    public void Free_HasCorrectDefaults()
    {
        // Assert
        DefaultRateLimitConfigs.Free.Tier.Should().Be(UserTier.Free);
        DefaultRateLimitConfigs.Free.MaxPendingRequests.Should().Be(2);
        DefaultRateLimitConfigs.Free.MaxRequestsPerMonth.Should().Be(5);
        DefaultRateLimitConfigs.Free.CooldownAfterRejection.Should().Be(TimeSpan.FromDays(7));
        DefaultRateLimitConfigs.Free.CooldownDays.Should().Be(7);
        DefaultRateLimitConfigs.Free.HasUnlimitedAccess.Should().BeFalse();
    }

    [Fact]
    public void Premium_HasCorrectDefaults()
    {
        // Assert
        DefaultRateLimitConfigs.Premium.Tier.Should().Be(UserTier.Premium);
        DefaultRateLimitConfigs.Premium.MaxPendingRequests.Should().Be(5);
        DefaultRateLimitConfigs.Premium.MaxRequestsPerMonth.Should().Be(15);
        DefaultRateLimitConfigs.Premium.CooldownAfterRejection.Should().Be(TimeSpan.FromDays(3));
        DefaultRateLimitConfigs.Premium.CooldownDays.Should().Be(3);
        DefaultRateLimitConfigs.Premium.HasUnlimitedAccess.Should().BeFalse();
    }

    [Fact]
    public void Pro_HasCorrectDefaults()
    {
        // Assert
        DefaultRateLimitConfigs.Pro.Tier.Should().Be(UserTier.Pro);
        DefaultRateLimitConfigs.Pro.MaxPendingRequests.Should().Be(10);
        DefaultRateLimitConfigs.Pro.MaxRequestsPerMonth.Should().Be(30);
        DefaultRateLimitConfigs.Pro.CooldownAfterRejection.Should().Be(TimeSpan.FromDays(1));
        DefaultRateLimitConfigs.Pro.CooldownDays.Should().Be(1);
        DefaultRateLimitConfigs.Pro.HasUnlimitedAccess.Should().BeFalse();
    }

    [Fact]
    public void Admin_HasUnlimitedDefaults()
    {
        // Assert
        DefaultRateLimitConfigs.Admin.Tier.Should().Be(UserTier.Admin);
        DefaultRateLimitConfigs.Admin.MaxPendingRequests.Should().Be(int.MaxValue);
        DefaultRateLimitConfigs.Admin.MaxRequestsPerMonth.Should().Be(int.MaxValue);
        DefaultRateLimitConfigs.Admin.CooldownAfterRejection.Should().Be(TimeSpan.Zero);
        DefaultRateLimitConfigs.Admin.CooldownDays.Should().Be(0);
        DefaultRateLimitConfigs.Admin.HasUnlimitedAccess.Should().BeTrue();
    }

    #endregion

    #region GetAll Tests

    [Fact]
    public void GetAll_ReturnsAllTiers()
    {
        // Act
        var all = DefaultRateLimitConfigs.GetAll();

        // Assert
        all.Should().HaveCount(4);
        all.Should().Contain(d => d.Tier == UserTier.Free);
        all.Should().Contain(d => d.Tier == UserTier.Premium);
        all.Should().Contain(d => d.Tier == UserTier.Pro);
        all.Should().Contain(d => d.Tier == UserTier.Admin);
    }

    [Fact]
    public void GetAll_ReturnsCorrectInstances()
    {
        // Act
        var all = DefaultRateLimitConfigs.GetAll();

        // Assert
        all.Should().Contain(DefaultRateLimitConfigs.Free);
        all.Should().Contain(DefaultRateLimitConfigs.Premium);
        all.Should().Contain(DefaultRateLimitConfigs.Pro);
        all.Should().Contain(DefaultRateLimitConfigs.Admin);
    }

    #endregion

    #region GetByTier Tests

    [Theory]
    [InlineData(UserTier.Free)]
    [InlineData(UserTier.Premium)]
    [InlineData(UserTier.Pro)]
    [InlineData(UserTier.Admin)]
    public void GetByTier_ReturnsCorrectDefaults(UserTier tier)
    {
        // Act
        var defaults = DefaultRateLimitConfigs.GetByTier(tier);

        // Assert
        defaults.Tier.Should().Be(tier);
    }

    [Fact]
    public void GetByTier_ForFree_ReturnsFreeDefaults()
    {
        // Act
        var defaults = DefaultRateLimitConfigs.GetByTier(UserTier.Free);

        // Assert
        defaults.Should().Be(DefaultRateLimitConfigs.Free);
    }

    [Fact]
    public void GetByTier_ForPremium_ReturnsPremiumDefaults()
    {
        // Act
        var defaults = DefaultRateLimitConfigs.GetByTier(UserTier.Premium);

        // Assert
        defaults.Should().Be(DefaultRateLimitConfigs.Premium);
    }

    [Fact]
    public void GetByTier_ForPro_ReturnsProDefaults()
    {
        // Act
        var defaults = DefaultRateLimitConfigs.GetByTier(UserTier.Pro);

        // Assert
        defaults.Should().Be(DefaultRateLimitConfigs.Pro);
    }

    [Fact]
    public void GetByTier_ForAdmin_ReturnsAdminDefaults()
    {
        // Act
        var defaults = DefaultRateLimitConfigs.GetByTier(UserTier.Admin);

        // Assert
        defaults.Should().Be(DefaultRateLimitConfigs.Admin);
    }

    [Fact]
    public void GetByTier_WithInvalidTier_ThrowsArgumentOutOfRangeException()
    {
        // Arrange
        var invalidTier = (UserTier)999;

        // Act
        var act = () => DefaultRateLimitConfigs.GetByTier(invalidTier);

        // Assert
        act.Should().Throw<ArgumentOutOfRangeException>()
            .WithParameterName("tier")
            .WithMessage("*Unknown user tier*");
    }

    #endregion

    #region HasUnlimitedAccess Tests

    [Fact]
    public void HasUnlimitedAccess_ForAdmin_ReturnsTrue()
    {
        // Assert
        DefaultRateLimitConfigs.HasUnlimitedAccess(UserTier.Admin).Should().BeTrue();
    }

    [Theory]
    [InlineData(UserTier.Free)]
    [InlineData(UserTier.Premium)]
    [InlineData(UserTier.Pro)]
    public void HasUnlimitedAccess_ForNonAdmin_ReturnsFalse(UserTier tier)
    {
        // Assert
        DefaultRateLimitConfigs.HasUnlimitedAccess(tier).Should().BeFalse();
    }

    #endregion

    #region GetDescription Tests

    [Fact]
    public void GetDescription_ForFreeTier_ReturnsCorrectDescription()
    {
        // Assert
        DefaultRateLimitConfigs.Free.GetDescription()
            .Should().Be("Max 2 pending, 5/month, 7 day cooldown");
    }

    [Fact]
    public void GetDescription_ForPremiumTier_ReturnsCorrectDescription()
    {
        // Assert
        DefaultRateLimitConfigs.Premium.GetDescription()
            .Should().Be("Max 5 pending, 15/month, 3 day cooldown");
    }

    [Fact]
    public void GetDescription_ForProTier_ReturnsCorrectDescription()
    {
        // Assert
        DefaultRateLimitConfigs.Pro.GetDescription()
            .Should().Be("Max 10 pending, 30/month, 1 day cooldown");
    }

    [Fact]
    public void GetDescription_ForAdminTier_ReturnsUnlimitedDescription()
    {
        // Assert
        DefaultRateLimitConfigs.Admin.GetDescription()
            .Should().Be("Unlimited access - no rate limits apply");
    }

    #endregion

    #region Tier Ordering Tests

    [Fact]
    public void TierDefaults_HaveIncreasingLimits()
    {
        // Arrange
        var free = DefaultRateLimitConfigs.Free;
        var premium = DefaultRateLimitConfigs.Premium;
        var pro = DefaultRateLimitConfigs.Pro;

        // Assert - limits should increase with higher tiers
        free.MaxPendingRequests.Should().BeLessThan(premium.MaxPendingRequests);
        premium.MaxPendingRequests.Should().BeLessThan(pro.MaxPendingRequests);

        free.MaxRequestsPerMonth.Should().BeLessThan(premium.MaxRequestsPerMonth);
        premium.MaxRequestsPerMonth.Should().BeLessThan(pro.MaxRequestsPerMonth);
    }

    [Fact]
    public void TierDefaults_HaveDecreasingCooldowns()
    {
        // Arrange
        var free = DefaultRateLimitConfigs.Free;
        var premium = DefaultRateLimitConfigs.Premium;
        var pro = DefaultRateLimitConfigs.Pro;

        // Assert - cooldowns should decrease with higher tiers
        free.CooldownAfterRejection.Should().BeGreaterThan(premium.CooldownAfterRejection);
        premium.CooldownAfterRejection.Should().BeGreaterThan(pro.CooldownAfterRejection);
    }

    #endregion
}

/// <summary>
/// Unit tests for RateLimitTierDefaults record.
/// </summary>
public class RateLimitTierDefaultsTests
{
    [Fact]
    public void CooldownDays_CalculatesCorrectly()
    {
        // Arrange
        var defaults = new RateLimitTierDefaults(
            UserTier.Free,
            MaxPendingRequests: 2,
            MaxRequestsPerMonth: 5,
            CooldownAfterRejection: TimeSpan.FromDays(7.5));

        // Assert - should truncate to whole days
        defaults.CooldownDays.Should().Be(7);
    }

    [Fact]
    public void HasUnlimitedAccess_WhenAdmin_ReturnsTrue()
    {
        // Arrange
        var defaults = new RateLimitTierDefaults(
            UserTier.Admin,
            MaxPendingRequests: int.MaxValue,
            MaxRequestsPerMonth: int.MaxValue,
            CooldownAfterRejection: TimeSpan.Zero);

        // Assert
        defaults.HasUnlimitedAccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(UserTier.Free)]
    [InlineData(UserTier.Premium)]
    [InlineData(UserTier.Pro)]
    public void HasUnlimitedAccess_WhenNotAdmin_ReturnsFalse(UserTier tier)
    {
        // Arrange
        var defaults = new RateLimitTierDefaults(
            tier,
            MaxPendingRequests: 10,
            MaxRequestsPerMonth: 50,
            CooldownAfterRejection: TimeSpan.FromDays(1));

        // Assert
        defaults.HasUnlimitedAccess.Should().BeFalse();
    }

    [Fact]
    public void Equality_WithSameValues_AreEqual()
    {
        // Arrange
        var defaults1 = new RateLimitTierDefaults(
            UserTier.Free,
            MaxPendingRequests: 2,
            MaxRequestsPerMonth: 5,
            CooldownAfterRejection: TimeSpan.FromDays(7));

        var defaults2 = new RateLimitTierDefaults(
            UserTier.Free,
            MaxPendingRequests: 2,
            MaxRequestsPerMonth: 5,
            CooldownAfterRejection: TimeSpan.FromDays(7));

        // Assert
        defaults1.Should().Be(defaults2);
    }

    [Fact]
    public void Equality_WithDifferentValues_AreNotEqual()
    {
        // Arrange
        var defaults1 = new RateLimitTierDefaults(
            UserTier.Free,
            MaxPendingRequests: 2,
            MaxRequestsPerMonth: 5,
            CooldownAfterRejection: TimeSpan.FromDays(7));

        var defaults2 = new RateLimitTierDefaults(
            UserTier.Premium,
            MaxPendingRequests: 5,
            MaxRequestsPerMonth: 15,
            CooldownAfterRejection: TimeSpan.FromDays(3));

        // Assert
        defaults1.Should().NotBe(defaults2);
    }
}
