using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.ValueObjects;

/// <summary>
/// Unit tests for RateLimitStatus value object.
/// </summary>
public class RateLimitStatusTests
{
    #region CreateUnlimited Tests

    [Fact]
    public void CreateUnlimited_ReturnsAdminStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var status = RateLimitStatus.CreateUnlimited(userId);

        // Assert
        status.UserId.Should().Be(userId);
        status.Tier.Should().Be(UserTier.Admin);
        status.HasOverride.Should().BeFalse();
        status.EffectiveMaxPending.Should().Be(int.MaxValue);
        status.EffectiveMaxPerMonth.Should().Be(int.MaxValue);
        status.EffectiveCooldown.Should().Be(TimeSpan.Zero);
        status.CanCreateRequest.Should().BeTrue();
        status.BlockReason.Should().BeNull();
        status.HasUnlimitedAccess.Should().BeTrue();
    }

    #endregion

    #region Create Tests

    [Fact]
    public void Create_WithAdminTier_ReturnsUnlimitedStatus()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var status = RateLimitStatus.Create(
            userId,
            UserTier.Admin,
            hasOverride: false,
            currentPendingCount: 100,
            currentMonthlyCount: 1000,
            lastRejectionAt: DateTime.UtcNow,
            effectiveMaxPending: 5,
            effectiveMaxPerMonth: 10,
            effectiveCooldown: TimeSpan.FromDays(7));

        // Assert
        status.Tier.Should().Be(UserTier.Admin);
        status.CanCreateRequest.Should().BeTrue();
        status.HasUnlimitedAccess.Should().BeTrue();
    }

    [Fact]
    public void Create_WithNoLimitsReached_CanCreateRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var status = RateLimitStatus.Create(
            userId,
            UserTier.Free,
            hasOverride: false,
            currentPendingCount: 1,
            currentMonthlyCount: 3,
            lastRejectionAt: null,
            effectiveMaxPending: 2,
            effectiveMaxPerMonth: 5,
            effectiveCooldown: TimeSpan.FromDays(7));

        // Assert
        status.CanCreateRequest.Should().BeTrue();
        status.BlockReason.Should().BeNull();
    }

    [Fact]
    public void Create_WhenMaxPendingReached_CannotCreateRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var status = RateLimitStatus.Create(
            userId,
            UserTier.Free,
            hasOverride: false,
            currentPendingCount: 2,
            currentMonthlyCount: 3,
            lastRejectionAt: null,
            effectiveMaxPending: 2,
            effectiveMaxPerMonth: 5,
            effectiveCooldown: TimeSpan.FromDays(7));

        // Assert
        status.CanCreateRequest.Should().BeFalse();
        status.BlockReason.Should().Be(BlockReasons.MaxPendingReached);
    }

    [Fact]
    public void Create_WhenMaxMonthlyReached_CannotCreateRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var status = RateLimitStatus.Create(
            userId,
            UserTier.Free,
            hasOverride: false,
            currentPendingCount: 1,
            currentMonthlyCount: 5,
            lastRejectionAt: null,
            effectiveMaxPending: 2,
            effectiveMaxPerMonth: 5,
            effectiveCooldown: TimeSpan.FromDays(7));

        // Assert
        status.CanCreateRequest.Should().BeFalse();
        status.BlockReason.Should().Be(BlockReasons.MaxMonthlyReached);
    }

    [Fact]
    public void Create_WhenInCooldown_CannotCreateRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lastRejection = DateTime.UtcNow.AddDays(-1); // 1 day ago

        // Act
        var status = RateLimitStatus.Create(
            userId,
            UserTier.Free,
            hasOverride: false,
            currentPendingCount: 0,
            currentMonthlyCount: 3,
            lastRejectionAt: lastRejection,
            effectiveMaxPending: 2,
            effectiveMaxPerMonth: 5,
            effectiveCooldown: TimeSpan.FromDays(7)); // 7 day cooldown

        // Assert
        status.CanCreateRequest.Should().BeFalse();
        status.BlockReason.Should().Be(BlockReasons.InCooldown);
        status.CooldownEndsAt.Should().BeCloseTo(lastRejection.AddDays(7), TimeSpan.FromSeconds(1));
    }

    [Fact]
    public void Create_WhenCooldownExpired_CanCreateRequest()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var lastRejection = DateTime.UtcNow.AddDays(-10); // 10 days ago

        // Act
        var status = RateLimitStatus.Create(
            userId,
            UserTier.Free,
            hasOverride: false,
            currentPendingCount: 0,
            currentMonthlyCount: 3,
            lastRejectionAt: lastRejection,
            effectiveMaxPending: 2,
            effectiveMaxPerMonth: 5,
            effectiveCooldown: TimeSpan.FromDays(7)); // 7 day cooldown

        // Assert
        status.CanCreateRequest.Should().BeTrue();
        status.BlockReason.Should().BeNull();
    }

    [Fact]
    public void Create_SetsMonthResetAtCorrectly()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var now = DateTime.UtcNow;
        var expectedNextMonth = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);

        // Act
        var status = RateLimitStatus.Create(
            userId,
            UserTier.Free,
            hasOverride: false,
            currentPendingCount: 0,
            currentMonthlyCount: 0,
            lastRejectionAt: null,
            effectiveMaxPending: 2,
            effectiveMaxPerMonth: 5,
            effectiveCooldown: TimeSpan.FromDays(7));

        // Assert
        status.MonthResetAt.Should().Be(expectedNextMonth);
    }

    #endregion

    #region Usage Percentage Tests

    [Fact]
    public void PendingUsagePercent_CalculatesCorrectly()
    {
        // Arrange
        var status = RateLimitStatus.Create(
            Guid.NewGuid(),
            UserTier.Free,
            hasOverride: false,
            currentPendingCount: 1,
            currentMonthlyCount: 0,
            lastRejectionAt: null,
            effectiveMaxPending: 2,
            effectiveMaxPerMonth: 5,
            effectiveCooldown: TimeSpan.FromDays(7));

        // Assert
        status.PendingUsagePercent.Should().Be(50.00m);
    }

    [Fact]
    public void PendingUsagePercent_WhenZeroMax_ReturnsZero()
    {
        // Arrange - use record init to create with zero max
        var status = new RateLimitStatus
        {
            UserId = Guid.NewGuid(),
            Tier = UserTier.Free,
            EffectiveMaxPending = 0,
            CurrentPendingCount = 5
        };

        // Assert
        status.PendingUsagePercent.Should().Be(0);
    }

    [Fact]
    public void MonthlyUsagePercent_CalculatesCorrectly()
    {
        // Arrange
        var status = RateLimitStatus.Create(
            Guid.NewGuid(),
            UserTier.Free,
            hasOverride: false,
            currentPendingCount: 0,
            currentMonthlyCount: 3,
            lastRejectionAt: null,
            effectiveMaxPending: 2,
            effectiveMaxPerMonth: 5,
            effectiveCooldown: TimeSpan.FromDays(7));

        // Assert
        status.MonthlyUsagePercent.Should().Be(60.00m);
    }

    [Fact]
    public void MonthlyUsagePercent_WhenZeroMax_ReturnsZero()
    {
        // Arrange
        var status = new RateLimitStatus
        {
            UserId = Guid.NewGuid(),
            Tier = UserTier.Free,
            EffectiveMaxPerMonth = 0,
            CurrentMonthlyCount = 5
        };

        // Assert
        status.MonthlyUsagePercent.Should().Be(0);
    }

    #endregion

    #region Remaining Requests Tests

    [Fact]
    public void RemainingPendingRequests_CalculatesCorrectly()
    {
        // Arrange
        var status = RateLimitStatus.Create(
            Guid.NewGuid(),
            UserTier.Free,
            hasOverride: false,
            currentPendingCount: 1,
            currentMonthlyCount: 0,
            lastRejectionAt: null,
            effectiveMaxPending: 5,
            effectiveMaxPerMonth: 10,
            effectiveCooldown: TimeSpan.FromDays(7));

        // Assert
        status.RemainingPendingRequests.Should().Be(4);
    }

    [Fact]
    public void RemainingPendingRequests_WhenOverMax_ReturnsZero()
    {
        // Arrange
        var status = new RateLimitStatus
        {
            UserId = Guid.NewGuid(),
            Tier = UserTier.Free,
            CurrentPendingCount = 10,
            EffectiveMaxPending = 5
        };

        // Assert
        status.RemainingPendingRequests.Should().Be(0);
    }

    [Fact]
    public void RemainingMonthlyRequests_CalculatesCorrectly()
    {
        // Arrange
        var status = RateLimitStatus.Create(
            Guid.NewGuid(),
            UserTier.Free,
            hasOverride: false,
            currentPendingCount: 0,
            currentMonthlyCount: 3,
            lastRejectionAt: null,
            effectiveMaxPending: 5,
            effectiveMaxPerMonth: 10,
            effectiveCooldown: TimeSpan.FromDays(7));

        // Assert
        status.RemainingMonthlyRequests.Should().Be(7);
    }

    #endregion

    #region IsInCooldown Tests

    [Fact]
    public void IsInCooldown_WhenNoCooldownEndsAt_ReturnsFalse()
    {
        // Arrange
        var status = RateLimitStatus.Create(
            Guid.NewGuid(),
            UserTier.Free,
            hasOverride: false,
            currentPendingCount: 0,
            currentMonthlyCount: 0,
            lastRejectionAt: null,
            effectiveMaxPending: 2,
            effectiveMaxPerMonth: 5,
            effectiveCooldown: TimeSpan.FromDays(7));

        // Assert
        status.IsInCooldown.Should().BeFalse();
    }

    [Fact]
    public void IsInCooldown_WhenCooldownEndsInFuture_ReturnsTrue()
    {
        // Arrange
        var status = new RateLimitStatus
        {
            UserId = Guid.NewGuid(),
            Tier = UserTier.Free,
            CooldownEndsAt = DateTime.UtcNow.AddDays(1)
        };

        // Assert
        status.IsInCooldown.Should().BeTrue();
    }

    [Fact]
    public void IsInCooldown_WhenCooldownEndsInPast_ReturnsFalse()
    {
        // Arrange
        var status = new RateLimitStatus
        {
            UserId = Guid.NewGuid(),
            Tier = UserTier.Free,
            CooldownEndsAt = DateTime.UtcNow.AddDays(-1)
        };

        // Assert
        status.IsInCooldown.Should().BeFalse();
    }

    #endregion

    #region HasUnlimitedAccess Tests

    [Fact]
    public void HasUnlimitedAccess_WhenAdmin_ReturnsTrue()
    {
        // Arrange
        var status = new RateLimitStatus
        {
            UserId = Guid.NewGuid(),
            Tier = UserTier.Admin
        };

        // Assert
        status.HasUnlimitedAccess.Should().BeTrue();
    }

    [Theory]
    [InlineData(UserTier.Free)]
    [InlineData(UserTier.Premium)]
    [InlineData(UserTier.Pro)]
    public void HasUnlimitedAccess_WhenNotAdmin_ReturnsFalse(UserTier tier)
    {
        // Arrange
        var status = new RateLimitStatus
        {
            UserId = Guid.NewGuid(),
            Tier = tier
        };

        // Assert
        status.HasUnlimitedAccess.Should().BeFalse();
    }

    #endregion

    #region HasOverride Tests

    [Fact]
    public void HasOverride_WhenSet_ReturnsTrue()
    {
        // Arrange
        var status = RateLimitStatus.Create(
            Guid.NewGuid(),
            UserTier.Free,
            hasOverride: true,
            currentPendingCount: 0,
            currentMonthlyCount: 0,
            lastRejectionAt: null,
            effectiveMaxPending: 10,
            effectiveMaxPerMonth: 50,
            effectiveCooldown: TimeSpan.FromDays(1));

        // Assert
        status.HasOverride.Should().BeTrue();
    }

    #endregion
}

/// <summary>
/// Unit tests for BlockReasons constants.
/// </summary>
public class BlockReasonsTests
{
    [Fact]
    public void BlockReasons_ContainsExpectedValues()
    {
        BlockReasons.MaxPendingReached.Should().Be("Maximum pending requests reached");
        BlockReasons.MaxMonthlyReached.Should().Be("Monthly request limit reached");
        BlockReasons.InCooldown.Should().Be("Cooldown period after rejection active");
    }
}
