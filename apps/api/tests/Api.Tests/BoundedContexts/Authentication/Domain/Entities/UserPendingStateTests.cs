using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Enums;
using Api.SharedKernel.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Tests for the User entity's Pending state support (admin invitation flow).
/// Covers CreatePending factory method and ActivateFromInvitation transition.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "Authentication")]
public sealed class UserPendingStateTests
{
    private static readonly Email TestEmail = Email.Parse("invited@example.com");
    private const string TestDisplayName = "Invited User";
    private static readonly Role TestRole = Role.User;
    private static readonly UserTier TestTier = UserTier.Free;
    private static readonly Guid TestAdminId = Guid.NewGuid();

    #region CreatePending Tests

    [Fact]
    public void CreatePending_SetsCorrectState()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var expiresAt = now.AddDays(7);

        // Act
        var user = User.CreatePending(
            TestEmail,
            TestDisplayName,
            TestRole,
            TestTier,
            TestAdminId,
            expiresAt,
            timeProvider);

        // Assert
        Assert.Equal(UserAccountStatus.Pending, user.Status);
        Assert.Equal(TestEmail, user.Email);
        Assert.Equal(TestDisplayName, user.DisplayName);
        Assert.Null(user.PasswordHash);
        Assert.Equal(TestAdminId, user.InvitedByUserId);
        Assert.Equal(expiresAt, user.InvitationExpiresAt);
        Assert.False(user.EmailVerified);
        Assert.Equal(TestRole, user.Role);
        Assert.Equal(TestTier, user.Tier);
        Assert.Equal(now, user.CreatedAt);
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Fact]
    public void CreatePending_CannotAuthenticate()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Act
        var user = User.CreatePending(
            TestEmail,
            TestDisplayName,
            TestRole,
            TestTier,
            TestAdminId,
            now.AddDays(7),
            timeProvider);

        // Assert
        Assert.False(user.CanAuthenticate());
    }

    [Fact]
    public void CreatePending_EmitsUserProvisionedEvent()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var now = timeProvider.GetUtcNow().UtcDateTime;

        // Act
        var user = User.CreatePending(
            TestEmail,
            TestDisplayName,
            TestRole,
            TestTier,
            TestAdminId,
            now.AddDays(7),
            timeProvider);

        // Assert
        Assert.Single(user.DomainEvents);
        var domainEvent = Assert.IsType<UserProvisionedEvent>(user.DomainEvents.First());
        Assert.Equal(user.Id, domainEvent.UserId);
        Assert.Equal(TestEmail.Value, domainEvent.Email);
        Assert.Equal(TestDisplayName, domainEvent.DisplayName);
        Assert.Equal(TestRole.Value, domainEvent.Role);
        Assert.Equal(TestTier.Value, domainEvent.Tier);
        Assert.Equal(TestAdminId, domainEvent.InvitedByUserId);
    }

    #endregion

    #region ActivateFromInvitation Tests

    [Fact]
    public void ActivateFromInvitation_TransitionsPendingToActive()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = User.CreatePending(
            TestEmail,
            TestDisplayName,
            TestRole,
            TestTier,
            TestAdminId,
            now.AddDays(7),
            timeProvider);
        user.ClearDomainEvents();

        var passwordHash = PasswordHash.Create("SecurePassword123!");

        // Act
        user.ActivateFromInvitation(passwordHash, timeProvider);

        // Assert
        Assert.Equal(UserAccountStatus.Active, user.Status);
        Assert.Equal(passwordHash, user.PasswordHash);
        Assert.True(user.EmailVerified);
        Assert.Equal(now, user.EmailVerifiedAt);
        Assert.True(user.CanAuthenticate());
    }

    [Fact]
    public void ActivateFromInvitation_ThrowsIfNotPending()
    {
        // Arrange — create an Active user (not pending)
        var activeUser = new User(
            Guid.NewGuid(),
            Email.Parse("active@example.com"),
            "Active User",
            PasswordHash.Create("SecurePassword123!"),
            Role.User,
            UserTier.Free);

        var passwordHash = PasswordHash.Create("NewPassword123!");
        var timeProvider = new FakeTimeProvider();

        // Act & Assert
        var exception = Assert.Throws<InvalidOperationException>(
            () => activeUser.ActivateFromInvitation(passwordHash, timeProvider));
        Assert.Contains("Pending", exception.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void ActivateFromInvitation_EmitsUserActivatedEvent()
    {
        // Arrange
        var timeProvider = new FakeTimeProvider();
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var user = User.CreatePending(
            TestEmail,
            TestDisplayName,
            TestRole,
            TestTier,
            TestAdminId,
            now.AddDays(7),
            timeProvider);
        user.ClearDomainEvents(); // Clear the provisioned event

        var passwordHash = PasswordHash.Create("SecurePassword123!");

        // Act
        user.ActivateFromInvitation(passwordHash, timeProvider);

        // Assert
        Assert.Single(user.DomainEvents);
        var domainEvent = Assert.IsType<UserActivatedFromInvitationEvent>(user.DomainEvents.First());
        Assert.Equal(user.Id, domainEvent.UserId);
        Assert.Equal(TestEmail.Value, domainEvent.Email);
        Assert.Equal(TestRole.Value, domainEvent.Role);
        Assert.Equal(TestTier.Value, domainEvent.Tier);
        Assert.Equal(TestAdminId, domainEvent.InvitedByUserId);
    }

    #endregion
}
