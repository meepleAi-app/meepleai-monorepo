using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Enums;
using Api.SharedKernel.Domain.ValueObjects;
using Microsoft.Extensions.Time.Testing;
using Xunit;
using FluentAssertions;

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
        user.Status.Should().Be(UserAccountStatus.Pending);
        user.Email.Should().Be(TestEmail);
        user.DisplayName.Should().Be(TestDisplayName);
        user.PasswordHash.Should().BeNull();
        user.InvitedByUserId.Should().Be(TestAdminId);
        user.InvitationExpiresAt.Should().Be(expiresAt);
        user.EmailVerified.Should().BeFalse();
        user.Role.Should().Be(TestRole);
        user.Tier.Should().Be(TestTier);
        user.CreatedAt.Should().Be(now);
        user.Id.Should().NotBe(Guid.Empty);
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
        user.CanAuthenticate().Should().BeFalse();
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
        user.DomainEvents.Should().ContainSingle();
        var domainEvent = user.DomainEvents.First().Should().BeOfType<UserProvisionedEvent>().Subject;
        domainEvent.UserId.Should().Be(user.Id);
        domainEvent.Email.Should().Be(TestEmail.Value);
        domainEvent.DisplayName.Should().Be(TestDisplayName);
        domainEvent.Role.Should().Be(TestRole.Value);
        domainEvent.Tier.Should().Be(TestTier.Value);
        domainEvent.InvitedByUserId.Should().Be(TestAdminId);
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
        user.Status.Should().Be(UserAccountStatus.Active);
        user.PasswordHash.Should().Be(passwordHash);
        user.EmailVerified.Should().BeTrue();
        user.EmailVerifiedAt.Should().Be(now);
        user.CanAuthenticate().Should().BeTrue();
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
        var act = () => activeUser.ActivateFromInvitation(passwordHash, timeProvider);
        var exception = act.Should().Throw<InvalidOperationException>().Which;
        exception.Message.Should().ContainEquivalentOf("Pending");
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
        user.DomainEvents.Should().ContainSingle();
        var domainEvent = user.DomainEvents.First().Should().BeOfType<UserActivatedFromInvitationEvent>().Subject;
        domainEvent.UserId.Should().Be(user.Id);
        domainEvent.Email.Should().Be(TestEmail.Value);
        domainEvent.Role.Should().Be(TestRole.Value);
        domainEvent.Tier.Should().Be(TestTier.Value);
        domainEvent.InvitedByUserId.Should().Be(TestAdminId);
    }

    #endregion
}
