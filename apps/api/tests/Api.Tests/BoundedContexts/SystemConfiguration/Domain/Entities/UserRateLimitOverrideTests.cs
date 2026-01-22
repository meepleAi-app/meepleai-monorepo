using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SystemConfiguration.Domain.Entities;

/// <summary>
/// Unit tests for UserRateLimitOverride entity.
/// </summary>
public class UserRateLimitOverrideTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidParameters_ReturnsOverride()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var reason = "Special promotion";

        // Act
        var rateLimitOverride = UserRateLimitOverride.Create(userId, adminId, reason);

        // Assert
        rateLimitOverride.Should().NotBeNull();
        rateLimitOverride.Id.Should().NotBe(Guid.Empty);
        rateLimitOverride.UserId.Should().Be(userId);
        rateLimitOverride.CreatedByAdminId.Should().Be(adminId);
        rateLimitOverride.Reason.Should().Be(reason);
        rateLimitOverride.ExpiresAt.Should().BeNull();
        rateLimitOverride.MaxPendingRequests.Should().BeNull();
        rateLimitOverride.MaxRequestsPerMonth.Should().BeNull();
        rateLimitOverride.CooldownAfterRejection.Should().BeNull();
        rateLimitOverride.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void Create_WithExpiration_SetsExpiresAt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();
        var reason = "Temporary boost";
        var expiresAt = DateTime.UtcNow.AddDays(30);

        // Act
        var rateLimitOverride = UserRateLimitOverride.Create(userId, adminId, reason, expiresAt);

        // Assert
        rateLimitOverride.ExpiresAt.Should().Be(expiresAt);
    }

    [Fact]
    public void Create_RaisesDomainEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        // Act
        var rateLimitOverride = UserRateLimitOverride.Create(userId, adminId, "Test reason");

        // Assert
        rateLimitOverride.DomainEvents.Should().ContainSingle();
    }

    [Fact]
    public void Create_WithEmptyUserId_ThrowsArgumentException()
    {
        // Act
        var act = () => UserRateLimitOverride.Create(Guid.Empty, Guid.NewGuid(), "Test");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId")
            .WithMessage("*UserId cannot be empty*");
    }

    [Fact]
    public void Create_WithEmptyAdminId_ThrowsArgumentException()
    {
        // Act
        var act = () => UserRateLimitOverride.Create(Guid.NewGuid(), Guid.Empty, "Test");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("createdByAdminId")
            .WithMessage("*CreatedByAdminId cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyReason_ThrowsArgumentException(string? invalidReason)
    {
        // Act
        var act = () => UserRateLimitOverride.Create(Guid.NewGuid(), Guid.NewGuid(), invalidReason!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("reason")
            .WithMessage("*Reason is required*");
    }

    [Fact]
    public void Create_WithPastExpiration_ThrowsArgumentException()
    {
        // Act
        var act = () => UserRateLimitOverride.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test",
            DateTime.UtcNow.AddDays(-1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("expiresAt")
            .WithMessage("*Expiration date must be in the future*");
    }

    [Fact]
    public void Create_TrimsReason()
    {
        // Act
        var rateLimitOverride = UserRateLimitOverride.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "  Trimmed reason  ");

        // Assert
        rateLimitOverride.Reason.Should().Be("Trimmed reason");
    }

    #endregion

    #region UpdateLimits Tests

    [Fact]
    public void UpdateLimits_WithValidValues_UpdatesLimits()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();
        var maxPending = 10;
        var maxPerMonth = 50;
        var cooldown = TimeSpan.FromDays(1);

        // Act
        rateLimitOverride.UpdateLimits(maxPending, maxPerMonth, cooldown);

        // Assert
        rateLimitOverride.MaxPendingRequests.Should().Be(maxPending);
        rateLimitOverride.MaxRequestsPerMonth.Should().Be(maxPerMonth);
        rateLimitOverride.CooldownAfterRejection.Should().Be(cooldown);
        rateLimitOverride.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateLimits_WithNullValues_ClearsCustomLimits()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();
        rateLimitOverride.UpdateLimits(10, 50, TimeSpan.FromDays(1));

        // Act
        rateLimitOverride.UpdateLimits(null, null, null);

        // Assert
        rateLimitOverride.MaxPendingRequests.Should().BeNull();
        rateLimitOverride.MaxRequestsPerMonth.Should().BeNull();
        rateLimitOverride.CooldownAfterRejection.Should().BeNull();
    }

    [Fact]
    public void UpdateLimits_WithZeroValues_AllowsZero()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();

        // Act
        rateLimitOverride.UpdateLimits(0, 0, TimeSpan.Zero);

        // Assert
        rateLimitOverride.MaxPendingRequests.Should().Be(0);
        rateLimitOverride.MaxRequestsPerMonth.Should().Be(0);
        rateLimitOverride.CooldownAfterRejection.Should().Be(TimeSpan.Zero);
    }

    [Fact]
    public void UpdateLimits_WithNegativePending_ThrowsArgumentException()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();

        // Act
        var act = () => rateLimitOverride.UpdateLimits(-1, 10, TimeSpan.FromDays(1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxPendingRequests")
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public void UpdateLimits_WithNegativeMonthly_ThrowsArgumentException()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();

        // Act
        var act = () => rateLimitOverride.UpdateLimits(10, -1, TimeSpan.FromDays(1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("maxRequestsPerMonth")
            .WithMessage("*cannot be negative*");
    }

    [Fact]
    public void UpdateLimits_WithNegativeCooldown_ThrowsArgumentException()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();

        // Act
        var act = () => rateLimitOverride.UpdateLimits(10, 50, TimeSpan.FromDays(-1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("cooldownAfterRejection")
            .WithMessage("*cannot be negative*");
    }

    #endregion

    #region UpdateReason Tests

    [Fact]
    public void UpdateReason_WithValidReason_UpdatesReason()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();
        var newReason = "Updated reason";

        // Act
        rateLimitOverride.UpdateReason(newReason);

        // Assert
        rateLimitOverride.Reason.Should().Be(newReason);
        rateLimitOverride.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateReason_TrimsReason()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();

        // Act
        rateLimitOverride.UpdateReason("  New trimmed reason  ");

        // Assert
        rateLimitOverride.Reason.Should().Be("New trimmed reason");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateReason_WithEmptyReason_ThrowsArgumentException(string? invalidReason)
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();

        // Act
        var act = () => rateLimitOverride.UpdateReason(invalidReason!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("reason")
            .WithMessage("*Reason is required*");
    }

    #endregion

    #region UpdateExpiration Tests

    [Fact]
    public void UpdateExpiration_WithFutureDate_UpdatesExpiration()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();
        var newExpiration = DateTime.UtcNow.AddDays(60);

        // Act
        rateLimitOverride.UpdateExpiration(newExpiration);

        // Assert
        rateLimitOverride.ExpiresAt.Should().Be(newExpiration);
        rateLimitOverride.UpdatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdateExpiration_WithNull_MakesPermanent()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();
        rateLimitOverride.UpdateExpiration(DateTime.UtcNow.AddDays(30));

        // Act
        rateLimitOverride.UpdateExpiration(null);

        // Assert
        rateLimitOverride.ExpiresAt.Should().BeNull();
    }

    [Fact]
    public void UpdateExpiration_WithPastDate_ThrowsArgumentException()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();

        // Act
        var act = () => rateLimitOverride.UpdateExpiration(DateTime.UtcNow.AddDays(-1));

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("expiresAt")
            .WithMessage("*Expiration date must be in the future*");
    }

    #endregion

    #region IsExpired Tests

    [Fact]
    public void IsExpired_WhenNoExpiration_ReturnsFalse()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();

        // Assert
        rateLimitOverride.IsExpired().Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenExpirationInFuture_ReturnsFalse()
    {
        // Arrange
        var rateLimitOverride = UserRateLimitOverride.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test",
            DateTime.UtcNow.AddDays(30));

        // Assert
        rateLimitOverride.IsExpired().Should().BeFalse();
    }

    [Fact]
    public void IsExpired_WhenExpirationInPast_ReturnsTrue()
    {
        // Arrange - use internal constructor to set past expiration
        var rateLimitOverride = new UserRateLimitOverride(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null, null, null,
            DateTime.UtcNow.AddDays(-1),
            "Test",
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow.AddDays(-1));

        // Assert
        rateLimitOverride.IsExpired().Should().BeTrue();
    }

    #endregion

    #region IsActive Tests

    [Fact]
    public void IsActive_WhenNotExpired_ReturnsTrue()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();

        // Assert
        rateLimitOverride.IsActive().Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenExpired_ReturnsFalse()
    {
        // Arrange
        var rateLimitOverride = new UserRateLimitOverride(
            Guid.NewGuid(),
            Guid.NewGuid(),
            null, null, null,
            DateTime.UtcNow.AddDays(-1),
            "Test",
            Guid.NewGuid(),
            DateTime.UtcNow.AddDays(-30),
            DateTime.UtcNow.AddDays(-1));

        // Assert
        rateLimitOverride.IsActive().Should().BeFalse();
    }

    #endregion

    #region HasCustomLimits Tests

    [Fact]
    public void HasCustomLimits_WhenNoLimitsSet_ReturnsFalse()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();

        // Assert
        rateLimitOverride.HasCustomLimits().Should().BeFalse();
    }

    [Fact]
    public void HasCustomLimits_WhenPendingSet_ReturnsTrue()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();
        rateLimitOverride.UpdateLimits(10, null, null);

        // Assert
        rateLimitOverride.HasCustomLimits().Should().BeTrue();
    }

    [Fact]
    public void HasCustomLimits_WhenMonthlySet_ReturnsTrue()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();
        rateLimitOverride.UpdateLimits(null, 50, null);

        // Assert
        rateLimitOverride.HasCustomLimits().Should().BeTrue();
    }

    [Fact]
    public void HasCustomLimits_WhenCooldownSet_ReturnsTrue()
    {
        // Arrange
        var rateLimitOverride = CreateTestOverride();
        rateLimitOverride.UpdateLimits(null, null, TimeSpan.FromDays(1));

        // Assert
        rateLimitOverride.HasCustomLimits().Should().BeTrue();
    }

    #endregion

    #region Internal Constructor Tests

    [Fact]
    public void InternalConstructor_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var maxPending = 10;
        var maxPerMonth = 50;
        var cooldown = TimeSpan.FromDays(2);
        var expiresAt = DateTime.UtcNow.AddDays(30);
        var reason = "Test reason";
        var adminId = Guid.NewGuid();
        var createdAt = DateTime.UtcNow.AddDays(-1);
        var updatedAt = DateTime.UtcNow;

        // Act
        var rateLimitOverride = new UserRateLimitOverride(
            id, userId, maxPending, maxPerMonth, cooldown, expiresAt,
            reason, adminId, createdAt, updatedAt);

        // Assert
        rateLimitOverride.Id.Should().Be(id);
        rateLimitOverride.UserId.Should().Be(userId);
        rateLimitOverride.MaxPendingRequests.Should().Be(maxPending);
        rateLimitOverride.MaxRequestsPerMonth.Should().Be(maxPerMonth);
        rateLimitOverride.CooldownAfterRejection.Should().Be(cooldown);
        rateLimitOverride.ExpiresAt.Should().Be(expiresAt);
        rateLimitOverride.Reason.Should().Be(reason);
        rateLimitOverride.CreatedByAdminId.Should().Be(adminId);
        rateLimitOverride.CreatedAt.Should().Be(createdAt);
        rateLimitOverride.UpdatedAt.Should().Be(updatedAt);
    }

    #endregion

    #region Helper Methods

    private static UserRateLimitOverride CreateTestOverride()
    {
        return UserRateLimitOverride.Create(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "Test override reason");
    }

    #endregion
}
