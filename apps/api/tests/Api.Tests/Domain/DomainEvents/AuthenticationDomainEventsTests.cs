using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Domain.DomainEvents;

/// <summary>
/// Tests for Authentication domain event publishing.
/// Verifies that domain events are raised when aggregate methods are called.
/// </summary>
public class AuthenticationDomainEventsTests
{
    [Fact]
    public void ChangePassword_ShouldRaisePasswordChangedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var newPasswordHash = PasswordHash.Create("NewPassword123!");

        // Act
        user.ChangePassword("TestPassword123!", newPasswordHash);

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<PasswordChangedEvent>();

        var passwordChangedEvent = (PasswordChangedEvent)domainEvent;
        passwordChangedEvent.UserId.Should().Be(user.Id);
        passwordChangedEvent.EventId.Should().NotBeEmpty();
        passwordChangedEvent.OccurredAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(5));
    }

    [Fact]
    public void UpdatePassword_ShouldRaisePasswordResetEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var newPasswordHash = PasswordHash.Create("NewPassword123!");

        // Act
        user.UpdatePassword(newPasswordHash);

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<PasswordResetEvent>();

        var passwordResetEvent = (PasswordResetEvent)domainEvent;
        passwordResetEvent.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void UpdateEmail_ShouldRaiseEmailChangedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var oldEmail = user.Email;
        var newEmail = Email.Parse("newemail@example.com");

        // Act
        user.UpdateEmail(newEmail);

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<EmailChangedEvent>();

        var emailChangedEvent = (EmailChangedEvent)domainEvent;
        emailChangedEvent.UserId.Should().Be(user.Id);
        emailChangedEvent.OldEmail.Should().Be(oldEmail.Value);
        emailChangedEvent.NewEmail.Should().Be(newEmail.Value);
    }

    [Fact]
    public void AssignRole_ShouldRaiseRoleChangedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var oldRole = user.Role;
        var newRole = Role.Editor;
        var requesterRole = Role.Admin;

        // Act
        user.AssignRole(newRole, requesterRole);

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<RoleChangedEvent>();

        var roleChangedEvent = (RoleChangedEvent)domainEvent;
        roleChangedEvent.UserId.Should().Be(user.Id);
        roleChangedEvent.OldRole.Should().Be(oldRole.Value);
        roleChangedEvent.NewRole.Should().Be(newRole.Value);
    }

    [Fact]
    public void Enable2FA_ShouldRaiseTwoFactorEnabledEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var totpSecret = TotpSecret.FromEncrypted("mock_encrypted_totp_secret_base64");

        // Act
        user.Enable2FA(totpSecret);

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<TwoFactorEnabledEvent>();

        var twoFactorEnabledEvent = (TwoFactorEnabledEvent)domainEvent;
        twoFactorEnabledEvent.UserId.Should().Be(user.Id);
        twoFactorEnabledEvent.BackupCodesCount.Should().BeGreaterThanOrEqualTo(0);
    }

    [Fact]
    public void Disable2FA_ShouldRaiseTwoFactorDisabledEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var totpSecret = TotpSecret.FromEncrypted("mock_encrypted_totp_secret_base64");
        user.Enable2FA(totpSecret);
        user.ClearDomainEvents(); // Clear the Enable event

        // Act
        user.Disable2FA();

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<TwoFactorDisabledEvent>();

        var twoFactorDisabledEvent = (TwoFactorDisabledEvent)domainEvent;
        twoFactorDisabledEvent.UserId.Should().Be(user.Id);
    }

    [Fact]
    public void LinkOAuthAccount_ShouldRaiseOAuthAccountLinkedEvent()
    {
        // Arrange
        var user = CreateTestUser();
        var oauthAccount = new OAuthAccount(
            id: Guid.NewGuid(),
            userId: user.Id,
            provider: "google",
            providerUserId: "google123",
            accessTokenEncrypted: "encrypted_token"
        );

        // Act
        user.LinkOAuthAccount(oauthAccount);

        // Assert
        user.DomainEvents.Should().HaveCount(1);
        var domainEvent = user.DomainEvents.First();
        domainEvent.Should().BeOfType<OAuthAccountLinkedEvent>();

        var oauthLinkedEvent = (OAuthAccountLinkedEvent)domainEvent;
        oauthLinkedEvent.UserId.Should().Be(user.Id);
        oauthLinkedEvent.Provider.Should().Be("google");
        oauthLinkedEvent.ProviderUserId.Should().Be("google123");
    }

    [Fact]
    public void ApiKeyRevoke_ShouldRaiseApiKeyRevokedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var (apiKey, _) = ApiKey.Create(
            id: Guid.NewGuid(),
            userId: userId,
            keyName: "Test Key",
            scopes: "read,write"
        );

        // Act
        apiKey.Revoke(userId, "Security audit");

        // Assert
        apiKey.DomainEvents.Should().HaveCount(1);
        var domainEvent = apiKey.DomainEvents.First();
        domainEvent.Should().BeOfType<ApiKeyRevokedEvent>();

        var apiKeyRevokedEvent = (ApiKeyRevokedEvent)domainEvent;
        apiKeyRevokedEvent.ApiKeyId.Should().Be(apiKey.Id);
        apiKeyRevokedEvent.UserId.Should().Be(userId);
        apiKeyRevokedEvent.Reason.Should().Be("Security audit");
    }

    [Fact]
    public void SessionRevoke_ShouldRaiseSessionRevokedEvent()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var sessionToken = SessionToken.Generate();
        var session = new Session(
            id: Guid.NewGuid(),
            userId: userId,
            token: sessionToken
        );

        // Act
        session.Revoke(reason: "User logged out");

        // Assert
        session.DomainEvents.Should().HaveCount(1);
        var domainEvent = session.DomainEvents.First();
        domainEvent.Should().BeOfType<SessionRevokedEvent>();

        var sessionRevokedEvent = (SessionRevokedEvent)domainEvent;
        sessionRevokedEvent.SessionId.Should().Be(session.Id);
        sessionRevokedEvent.UserId.Should().Be(userId);
        sessionRevokedEvent.Reason.Should().Be("User logged out");
    }

    private static User CreateTestUser()
    {
        return new User(
            id: Guid.NewGuid(),
            email: Email.Parse("test@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("TestPassword123!"),
            role: Role.User
        );
    }
}

