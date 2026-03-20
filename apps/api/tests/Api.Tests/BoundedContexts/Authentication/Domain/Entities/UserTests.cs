using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.Entities;

/// <summary>
/// Tests for the User aggregate root entity.
/// Issue #3025: Backend 90% Coverage Target - Phase 30
/// </summary>
[Trait("Category", "Unit")]
public sealed class UserTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidData_CreatesUser()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = new Email("test@example.com");
        var displayName = "Test User";
        var passwordHash = PasswordHash.Create("SecurePassword123!");
        var role = Role.User;

        // Act
        var user = new User(id, email, displayName, passwordHash, role);

        // Assert
        user.Id.Should().Be(id);
        user.Email.Should().Be(email);
        user.DisplayName.Should().Be(displayName);
        user.PasswordHash.Should().Be(passwordHash);
        user.Role.Should().Be(role);
        user.Tier.Should().Be(UserTier.Free);
        user.CreatedAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        user.IsDemoAccount.Should().BeFalse();
        user.IsTwoFactorEnabled.Should().BeFalse();
        user.Language.Should().Be("en");
        user.EmailNotifications.Should().BeTrue();
        user.Theme.Should().Be("system");
        user.DataRetentionDays.Should().Be(90);
    }

    [Fact]
    public void Constructor_WithCustomTier_SetsTier()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = new Email("premium@example.com");
        var passwordHash = PasswordHash.Create("SecurePassword123!");

        // Act
        var user = new User(id, email, "Premium User", passwordHash, Role.User, UserTier.Premium);

        // Assert
        user.Tier.Should().Be(UserTier.Premium);
    }

    [Fact]
    public void Constructor_WithNullEmail_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new User(
            Guid.NewGuid(),
            null!,
            "Test User",
            PasswordHash.Create("SecurePassword123!"),
            Role.User);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("email");
    }

    [Fact]
    public void Constructor_WithNullDisplayName_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new User(
            Guid.NewGuid(),
            new Email("test@example.com"),
            null!,
            PasswordHash.Create("SecurePassword123!"),
            Role.User);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("displayName");
    }

    [Fact]
    public void Constructor_WithNullPasswordHash_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new User(
            Guid.NewGuid(),
            new Email("test@example.com"),
            "Test User",
            null!,
            Role.User);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("passwordHash");
    }

    [Fact]
    public void Constructor_WithNullRole_ThrowsArgumentNullException()
    {
        // Act
        var action = () => new User(
            Guid.NewGuid(),
            new Email("test@example.com"),
            "Test User",
            PasswordHash.Create("SecurePassword123!"),
            null!);

        // Assert
        action.Should().Throw<ArgumentNullException>()
            .WithParameterName("role");
    }

    #endregion

    #region VerifyPassword Tests

    [Fact]
    public void VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateValidUser(password);

        // Act
        var result = user.VerifyPassword(password);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void VerifyPassword_WithIncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var user = CreateValidUser("SecurePassword123!");

        // Act
        var result = user.VerifyPassword("WrongPassword123!");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ChangePassword Tests

    [Fact]
    public void ChangePassword_WithCorrectCurrentPassword_UpdatesPassword()
    {
        // Arrange
        var currentPassword = "SecurePassword123!";
        var user = CreateValidUser(currentPassword);
        var newPasswordHash = PasswordHash.Create("NewSecurePassword456!");
        user.ClearDomainEvents();

        // Act
        user.ChangePassword(currentPassword, newPasswordHash);

        // Assert
        user.PasswordHash.Should().Be(newPasswordHash);
        user.DomainEvents.Should().HaveCount(1);
        user.DomainEvents.First().Should().BeOfType<Api.BoundedContexts.Authentication.Domain.Events.PasswordChangedEvent>();
    }

    [Fact]
    public void ChangePassword_WithIncorrectCurrentPassword_ThrowsDomainException()
    {
        // Arrange
        var user = CreateValidUser("SecurePassword123!");
        var newPasswordHash = PasswordHash.Create("NewSecurePassword456!");

        // Act
        var action = () => user.ChangePassword("WrongPassword", newPasswordHash);

        // Assert
        action.Should().Throw<DomainException>()
            .WithMessage("*Current password is incorrect*");
    }

    #endregion

    #region UpdatePassword Tests

    [Fact]
    public void UpdatePassword_UpdatesPasswordWithoutVerification()
    {
        // Arrange
        var user = CreateValidUser("OldPassword123!");
        var newPasswordHash = PasswordHash.Create("NewPassword456!");
        user.ClearDomainEvents();

        // Act
        user.UpdatePassword(newPasswordHash);

        // Assert
        user.PasswordHash.Should().Be(newPasswordHash);
        user.DomainEvents.Should().HaveCount(1);
        user.DomainEvents.First().Should().BeOfType<Api.BoundedContexts.Authentication.Domain.Events.PasswordResetEvent>();
    }

    #endregion

    #region MarkAsDemoAccount Tests

    [Fact]
    public void MarkAsDemoAccount_SetsIsDemoAccountTrue()
    {
        // Arrange
        var user = CreateValidUser();
        user.IsDemoAccount.Should().BeFalse();

        // Act
        user.MarkAsDemoAccount();

        // Assert
        user.IsDemoAccount.Should().BeTrue();
    }

    #endregion

    #region UpdateEmail Tests

    [Fact]
    public void UpdateEmail_WithNewEmail_UpdatesEmailAndAddsDomainEvent()
    {
        // Arrange
        var user = CreateValidUser();
        var newEmail = new Email("newemail@example.com");
        user.ClearDomainEvents();

        // Act
        user.UpdateEmail(newEmail);

        // Assert
        user.Email.Should().Be(newEmail);
        user.DomainEvents.Should().HaveCount(1);
        user.DomainEvents.First().Should().BeOfType<Api.BoundedContexts.Authentication.Domain.Events.EmailChangedEvent>();
    }

    [Fact]
    public void UpdateEmail_WithSameEmail_DoesNotAddDomainEvent()
    {
        // Arrange
        var email = new Email("test@example.com");
        var user = CreateValidUser();
        user.ClearDomainEvents();

        // Act
        user.UpdateEmail(email);

        // Assert
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateEmail_WithNullEmail_ThrowsArgumentNullException()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.UpdateEmail(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region UpdateDisplayName Tests

    [Fact]
    public void UpdateDisplayName_WithNewName_UpdatesDisplayName()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        user.UpdateDisplayName("New Display Name");

        // Assert
        user.DisplayName.Should().Be("New Display Name");
    }

    [Fact]
    public void UpdateDisplayName_WithSameName_DoesNothing()
    {
        // Arrange
        var user = CreateValidUser();
        var originalName = user.DisplayName;

        // Act
        user.UpdateDisplayName(originalName);

        // Assert
        user.DisplayName.Should().Be(originalName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDisplayName_WithEmptyName_ThrowsValidationException(string? newName)
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.UpdateDisplayName(newName!);

        // Assert
        action.Should().Throw<ValidationException>();
    }

    #endregion

    #region AssignRole Tests

    [Fact]
    public void AssignRole_WithAdminRequester_UpdatesRole()
    {
        // Arrange
        var user = CreateValidUser();
        var newRole = Role.Editor;
        user.ClearDomainEvents();

        // Act
        user.AssignRole(newRole, Role.SuperAdmin);

        // Assert
        user.Role.Should().Be(newRole);
        user.DomainEvents.Should().HaveCount(1);
        user.DomainEvents.First().Should().BeOfType<Api.BoundedContexts.Authentication.Domain.Events.RoleChangedEvent>();
    }

    [Fact]
    public void AssignRole_WithNonAdminRequester_ThrowsDomainException()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.AssignRole(Role.Editor, Role.User);

        // Assert
        action.Should().Throw<DomainException>()
            .WithMessage("*Only the SuperAdmin can assign roles*");
    }

    [Fact]
    public void AssignRole_AdminAssigningAdminToAdmin_ThrowsDomainException()
    {
        // Arrange
        var admin = CreateAdminUser();

        // Act — Admin requester is rejected because only SuperAdmin can assign roles
        var action = () => admin.AssignRole(Role.Admin, Role.Admin);

        // Assert
        action.Should().Throw<DomainException>()
            .WithMessage("*Only the SuperAdmin can assign roles*");
    }

    [Fact]
    public void AssignRole_WithNullNewRole_ThrowsArgumentNullException()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.AssignRole(null!, Role.Admin);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void AssignRole_WithNullRequesterRole_ThrowsArgumentNullException()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.AssignRole(Role.Editor, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region UpdateRole Tests

    [Fact]
    public void UpdateRole_WithNewRole_UpdatesRoleAndAddsDomainEvent()
    {
        // Arrange
        var user = CreateValidUser();
        user.ClearDomainEvents();

        // Act
        user.UpdateRole(Role.Editor);

        // Assert
        user.Role.Should().Be(Role.Editor);
        user.DomainEvents.Should().HaveCount(1);
        user.DomainEvents.First().Should().BeOfType<Api.BoundedContexts.Authentication.Domain.Events.RoleChangedEvent>();
    }

    [Fact]
    public void UpdateRole_WithSameRole_DoesNotAddDomainEvent()
    {
        // Arrange
        var user = CreateValidUser();
        user.ClearDomainEvents();

        // Act
        user.UpdateRole(Role.User);

        // Assert
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateRole_WithNullRole_ThrowsArgumentNullException()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.UpdateRole(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Enable2FA Tests

    [Fact]
    public void Enable2FA_WithValidSecret_EnablesTwoFactor()
    {
        // Arrange
        var user = CreateValidUser();
        var totpSecret = TotpSecret.FromEncrypted("encrypted-totp-secret-base64");
        var backupCodes = new List<BackupCode>
        {
            BackupCode.FromHashed("hash1"),
            BackupCode.FromHashed("hash2"),
            BackupCode.FromHashed("hash3")
        };
        user.ClearDomainEvents();

        // Act
        user.Enable2FA(totpSecret, backupCodes);

        // Assert
        user.IsTwoFactorEnabled.Should().BeTrue();
        user.TotpSecret.Should().Be(totpSecret);
        user.TwoFactorEnabledAt.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromSeconds(2));
        user.BackupCodes.Should().HaveCount(3);
        user.DomainEvents.Should().HaveCount(1);
        user.DomainEvents.First().Should().BeOfType<Api.BoundedContexts.Authentication.Domain.Events.TwoFactorEnabledEvent>();
    }

    [Fact]
    public void Enable2FA_WithNullBackupCodes_EnablesWithoutBackupCodes()
    {
        // Arrange
        var user = CreateValidUser();
        var totpSecret = TotpSecret.FromEncrypted("encrypted-secret");

        // Act
        user.Enable2FA(totpSecret, null);

        // Assert
        user.IsTwoFactorEnabled.Should().BeTrue();
        user.BackupCodes.Should().BeEmpty();
    }

    [Fact]
    public void Enable2FA_WhenAlreadyEnabled_ThrowsDomainException()
    {
        // Arrange
        var user = CreateValidUser();
        user.Enable2FA(TotpSecret.FromEncrypted("secret1"), null);

        // Act
        var action = () => user.Enable2FA(TotpSecret.FromEncrypted("secret2"), null);

        // Assert
        action.Should().Throw<DomainException>()
            .WithMessage("*Two-factor authentication is already enabled*");
    }

    [Fact]
    public void Enable2FA_WithUsedBackupCodes_ThrowsDomainException()
    {
        // Arrange
        var user = CreateValidUser();
        var totpSecret = TotpSecret.FromEncrypted("secret");
        var usedCode = BackupCode.FromHashed("hash", isUsed: true, usedAt: DateTime.UtcNow);
        var backupCodes = new List<BackupCode> { usedCode };

        // Act
        var action = () => user.Enable2FA(totpSecret, backupCodes);

        // Assert
        action.Should().Throw<DomainException>()
            .WithMessage("*Cannot enable 2FA with used backup codes*");
    }

    [Fact]
    public void Enable2FA_WithNullSecret_ThrowsArgumentNullException()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.Enable2FA(null!, null);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Disable2FA Tests

    [Fact]
    public void Disable2FA_WhenEnabled_DisablesTwoFactor()
    {
        // Arrange
        var user = CreateValidUser();
        user.Enable2FA(TotpSecret.FromEncrypted("secret"), new List<BackupCode>
        {
            BackupCode.FromHashed("hash1")
        });
        user.ClearDomainEvents();

        // Act
        user.Disable2FA();

        // Assert
        user.IsTwoFactorEnabled.Should().BeFalse();
        user.TotpSecret.Should().BeNull();
        user.TwoFactorEnabledAt.Should().BeNull();
        user.BackupCodes.Should().BeEmpty();
        user.DomainEvents.Should().HaveCount(1);
        user.DomainEvents.First().Should().BeOfType<Api.BoundedContexts.Authentication.Domain.Events.TwoFactorDisabledEvent>();
    }

    [Fact]
    public void Disable2FA_WhenNotEnabled_ThrowsDomainException()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.Disable2FA();

        // Assert
        action.Should().Throw<DomainException>()
            .WithMessage("*Two-factor authentication is not enabled*");
    }

    [Fact]
    public void Disable2FA_WithAdminOverride_SetsEventFlag()
    {
        // Arrange
        var user = CreateValidUser();
        user.Enable2FA(TotpSecret.FromEncrypted("secret"), null);
        user.ClearDomainEvents();

        // Act
        user.Disable2FA(wasAdminOverride: true);

        // Assert
        user.IsTwoFactorEnabled.Should().BeFalse();
        var domainEvent = user.DomainEvents.First() as Api.BoundedContexts.Authentication.Domain.Events.TwoFactorDisabledEvent;
        domainEvent.Should().NotBeNull();
    }

    #endregion

    #region RequiresTwoFactor Tests

    [Fact]
    public void RequiresTwoFactor_WhenEnabled_ReturnsTrue()
    {
        // Arrange
        var user = CreateValidUser();
        user.Enable2FA(TotpSecret.FromEncrypted("secret"), null);

        // Act
        var result = user.RequiresTwoFactor();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void RequiresTwoFactor_WhenDisabled_ReturnsFalse()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var result = user.RequiresTwoFactor();

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region UseBackupCode Tests

    [Fact]
    public void UseBackupCode_WithValidCode_MarksCodeAsUsed()
    {
        // Arrange
        var user = CreateValidUser();
        var backupCodes = new List<BackupCode>
        {
            BackupCode.FromHashed("hash1"),
            BackupCode.FromHashed("hash2")
        };
        user.Enable2FA(TotpSecret.FromEncrypted("secret"), backupCodes);
        var usedAt = DateTime.UtcNow;

        // Act
        user.UseBackupCode("hash1", usedAt);

        // Assert
        user.BackupCodes.First(bc => bc.HashedValue == "hash1").IsUsed.Should().BeTrue();
        user.BackupCodes.First(bc => bc.HashedValue == "hash2").IsUsed.Should().BeFalse();
    }

    [Fact]
    public void UseBackupCode_WithInvalidCode_ThrowsDomainException()
    {
        // Arrange
        var user = CreateValidUser();
        user.Enable2FA(TotpSecret.FromEncrypted("secret"), new List<BackupCode>
        {
            BackupCode.FromHashed("hash1")
        });

        // Act
        var action = () => user.UseBackupCode("nonexistent-hash", DateTime.UtcNow);

        // Assert
        action.Should().Throw<DomainException>()
            .WithMessage("*Backup code not found*");
    }

    #endregion

    #region GetUnusedBackupCodesCount Tests

    [Fact]
    public void GetUnusedBackupCodesCount_ReturnsCorrectCount()
    {
        // Arrange
        var user = CreateValidUser();
        var backupCodes = new List<BackupCode>
        {
            BackupCode.FromHashed("hash1"),
            BackupCode.FromHashed("hash2"),
            BackupCode.FromHashed("hash3")
        };
        user.Enable2FA(TotpSecret.FromEncrypted("secret"), backupCodes);
        user.UseBackupCode("hash1", DateTime.UtcNow);

        // Act
        var result = user.GetUnusedBackupCodesCount();

        // Assert
        result.Should().Be(2);
    }

    [Fact]
    public void GetUnusedBackupCodesCount_WithNoBackupCodes_ReturnsZero()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var result = user.GetUnusedBackupCodesCount();

        // Assert
        result.Should().Be(0);
    }

    #endregion

    #region HasUnusedBackupCode Tests

    [Fact]
    public void HasUnusedBackupCode_WithUnusedCode_ReturnsTrue()
    {
        // Arrange
        var user = CreateValidUser();
        user.Enable2FA(TotpSecret.FromEncrypted("secret"), new List<BackupCode>
        {
            BackupCode.FromHashed("hash1")
        });

        // Act
        var result = user.HasUnusedBackupCode("hash1");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasUnusedBackupCode_WithUsedCode_ReturnsFalse()
    {
        // Arrange
        var user = CreateValidUser();
        user.Enable2FA(TotpSecret.FromEncrypted("secret"), new List<BackupCode>
        {
            BackupCode.FromHashed("hash1")
        });
        user.UseBackupCode("hash1", DateTime.UtcNow);

        // Act
        var result = user.HasUnusedBackupCode("hash1");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void HasUnusedBackupCode_WithNonexistentCode_ReturnsFalse()
    {
        // Arrange
        var user = CreateValidUser();
        user.Enable2FA(TotpSecret.FromEncrypted("secret"), new List<BackupCode>
        {
            BackupCode.FromHashed("hash1")
        });

        // Act
        var result = user.HasUnusedBackupCode("nonexistent");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region LinkOAuthAccount Tests

    [Fact]
    public void LinkOAuthAccount_WithValidAccount_AddsAccount()
    {
        // Arrange
        var user = CreateValidUser();
        var oauthAccount = new OAuthAccount(
            Guid.NewGuid(),
            user.Id,
            "google",
            "provider-user-id",
            "encrypted-access-token");
        user.ClearDomainEvents();

        // Act
        user.LinkOAuthAccount(oauthAccount);

        // Assert
        user.OAuthAccounts.Should().HaveCount(1);
        user.OAuthAccounts.First().Provider.Should().Be("google");
        user.DomainEvents.Should().HaveCount(1);
        user.DomainEvents.First().Should().BeOfType<Api.BoundedContexts.Authentication.Domain.Events.OAuthAccountLinkedEvent>();
    }

    [Fact]
    public void LinkOAuthAccount_WithDuplicateProvider_ThrowsDomainException()
    {
        // Arrange
        var user = CreateValidUser();
        var account1 = new OAuthAccount(Guid.NewGuid(), user.Id, "google", "user1", "token1");
        var account2 = new OAuthAccount(Guid.NewGuid(), user.Id, "google", "user2", "token2");
        user.LinkOAuthAccount(account1);

        // Act
        var action = () => user.LinkOAuthAccount(account2);

        // Assert
        action.Should().Throw<DomainException>()
            .WithMessage("*OAuth provider 'google' is already linked*");
    }

    [Fact]
    public void LinkOAuthAccount_WithNullAccount_ThrowsArgumentNullException()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.LinkOAuthAccount(null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region UnlinkOAuthAccount Tests

    [Fact]
    public void UnlinkOAuthAccount_WithLinkedProvider_RemovesAccount()
    {
        // Arrange
        var user = CreateValidUser();
        var account = new OAuthAccount(Guid.NewGuid(), user.Id, "google", "provider-id", "token");
        user.LinkOAuthAccount(account);
        user.ClearDomainEvents();

        // Act
        user.UnlinkOAuthAccount("google");

        // Assert
        user.OAuthAccounts.Should().BeEmpty();
        user.DomainEvents.Should().HaveCount(1);
        user.DomainEvents.First().Should().BeOfType<Api.BoundedContexts.Authentication.Domain.Events.OAuthAccountUnlinkedEvent>();
    }

    [Fact]
    public void UnlinkOAuthAccount_WithUnlinkedProvider_ThrowsDomainException()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.UnlinkOAuthAccount("google");

        // Assert
        action.Should().Throw<DomainException>()
            .WithMessage("*OAuth provider 'google' is not linked*");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UnlinkOAuthAccount_WithEmptyProvider_ThrowsValidationException(string? provider)
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.UnlinkOAuthAccount(provider!);

        // Assert
        action.Should().Throw<ValidationException>();
    }

    #endregion

    #region GetOAuthAccount Tests

    [Fact]
    public void GetOAuthAccount_WithLinkedProvider_ReturnsAccount()
    {
        // Arrange
        var user = CreateValidUser();
        var account = new OAuthAccount(Guid.NewGuid(), user.Id, "discord", "provider-id", "token");
        user.LinkOAuthAccount(account);

        // Act
        var result = user.GetOAuthAccount("discord");

        // Assert
        result.Should().NotBeNull();
        result!.Provider.Should().Be("discord");
    }

    [Fact]
    public void GetOAuthAccount_WithUnlinkedProvider_ReturnsNull()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var result = user.GetOAuthAccount("github");

        // Assert
        result.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void GetOAuthAccount_WithEmptyProvider_ReturnsNull(string? provider)
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var result = user.GetOAuthAccount(provider!);

        // Assert
        result.Should().BeNull();
    }

    #endregion

    #region HasOAuthAccount Tests

    [Fact]
    public void HasOAuthAccount_WithLinkedProvider_ReturnsTrue()
    {
        // Arrange
        var user = CreateValidUser();
        user.LinkOAuthAccount(new OAuthAccount(Guid.NewGuid(), user.Id, "github", "id", "token"));

        // Act
        var result = user.HasOAuthAccount("github");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasOAuthAccount_WithUnlinkedProvider_ReturnsFalse()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var result = user.HasOAuthAccount("github");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void HasOAuthAccount_WithEmptyProvider_ReturnsFalse(string? provider)
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var result = user.HasOAuthAccount(provider!);

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region HasAnyAuthenticationMethod Tests

    [Fact]
    public void HasAnyAuthenticationMethod_WithPassword_ReturnsTrue()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var result = user.HasAnyAuthenticationMethod();

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void HasAnyAuthenticationMethod_WithOAuthOnly_ReturnsTrue()
    {
        // Arrange
        var user = CreateValidUser();
        user.LinkOAuthAccount(new OAuthAccount(Guid.NewGuid(), user.Id, "google", "id", "token"));

        // Act
        var result = user.HasAnyAuthenticationMethod();

        // Assert
        result.Should().BeTrue();
    }

    #endregion

    #region UpdateTier Tests

    [Fact]
    public void UpdateTier_WithAdminRequester_UpdatesTier()
    {
        // Arrange
        var user = CreateValidUser();
        user.ClearDomainEvents();

        // Act
        user.UpdateTier(UserTier.Premium, Role.Admin);

        // Assert
        user.Tier.Should().Be(UserTier.Premium);
        user.DomainEvents.Should().HaveCount(1);
        user.DomainEvents.First().Should().BeOfType<Api.BoundedContexts.Authentication.Domain.Events.UserTierChangedEvent>();
    }

    [Fact]
    public void UpdateTier_WithSameTier_DoesNotAddDomainEvent()
    {
        // Arrange
        var user = CreateValidUser();
        user.ClearDomainEvents();

        // Act
        user.UpdateTier(UserTier.Free, Role.Admin);

        // Assert
        user.DomainEvents.Should().BeEmpty();
    }

    [Fact]
    public void UpdateTier_WithNonAdminRequester_ThrowsDomainException()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.UpdateTier(UserTier.Premium, Role.User);

        // Assert
        action.Should().Throw<DomainException>()
            .WithMessage("*Only administrators can change user tiers*");
    }

    [Fact]
    public void UpdateTier_WithNullTier_ThrowsArgumentNullException()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.UpdateTier(null!, Role.Admin);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void UpdateTier_WithNullRequesterRole_ThrowsArgumentNullException()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.UpdateTier(UserTier.Premium, null!);

        // Assert
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region UpdatePreferences Tests

    [Fact]
    public void UpdatePreferences_WithValidData_UpdatesAllPreferences()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        user.UpdatePreferences("it", "dark", false, 180);

        // Assert
        user.Language.Should().Be("it");
        user.Theme.Should().Be("dark");
        user.EmailNotifications.Should().BeFalse();
        user.DataRetentionDays.Should().Be(180);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdatePreferences_WithEmptyLanguage_ThrowsValidationException(string? language)
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.UpdatePreferences(language!, "dark", true, 90);

        // Assert
        action.Should().Throw<ValidationException>();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdatePreferences_WithEmptyTheme_ThrowsValidationException(string? theme)
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.UpdatePreferences("en", theme!, true, 90);

        // Assert
        action.Should().Throw<ValidationException>();
    }

    [Fact]
    public void UpdatePreferences_WithInvalidTheme_ThrowsValidationException()
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.UpdatePreferences("en", "invalid-theme", true, 90);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Theme must be one of: light, dark, system*");
    }

    [Theory]
    [InlineData(0)]
    [InlineData(-1)]
    [InlineData(-100)]
    public void UpdatePreferences_WithInvalidDataRetentionDays_ThrowsValidationException(int days)
    {
        // Arrange
        var user = CreateValidUser();

        // Act
        var action = () => user.UpdatePreferences("en", "dark", true, days);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Data retention days must be positive*");
    }

    #endregion

    #region Restore2FAState Tests

    [Fact]
    public void Restore2FAState_WithValidData_RestoresState()
    {
        // Arrange
        var user = CreateValidUser();
        var totpSecret = TotpSecret.FromEncrypted("encrypted-secret");
        var enabledAt = DateTime.UtcNow.AddDays(-7);
        var backupCodes = new List<BackupCode>
        {
            BackupCode.FromHashed("hash1"),
            BackupCode.FromHashed("hash2", isUsed: true, usedAt: DateTime.UtcNow.AddDays(-1))
        };

        // Act
        user.Restore2FAState(totpSecret, true, enabledAt, backupCodes);

        // Assert
        user.TotpSecret.Should().Be(totpSecret);
        user.IsTwoFactorEnabled.Should().BeTrue();
        user.TwoFactorEnabledAt.Should().Be(enabledAt);
        user.BackupCodes.Should().HaveCount(2);
    }

    #endregion

    #region RestoreOAuthAccounts Tests

    [Fact]
    public void RestoreOAuthAccounts_WithAccounts_RestoresAccounts()
    {
        // Arrange
        var user = CreateValidUser();
        var accounts = new List<OAuthAccount>
        {
            new OAuthAccount(Guid.NewGuid(), user.Id, "google", "id1", "token1"),
            new OAuthAccount(Guid.NewGuid(), user.Id, "discord", "id2", "token2")
        };

        // Act
        user.RestoreOAuthAccounts(accounts);

        // Assert
        user.OAuthAccounts.Should().HaveCount(2);
    }

    #endregion

    #region Helper Methods

    private static User CreateValidUser(string password = "SecurePassword123!")
    {
        return new User(
            Guid.NewGuid(),
            new Email("test@example.com"),
            "Test User",
            PasswordHash.Create(password),
            Role.User);
    }

    private static User CreateAdminUser()
    {
        return new User(
            Guid.NewGuid(),
            new Email("admin@example.com"),
            "Admin User",
            PasswordHash.Create("AdminPassword123!"),
            Role.Admin);
    }

    #endregion
}
