using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.Entities;

/// <summary>
/// Unit tests for UserBadge entity.
/// </summary>
public class UserBadgeTests
{
    #region Award Tests

    [Fact]
    public void Award_WithValidParameters_ReturnsUserBadge()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var badgeId = Guid.NewGuid();
        var badgeCode = "FIRST_CONTRIBUTION";

        // Act
        var userBadge = UserBadge.Award(userId, badgeId, badgeCode);

        // Assert
        userBadge.Should().NotBeNull();
        userBadge.Id.Should().NotBe(Guid.Empty);
        userBadge.UserId.Should().Be(userId);
        userBadge.BadgeId.Should().Be(badgeId);
        userBadge.EarnedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        userBadge.TriggeringShareRequestId.Should().BeNull();
        userBadge.IsDisplayed.Should().BeTrue();
        userBadge.IsActive.Should().BeTrue();
        userBadge.RevokedAt.Should().BeNull();
        userBadge.RevocationReason.Should().BeNull();
    }

    [Fact]
    public void Award_WithTriggeringShareRequest_IncludesIt()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var badgeId = Guid.NewGuid();
        var badgeCode = "FIRST_CONTRIBUTION";
        var shareRequestId = Guid.NewGuid();

        // Act
        var userBadge = UserBadge.Award(userId, badgeId, badgeCode, shareRequestId);

        // Assert
        userBadge.TriggeringShareRequestId.Should().Be(shareRequestId);
    }

    [Fact]
    public void Award_RaisesBadgeEarnedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var badgeId = Guid.NewGuid();
        var badgeCode = "FIRST_CONTRIBUTION";

        // Act
        var userBadge = UserBadge.Award(userId, badgeId, badgeCode);

        // Assert
        var domainEvents = userBadge.DomainEvents;
        domainEvents.Should().ContainSingle();

        var badgeEarnedEvent = domainEvents.First() as BadgeEarnedEvent;
        badgeEarnedEvent.Should().NotBeNull();
        badgeEarnedEvent!.UserBadgeId.Should().Be(userBadge.Id);
        badgeEarnedEvent.UserId.Should().Be(userId);
        badgeEarnedEvent.BadgeId.Should().Be(badgeId);
        badgeEarnedEvent.BadgeCode.Should().Be(badgeCode);
        badgeEarnedEvent.EarnedAt.Should().Be(userBadge.EarnedAt);
    }

    [Fact]
    public void Award_WithEmptyUserId_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.Empty;
        var badgeId = Guid.NewGuid();

        // Act
        var act = () => UserBadge.Award(userId, badgeId, "CODE");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("userId")
            .WithMessage("*UserId cannot be empty*");
    }

    [Fact]
    public void Award_WithEmptyBadgeId_ThrowsArgumentException()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var badgeId = Guid.Empty;

        // Act
        var act = () => UserBadge.Award(userId, badgeId, "CODE");

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("badgeId")
            .WithMessage("*BadgeId cannot be empty*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Award_WithEmptyBadgeCode_ThrowsArgumentException(string? invalidCode)
    {
        // Arrange
        var userId = Guid.NewGuid();
        var badgeId = Guid.NewGuid();

        // Act
        var act = () => UserBadge.Award(userId, badgeId, invalidCode!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("badgeCode")
            .WithMessage("*BadgeCode is required*");
    }

    #endregion

    #region Show/Hide Tests

    [Fact]
    public void Show_WhenHidden_ShowsBadge()
    {
        // Arrange
        var userBadge = CreateTestUserBadge();
        userBadge.Hide();
        userBadge.IsDisplayed.Should().BeFalse();

        // Act
        userBadge.Show();

        // Assert
        userBadge.IsDisplayed.Should().BeTrue();
    }

    [Fact]
    public void Show_WhenAlreadyShown_DoesNotThrow()
    {
        // Arrange
        var userBadge = CreateTestUserBadge();
        userBadge.IsDisplayed.Should().BeTrue();

        // Act
        var act = () => userBadge.Show();

        // Assert
        act.Should().NotThrow();
        userBadge.IsDisplayed.Should().BeTrue();
    }

    [Fact]
    public void Show_WhenRevoked_ThrowsInvalidOperationException()
    {
        // Arrange
        var userBadge = CreateTestUserBadge();
        userBadge.Revoke("Badge system changed");

        // Act
        var act = () => userBadge.Show();

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Cannot show a revoked badge*");
    }

    [Fact]
    public void Hide_WhenShown_HidesBadge()
    {
        // Arrange
        var userBadge = CreateTestUserBadge();
        userBadge.IsDisplayed.Should().BeTrue();

        // Act
        userBadge.Hide();

        // Assert
        userBadge.IsDisplayed.Should().BeFalse();
    }

    [Fact]
    public void Hide_WhenAlreadyHidden_DoesNotThrow()
    {
        // Arrange
        var userBadge = CreateTestUserBadge();
        userBadge.Hide();

        // Act
        var act = () => userBadge.Hide();

        // Assert
        act.Should().NotThrow();
        userBadge.IsDisplayed.Should().BeFalse();
    }

    #endregion

    #region Revoke Tests

    [Fact]
    public void Revoke_WithValidReason_RevokesBadge()
    {
        // Arrange
        var userBadge = CreateTestUserBadge();
        var reason = "Badge system changed";

        // Act
        userBadge.Revoke(reason);

        // Assert
        userBadge.IsActive.Should().BeFalse();
        userBadge.RevokedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
        userBadge.RevocationReason.Should().Be(reason);
        userBadge.IsDisplayed.Should().BeFalse();
    }

    [Fact]
    public void Revoke_TrimsReason()
    {
        // Arrange
        var userBadge = CreateTestUserBadge();

        // Act
        userBadge.Revoke("  Badge removed  ");

        // Assert
        userBadge.RevocationReason.Should().Be("Badge removed");
    }

    [Fact]
    public void Revoke_RaisesBadgeRevokedEvent()
    {
        // Arrange
        var userBadge = CreateTestUserBadge();
        userBadge.ClearDomainEvents(); // Clear the BadgeEarned event
        var reason = "Badge system changed";

        // Act
        userBadge.Revoke(reason);

        // Assert
        var domainEvents = userBadge.DomainEvents;
        domainEvents.Should().ContainSingle();

        var badgeRevokedEvent = domainEvents.First() as BadgeRevokedEvent;
        badgeRevokedEvent.Should().NotBeNull();
        badgeRevokedEvent!.UserBadgeId.Should().Be(userBadge.Id);
        badgeRevokedEvent.UserId.Should().Be(userBadge.UserId);
        badgeRevokedEvent.BadgeId.Should().Be(userBadge.BadgeId);
        badgeRevokedEvent.Reason.Should().Be(reason);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Revoke_WithEmptyReason_ThrowsArgumentException(string? invalidReason)
    {
        // Arrange
        var userBadge = CreateTestUserBadge();

        // Act
        var act = () => userBadge.Revoke(invalidReason!);

        // Assert
        act.Should().Throw<ArgumentException>()
            .WithParameterName("reason")
            .WithMessage("*Revocation reason is required*");
    }

    [Fact]
    public void Revoke_WhenAlreadyRevoked_ThrowsInvalidOperationException()
    {
        // Arrange
        var userBadge = CreateTestUserBadge();
        userBadge.Revoke("First revocation");

        // Act
        var act = () => userBadge.Revoke("Second revocation");

        // Assert
        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*Badge is already revoked*");
    }

    #endregion

    #region IsActive Tests

    [Fact]
    public void IsActive_WhenNotRevoked_ReturnsTrue()
    {
        // Arrange
        var userBadge = CreateTestUserBadge();

        // Assert
        userBadge.IsActive.Should().BeTrue();
    }

    [Fact]
    public void IsActive_WhenRevoked_ReturnsFalse()
    {
        // Arrange
        var userBadge = CreateTestUserBadge();
        userBadge.Revoke("Test revocation");

        // Assert
        userBadge.IsActive.Should().BeFalse();
    }

    #endregion

    #region Internal Constructor Tests

    [Fact]
    public void InternalConstructor_WithAllParameters_SetsAllProperties()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var badgeId = Guid.NewGuid();
        var earnedAt = DateTime.UtcNow.AddDays(-1);
        var triggeringShareRequestId = Guid.NewGuid();
        var isDisplayed = false;
        var revokedAt = DateTime.UtcNow;
        var revocationReason = "Test revocation";

        // Act
        var userBadge = new UserBadge(
            id, userId, badgeId, earnedAt, triggeringShareRequestId,
            isDisplayed, revokedAt, revocationReason);

        // Assert
        userBadge.Id.Should().Be(id);
        userBadge.UserId.Should().Be(userId);
        userBadge.BadgeId.Should().Be(badgeId);
        userBadge.EarnedAt.Should().Be(earnedAt);
        userBadge.TriggeringShareRequestId.Should().Be(triggeringShareRequestId);
        userBadge.IsDisplayed.Should().Be(isDisplayed);
        userBadge.RevokedAt.Should().Be(revokedAt);
        userBadge.RevocationReason.Should().Be(revocationReason);
        userBadge.IsActive.Should().BeFalse(); // Because revokedAt is set
    }

    [Fact]
    public void InternalConstructor_WithNullRevokedAt_IsActive()
    {
        // Arrange
        var id = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var badgeId = Guid.NewGuid();
        var earnedAt = DateTime.UtcNow;

        // Act
        var userBadge = new UserBadge(
            id, userId, badgeId, earnedAt, null,
            isDisplayed: true, revokedAt: null, revocationReason: null);

        // Assert
        userBadge.IsActive.Should().BeTrue();
        userBadge.RevokedAt.Should().BeNull();
        userBadge.RevocationReason.Should().BeNull();
    }

    #endregion

    #region Helper Methods

    private static UserBadge CreateTestUserBadge()
    {
        return UserBadge.Award(
            Guid.NewGuid(),
            Guid.NewGuid(),
            "TEST_BADGE");
    }

    #endregion
}