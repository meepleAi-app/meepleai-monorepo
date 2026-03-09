using Api.BoundedContexts.Authentication.Domain.Events;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Events;

/// <summary>
/// Tests for Authentication domain events.
/// Issue #3025: Backend 90% Coverage Target - Phase 24
/// </summary>
[Trait("Category", "Unit")]
public sealed class AuthenticationDomainEventsTests
{
    #region ApiKeyRevokedEvent Tests

    [Fact]
    public void ApiKeyRevokedEvent_WithReason_SetsProperties()
    {
        // Arrange
        var apiKeyId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var evt = new ApiKeyRevokedEvent(apiKeyId, userId, "Security concern");

        // Assert
        evt.ApiKeyId.Should().Be(apiKeyId);
        evt.UserId.Should().Be(userId);
        evt.Reason.Should().Be("Security concern");
    }

    [Fact]
    public void ApiKeyRevokedEvent_WithoutReason_SetsNullReason()
    {
        // Arrange
        var apiKeyId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var evt = new ApiKeyRevokedEvent(apiKeyId, userId);

        // Assert
        evt.ApiKeyId.Should().Be(apiKeyId);
        evt.UserId.Should().Be(userId);
        evt.Reason.Should().BeNull();
    }

    #endregion

    #region ApiKeyUsedEvent Tests

    [Fact]
    public void ApiKeyUsedEvent_WithAllParameters_SetsProperties()
    {
        // Arrange
        var keyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var usedAt = DateTime.UtcNow.AddMinutes(-5);

        // Act
        var evt = new ApiKeyUsedEvent(
            keyId,
            userId,
            "/api/v1/games",
            "192.168.1.1",
            "Mozilla/5.0",
            usedAt);

        // Assert
        evt.KeyId.Should().Be(keyId);
        evt.UserId.Should().Be(userId);
        evt.Endpoint.Should().Be("/api/v1/games");
        evt.IpAddress.Should().Be("192.168.1.1");
        evt.UserAgent.Should().Be("Mozilla/5.0");
        evt.UsedAt.Should().Be(usedAt);
    }

    [Fact]
    public void ApiKeyUsedEvent_WithMinimalParameters_SetsDefaults()
    {
        // Arrange
        var keyId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var beforeCreation = DateTime.UtcNow;

        // Act
        var evt = new ApiKeyUsedEvent(keyId, userId, "/api/v1/users");

        // Assert
        evt.KeyId.Should().Be(keyId);
        evt.UserId.Should().Be(userId);
        evt.Endpoint.Should().Be("/api/v1/users");
        evt.IpAddress.Should().BeNull();
        evt.UserAgent.Should().BeNull();
        evt.UsedAt.Should().BeOnOrAfter(beforeCreation);
    }

    [Fact]
    public void ApiKeyUsedEvent_WithEmptyEndpoint_ThrowsArgumentException()
    {
        // Act
        var action = () => new ApiKeyUsedEvent(Guid.NewGuid(), Guid.NewGuid(), "");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("endpoint");
    }

    [Fact]
    public void ApiKeyUsedEvent_WithWhitespaceEndpoint_ThrowsArgumentException()
    {
        // Act
        var action = () => new ApiKeyUsedEvent(Guid.NewGuid(), Guid.NewGuid(), "   ");

        // Assert
        action.Should().Throw<ArgumentException>()
            .WithParameterName("endpoint");
    }

    #endregion

    #region EmailChangedEvent Tests

    [Fact]
    public void EmailChangedEvent_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldEmail = new Email("old@example.com");
        var newEmail = new Email("new@example.com");

        // Act
        var evt = new EmailChangedEvent(userId, oldEmail, newEmail);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.OldEmail.Should().Be("old@example.com");
        evt.NewEmail.Should().Be("new@example.com");
    }

    #endregion

    #region OAuthAccountLinkedEvent Tests

    [Fact]
    public void OAuthAccountLinkedEvent_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new OAuthAccountLinkedEvent(userId, "google", "google-user-123");

        // Assert
        evt.UserId.Should().Be(userId);
        evt.Provider.Should().Be("google");
        evt.ProviderUserId.Should().Be("google-user-123");
    }

    #endregion

    #region OAuthAccountUnlinkedEvent Tests

    [Fact]
    public void OAuthAccountUnlinkedEvent_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new OAuthAccountUnlinkedEvent(userId, "discord");

        // Assert
        evt.UserId.Should().Be(userId);
        evt.Provider.Should().Be("discord");
    }

    #endregion

    #region OAuthTokensRefreshedEvent Tests

    [Fact]
    public void OAuthTokensRefreshedEvent_SetsProperties()
    {
        // Arrange
        var oauthAccountId = Guid.NewGuid();
        var expiresAt = DateTime.UtcNow.AddHours(1);

        // Act
        var evt = new OAuthTokensRefreshedEvent(oauthAccountId, "github", expiresAt);

        // Assert
        evt.OAuthAccountId.Should().Be(oauthAccountId);
        evt.Provider.Should().Be("github");
        evt.ExpiresAt.Should().Be(expiresAt);
    }

    #endregion

    #region PasswordChangedEvent Tests

    [Fact]
    public void PasswordChangedEvent_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new PasswordChangedEvent(userId);

        // Assert
        evt.UserId.Should().Be(userId);
    }

    #endregion

    #region PasswordResetEvent Tests

    [Fact]
    public void PasswordResetEvent_WithResetByUserId_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var adminId = Guid.NewGuid();

        // Act
        var evt = new PasswordResetEvent(userId, adminId);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.ResetByUserId.Should().Be(adminId);
    }

    [Fact]
    public void PasswordResetEvent_WithoutResetByUserId_SetsNullResetBy()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new PasswordResetEvent(userId);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.ResetByUserId.Should().BeNull();
    }

    #endregion

    #region RoleChangedEvent Tests

    [Fact]
    public void RoleChangedEvent_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldRole = Role.User;
        var newRole = Role.Admin;

        // Act
        var evt = new RoleChangedEvent(userId, oldRole, newRole);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.OldRole.Should().Be("user");
        evt.NewRole.Should().Be("admin");
    }

    [Fact]
    public void RoleChangedEvent_FromEditorToUser_SetsRoleValues()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new RoleChangedEvent(userId, Role.Editor, Role.User);

        // Assert
        evt.OldRole.Should().Be("editor");
        evt.NewRole.Should().Be("user");
    }

    #endregion

    #region SessionExtendedEvent Tests

    [Fact]
    public void SessionExtendedEvent_SetsProperties()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();
        var extension = TimeSpan.FromHours(2);
        var newExpiresAt = DateTime.UtcNow.AddHours(4);

        // Act
        var evt = new SessionExtendedEvent(sessionId, userId, extension, newExpiresAt);

        // Assert
        evt.SessionId.Should().Be(sessionId);
        evt.UserId.Should().Be(userId);
        evt.Extension.Should().Be(extension);
        evt.NewExpiresAt.Should().Be(newExpiresAt);
    }

    #endregion

    #region SessionRevokedEvent Tests

    [Fact]
    public void SessionRevokedEvent_WithReason_SetsProperties()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var evt = new SessionRevokedEvent(sessionId, userId, "User requested logout");

        // Assert
        evt.SessionId.Should().Be(sessionId);
        evt.UserId.Should().Be(userId);
        evt.Reason.Should().Be("User requested logout");
    }

    [Fact]
    public void SessionRevokedEvent_WithoutReason_SetsNullReason()
    {
        // Arrange
        var sessionId = Guid.NewGuid();
        var userId = Guid.NewGuid();

        // Act
        var evt = new SessionRevokedEvent(sessionId, userId);

        // Assert
        evt.SessionId.Should().Be(sessionId);
        evt.UserId.Should().Be(userId);
        evt.Reason.Should().BeNull();
    }

    #endregion

    #region TwoFactorDisabledEvent Tests

    [Fact]
    public void TwoFactorDisabledEvent_WithAdminOverride_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new TwoFactorDisabledEvent(userId, wasAdminOverride: true);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.WasAdminOverride.Should().BeTrue();
    }

    [Fact]
    public void TwoFactorDisabledEvent_WithoutAdminOverride_SetsFalseByDefault()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new TwoFactorDisabledEvent(userId);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.WasAdminOverride.Should().BeFalse();
    }

    #endregion

    #region TwoFactorEnabledEvent Tests

    [Fact]
    public void TwoFactorEnabledEvent_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new TwoFactorEnabledEvent(userId, backupCodesCount: 10);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.BackupCodesCount.Should().Be(10);
    }

    #endregion

    #region UserTierChangedEvent Tests

    [Fact]
    public void UserTierChangedEvent_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new UserTierChangedEvent(userId, UserTier.Free, UserTier.Premium);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.OldTier.Should().Be(UserTier.Free);
        evt.NewTier.Should().Be(UserTier.Premium);
    }

    [Fact]
    public void UserTierChangedEvent_FromNormalToFree_SetsCorrectTiers()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new UserTierChangedEvent(userId, UserTier.Normal, UserTier.Free);

        // Assert
        evt.OldTier.Value.Should().Be("normal");
        evt.NewTier.Value.Should().Be("free");
    }

    #endregion

    #region UserLevelChangedEvent Tests

    [Fact]
    public void UserLevelChangedEvent_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();
        var oldLevel = 5;
        var newLevel = 10;

        // Act
        var evt = new UserLevelChangedEvent(userId, oldLevel, newLevel);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.OldLevel.Should().Be(oldLevel);
        evt.NewLevel.Should().Be(newLevel);
    }

    [Fact]
    public void UserLevelChangedEvent_WithLevelDecrease_SetsCorrectValues()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new UserLevelChangedEvent(userId, oldLevel: 15, newLevel: 10);

        // Assert
        evt.OldLevel.Should().Be(15);
        evt.NewLevel.Should().Be(10);
    }

    [Fact]
    public void UserLevelChangedEvent_WithZeroLevels_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new UserLevelChangedEvent(userId, oldLevel: 0, newLevel: 1);

        // Assert
        evt.OldLevel.Should().Be(0);
        evt.NewLevel.Should().Be(1);
    }

    #endregion

    #region UserSuspendedEvent Tests

    [Fact]
    public void UserSuspendedEvent_WithReason_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new UserSuspendedEvent(userId, "Policy violation");

        // Assert
        evt.UserId.Should().Be(userId);
        evt.Reason.Should().Be("Policy violation");
    }

    [Fact]
    public void UserSuspendedEvent_WithoutReason_SetsNullReason()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new UserSuspendedEvent(userId);

        // Assert
        evt.UserId.Should().Be(userId);
        evt.Reason.Should().BeNull();
    }

    #endregion

    #region UserUnsuspendedEvent Tests

    [Fact]
    public void UserUnsuspendedEvent_SetsProperties()
    {
        // Arrange
        var userId = Guid.NewGuid();

        // Act
        var evt = new UserUnsuspendedEvent(userId);

        // Assert
        evt.UserId.Should().Be(userId);
    }

    #endregion
}
