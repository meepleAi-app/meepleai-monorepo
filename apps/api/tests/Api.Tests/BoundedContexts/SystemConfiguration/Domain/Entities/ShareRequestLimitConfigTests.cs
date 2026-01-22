using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Unit tests for ShareRequestLimitConfig entity.
/// </summary>
public class ShareRequestLimitConfigTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsConfig()
    {
        // Arrange
        var tier = UserTier.Free;
        var maxPending = 2;
        var maxPerMonth = 5;
        var cooldown = TimeSpan.FromDays(7);

        // Act
        var config = ShareRequestLimitConfig.Create(tier, maxPending, maxPerMonth, cooldown);

        // Assert
        config.Should().NotBeNull();
        config.Id.Should().NotBe(Guid.Empty);
        config.Tier.Should().Be(tier);
        config.MaxPendingRequests.Should().Be(maxPending);
        config.MaxRequestsPerMonth.Should().Be(maxPerMonth);
        config.CooldownAfterRejection.Should().Be(cooldown);
        config.IsActive.Should().BeTrue();
        config.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        config.UpdatedAt.Should().Be(config.CreatedAt);
    }

    [Theory]
    [InlineData(UserTier.Free)]
    [InlineData(UserTier.Premium)]
    [InlineData(UserTier.Pro)]
    public void Create_WithNonAdminTier_RequiresPositivePendingLimit(UserTier tier)
    {
        // Act
        var act = () => ShareRequestLimitConfig.Create(tier, 0, 10, TimeSpan.FromDays(1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxPendingRequests")
            .WithMessage("*Maximum pending requests must be at least 1*");
    }

    [Theory]
    [InlineData(UserTier.Free)]
    [InlineData(UserTier.Premium)]
    [InlineData(UserTier.Pro)]
    public void Create_WithNonAdminTier_RequiresPositiveMonthlyLimit(UserTier tier)
    {
        // Act
        var act = () => ShareRequestLimitConfig.Create(tier, 5, 0, TimeSpan.FromDays(1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxRequestsPerMonth")
            .WithMessage("*Maximum requests per month must be at least 1*");
    }

    [Fact]
    public void Create_WithNegativeCooldown_ThrowsArgumentException()
    {
        // Act
        var act = () => ShareRequestLimitConfig.Create(UserTier.Free, 5, 10, TimeSpan.FromDays(-1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("cooldownAfterRejection")
            .WithMessage("*Cooldown cannot be negative*");
    }

    [Fact]
    public void Create_WithAdminTier_AllowsZeroValues()
    {
        // Act
        var config = ShareRequestLimitConfig.Create(UserTier.Admin, 0, 0, TimeSpan.Zero);

        // Assert
        config.Should().NotBeNull();
        config.Tier.Should().Be(UserTier.Admin);
        config.MaxPendingRequests.Should().Be(0);
        config.MaxRequestsPerMonth.Should().Be(0);
        config.CooldownAfterRejection.Should().Be(TimeSpan.Zero);
    }

    #endregion

    #region Update Tests

    [Fact]
    public void Update_WithValidParameters_UpdatesConfig()
    {
        // Arrange
        var config = CreateTestConfig();
        var newMaxPending = 10;
        var newMaxPerMonth = 20;
        var newCooldown = TimeSpan.FromDays(3);

        // Act
        config.Update(newMaxPending, newMaxPerMonth, newCooldown);

        // Assert
        config.MaxPendingRequests.Should().Be(newMaxPending);
        config.MaxRequestsPerMonth.Should().Be(newMaxPerMonth);
        config.CooldownAfterRejection.Should().Be(newCooldown);
        config.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Update_RaisesDomainEvent()
    {
        // Arrange
        var config = CreateTestConfig();
        config.ClearDomainEvents();

        // Act
        config.Update(10, 20, TimeSpan.FromDays(3));

        // Assert
        config.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void Update_WithInvalidPending_ThrowsArgumentException()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act
        var act = () => config.Update(0, 10, TimeSpan.FromDays(1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxPendingRequests");
    }

    [Fact]
    public void Update_WithInvalidMonthly_ThrowsArgumentException()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act
        var act = () => config.Update(5, 0, TimeSpan.FromDays(1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxRequestsPerMonth");
    }

    [Fact]
    public void Update_WithNegativeCooldown_ThrowsArgumentException()
    {
        // Arrange
        var config = CreateTestConfig();

        // Act
        var act = () => config.Update(5, 10, TimeSpan.FromDays(-1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("cooldownAfterRejection");
    }

    #endregion

    #region Activate/Deactivate Tests

    [Fact]
    public void Deactivate_WhenActive_DeactivatesConfig()
    {
        // Arrange
        var config = CreateTestConfig();
        config.IsActive.Should().BeTrue();

        // Act
        config.Deactivate();

        // Assert
        config.IsActive.Should().BeFalse();
        config.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Deactivate_WhenAlreadyInactive_DoesNotUpdateTimestamp()
    {
        // Arrange
        var config = CreateTestConfig();
        config.Deactivate();
        var firstUpdatedAt = config.UpdatedAt;
        Thread.Sleep(10);

        // Act
        config.Deactivate();

        // Assert
        config.IsActive.Should().BeFalse();
        config.UpdatedAt.Should().Be(firstUpdatedAt);
    }

    [Fact]
    public void Activate_WhenInactive_ActivatesConfig()
    {
        // Arrange
        var config = CreateTestConfig();
        config.Deactivate();

        // Act
        config.Activate();

        // Assert
        config.IsActive.Should().BeTrue();
        config.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Activate_WhenAlreadyActive_DoesNotUpdateTimestamp()
    {
        // Arrange
        var config = CreateTestConfig();
        var createdAt = config.CreatedAt;

        // Act
        config.Activate();

        // Assert
        config.IsActive.Should().BeTrue();
        config.UpdatedAt.Should().Be(createdAt);
    }

    #endregion

    #region HasUnlimitedAccess Tests

    [Fact]
    public void HasUnlimitedAccess_WhenAdminTier_ReturnsTrue()
    {
        // Arrange
        var config = ShareRequestLimitConfig.Create(
            UserTier.Admin, int.MaxValue, int.MaxValue, TimeSpan.Zero);

        // Assert
        config.HasUnlimitedAccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(UserTier.Free)]
    [InlineData(UserTier.Premium)]
    [InlineData(UserTier.Pro)]
    public void HasUnlimitedAccess_WhenNotAdminTier_ReturnsFalse(UserTier tier)
    {
        // Arrange
        var config = ShareRequestLimitConfig.Create(tier, 5, 10, TimeSpan.FromDays(1));

        // Assert
        config.HasUnlimitedAccess.Should().BeFalse();
    }

    #endregion

    #region Internal Constructor Tests

    [Fact]
    public void InternalConstructor_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var tier = UserTier.Premium;
        var maxPending = 5;
        var maxPerMonth = 15;
        var cooldown = TimeSpan.FromDays(3);
        var isActive = false;
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var updatedAt = DateTime.UtcNow;

        // Act
        var config = new ShareRequestLimitConfig(
            id, tier, maxPending, maxPerMonth, cooldown, isActive, createdAt, updatedAt);

        // Assert
        config.Id.Should().Be(id);
        config.Tier.Should().Be(tier);
        config.MaxPendingRequests.Should().Be(maxPending);
        config.MaxRequestsPerMonth.Should().Be(maxPerMonth);
        config.CooldownAfterRejection.Should().Be(cooldown);
        config.IsActive.Should().Be(isActive);
        config.CreatedAt.Should().Be(createdAt);
        config.UpdatedAt.Should().Be(updatedAt);
    }

    #endregion

    #region Helper Methods

    private static ShareRequestLimitConfig CreateTestConfig()
    {
        return ShareRequestLimitConfig.Create(
            UserTier.Free,
            2,
            5,
            TimeSpan.FromDays(7));
    }

    #endregion
}
