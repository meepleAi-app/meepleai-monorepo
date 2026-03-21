using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Domain;

/// <summary>
/// Domain tests for User aggregate root.
/// DDD-PHASE2: Tests aligned with Guid schema.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UserDomainTests
{
    [Fact]
    public void User_Create_WithValidData_Succeeds()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = new Email("test@example.com");
        var passwordHash = PasswordHash.Create("SecurePassword123!");
        var role = Role.User;

        // Act
        var user = new User(id, email, "Test User", passwordHash, role);

        // Assert
        user.Id.Should().Be(id);
        user.Email.Should().Be(email);
        user.Role.Should().Be(role);
        user.IsTwoFactorEnabled.Should().BeFalse();
    }

    [Fact]
    public void User_VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateTestUser(password);

        // Act & Assert
        user.VerifyPassword(password).Should().BeTrue();
    }

    [Fact]
    public void User_VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser("CorrectPassword123!");

        // Act & Assert
        user.VerifyPassword("WrongPassword!").Should().BeFalse();
    }

    [Fact]
    public void User_EnableTwoFactor_WhenNotEnabled_EnablesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        user.Enable2FA(TotpSecret.FromEncrypted("encrypted_secret_base64"));

        // Assert
        user.IsTwoFactorEnabled.Should().BeTrue();
        user.TotpSecret?.EncryptedValue.Should().NotBeNull();
        user.TwoFactorEnabledAt.Should().NotBeNull();
    }

    [Fact]
    public void User_DisableTwoFactor_WhenEnabled_DisablesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        user.Enable2FA(TotpSecret.FromEncrypted("secret"));

        // Act
        user.Disable2FA();

        // Assert
        user.IsTwoFactorEnabled.Should().BeFalse();
        user.TotpSecret?.EncryptedValue.Should().BeNull();
    }

    [Fact]
    public void User_AssignRole_BySuperAdmin_UpdatesRole()
    {
        // Arrange
        var user = CreateTestUser(role: Role.User);

        // Act
        user.AssignRole(Role.Editor, Role.SuperAdmin);

        // Assert
        user.Role.Should().Be(Role.Editor);
    }

    [Fact]
    public void User_AssignRole_ByNonAdmin_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser(role: Role.User);

        // Act & Assert
        var act = () =>
            user.AssignRole(Role.Admin, Role.Editor);
        act.Should().Throw<DomainException>();
    }

    [Fact]
    public void User_AssignRole_ByAdmin_ThrowsException()
    {
        // Arrange — Only SuperAdmin can assign roles; Admin is not sufficient
        var user = CreateTestUser(role: Role.Admin);

        // Act & Assert — Only SuperAdmin can assign roles, so Admin requester is rejected
        var act = () =>
            user.AssignRole(Role.Admin, Role.Admin);
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().Contain("Only the SuperAdmin can assign roles");
    }

    // ChangePassword Tests
    [Fact]
    public void User_ChangePassword_WithCorrectCurrentPassword_Succeeds()
    {
        // Arrange
        var currentPassword = "OldPassword123!";
        var user = CreateTestUser(currentPassword);
        var newPasswordHash = PasswordHash.Create("NewPassword456!");

        // Act
        user.ChangePassword(currentPassword, newPasswordHash);

        // Assert
        user.VerifyPassword("NewPassword456!").Should().BeTrue();
        user.VerifyPassword(currentPassword).Should().BeFalse();
    }

    [Fact]
    public void User_ChangePassword_WithIncorrectCurrentPassword_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser("CorrectPassword123!");
        var newPasswordHash = PasswordHash.Create("NewPassword456!");

        // Act & Assert
        var act = () =>
            user.ChangePassword("WrongPassword!", newPasswordHash);
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().Contain("Current password is incorrect");
    }

    // UpdatePassword Tests (Admin operation)
    [Fact]
    public void User_UpdatePassword_AsAdmin_UpdatesWithoutVerification()
    {
        // Arrange
        var user = CreateTestUser("OldPassword123!");
        var newPasswordHash = PasswordHash.Create("NewPassword456!");

        // Act
        user.UpdatePassword(newPasswordHash);

        // Assert
        user.VerifyPassword("NewPassword456!").Should().BeTrue();
        user.VerifyPassword("OldPassword123!").Should().BeFalse();
    }

    // UpdateEmail Tests
    [Fact]
    public void User_UpdateEmail_WithNewEmail_UpdatesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        var newEmail = new Email("newemail@example.com");

        // Act
        user.UpdateEmail(newEmail);

        // Assert
        user.Email.Should().Be(newEmail);
    }

    [Fact]
    public void User_UpdateEmail_WithSameEmail_NoChange()
    {
        // Arrange
        var email = new Email("test@example.com");
        var user = CreateTestUser();
        var originalEmail = user.Email;

        // Act
        user.UpdateEmail(email);

        // Assert
        user.Email.Should().Be(originalEmail);
    }

    // UpdateDisplayName Tests
    [Fact]
    public void User_UpdateDisplayName_WithValidName_UpdatesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        var newName = "New Display Name";

        // Act
        user.UpdateDisplayName(newName);

        // Assert
        user.DisplayName.Should().Be(newName);
    }

    [Fact]
    public void User_UpdateDisplayName_WithNull_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var act = () =>
            user.UpdateDisplayName(null!);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void User_UpdateDisplayName_WithEmpty_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var act = () =>
            user.UpdateDisplayName(string.Empty);
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void User_UpdateDisplayName_WithWhitespace_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var act = () =>
            user.UpdateDisplayName("   ");
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void User_UpdateDisplayName_WithSameName_NoChange()
    {
        // Arrange
        var user = CreateTestUser();
        var originalName = user.DisplayName;

        // Act
        user.UpdateDisplayName("Test User");

        // Assert
        user.DisplayName.Should().Be(originalName);
    }

    // UpdateRole Tests (Admin operation)
    [Fact]
    public void User_UpdateRole_WithNewRole_UpdatesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser(role: Role.User);

        // Act
        user.UpdateRole(Role.Editor);

        // Assert
        user.Role.Should().Be(Role.Editor);
    }

    [Fact]
    public void User_UpdateRole_WithSameRole_NoChange()
    {
        // Arrange
        var user = CreateTestUser(role: Role.User);

        // Act
        user.UpdateRole(Role.User);

        // Assert
        user.Role.Should().Be(Role.User);
    }

    // RequiresTwoFactor Tests
    [Fact]
    public void User_RequiresTwoFactor_WhenEnabled_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser();
        user.Enable2FA(TotpSecret.FromEncrypted("encrypted_secret"));

        // Act & Assert
        user.RequiresTwoFactor().Should().BeTrue();
    }

    [Fact]
    public void User_RequiresTwoFactor_WhenNotEnabled_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        user.RequiresTwoFactor().Should().BeFalse();
    }

    // EnableTwoFactor Additional Tests
    [Fact]
    public void User_EnableTwoFactor_WithNullSecret_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert - Domain throws ArgumentNullException for null aggregates
        var act = () =>
            user.Enable2FA(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void User_EnableTwoFactor_WithEmptySecret_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var act = () =>
            user.Enable2FA(TotpSecret.FromEncrypted(string.Empty));
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void User_EnableTwoFactor_WithWhitespaceSecret_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var act = () =>
            user.Enable2FA(TotpSecret.FromEncrypted("   "));
        act.Should().Throw<ValidationException>();
    }

    [Fact]
    public void User_EnableTwoFactor_WhenAlreadyEnabled_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser();
        user.Enable2FA(TotpSecret.FromEncrypted("first_secret"));

        // Act & Assert
        var act = () =>
            user.Enable2FA(TotpSecret.FromEncrypted("second_secret"));
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().Contain("Two-factor authentication is already enabled");
    }

    [Fact]
    public void User_EnableTwoFactor_SetsTwoFactorEnabledAt()
    {
        // Arrange
        var user = CreateTestUser();
        var beforeEnable = DateTime.UtcNow;

        // Act
        user.Enable2FA(TotpSecret.FromEncrypted("encrypted_secret"));

        // Assert
        user.TwoFactorEnabledAt.Should().NotBeNull();
        (user.TwoFactorEnabledAt >= beforeEnable).Should().BeTrue();
        (user.TwoFactorEnabledAt <= DateTime.UtcNow.AddSeconds(1)).Should().BeTrue();
    }

    // DisableTwoFactor Additional Tests
    [Fact]
    public void User_DisableTwoFactor_WhenNotEnabled_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var act = () =>
            user.Disable2FA();
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().Contain("Two-factor authentication is not enabled");
    }

    [Fact]
    public void User_DisableTwoFactor_ClearsTwoFactorEnabledAt()
    {
        // Arrange
        var user = CreateTestUser();
        user.Enable2FA(TotpSecret.FromEncrypted("secret"));

        // Act
        user.Disable2FA();

        // Assert
        user.TwoFactorEnabledAt.Should().BeNull();
    }

    // Constructor Validation Tests
    [Fact]
    public void User_Constructor_WithNullEmail_ThrowsException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var passwordHash = PasswordHash.Create("Password123!");

        // Act & Assert
        var act = () =>
            new User(id, null!, "Test User", passwordHash, Role.User);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void User_Constructor_WithNullDisplayName_ThrowsException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = new Email("test@example.com");
        var passwordHash = PasswordHash.Create("Password123!");

        // Act & Assert
        var act = () =>
            new User(id, email, null!, passwordHash, Role.User);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void User_Constructor_WithNullPasswordHash_ThrowsException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = new Email("test@example.com");

        // Act & Assert
        var act = () =>
            new User(id, email, "Test User", null!, Role.User);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void User_Constructor_WithNullRole_ThrowsException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = new Email("test@example.com");
        var passwordHash = PasswordHash.Create("Password123!");

        // Act & Assert
        var act = () =>
            new User(id, email, "Test User", passwordHash, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void User_Constructor_SetsCreatedAt()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = new Email("test@example.com");
        var passwordHash = PasswordHash.Create("Password123!");
        var beforeCreate = DateTime.UtcNow;

        // Act
        var user = new User(id, email, "Test User", passwordHash, Role.User);

        // Assert
        (user.CreatedAt >= beforeCreate).Should().BeTrue();
        (user.CreatedAt <= DateTime.UtcNow.AddSeconds(1)).Should().BeTrue();
    }

    [Fact]
    public void User_Constructor_DefaultsTwoFactorToDisabled()
    {
        // Arrange & Act
        var user = CreateTestUser();

        // Assert
        user.IsTwoFactorEnabled.Should().BeFalse();
        user.TotpSecret?.EncryptedValue.Should().BeNull();
        user.TwoFactorEnabledAt.Should().BeNull();
    }

    // Edge Cases and Multiple Operation Tests
    [Fact]
    public void User_MultiplePasswordChanges_WorksCorrectly()
    {
        // Arrange
        var user = CreateTestUser("Password1!");

        // Act
        user.ChangePassword("Password1!", PasswordHash.Create("Password2!"));
        user.ChangePassword("Password2!", PasswordHash.Create("Password3!"));

        // Assert
        user.VerifyPassword("Password3!").Should().BeTrue();
        user.VerifyPassword("Password1!").Should().BeFalse();
        user.VerifyPassword("Password2!").Should().BeFalse();
    }

    [Fact]
    public void User_EnableDisableEnable2FA_WorksCorrectly()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        user.Enable2FA(TotpSecret.FromEncrypted("secret1"));
        user.Disable2FA();
        user.Enable2FA(TotpSecret.FromEncrypted("secret2"));

        // Assert
        user.IsTwoFactorEnabled.Should().BeTrue();
        user.TotpSecret?.EncryptedValue.Should().Be("secret2");
    }

    [Fact]
    public void User_RoleProgression_UserToEditorToAdmin()
    {
        // Arrange
        var user = CreateTestUser(role: Role.User);

        // Act
        user.UpdateRole(Role.Editor);
        user.UpdateRole(Role.Admin);

        // Assert
        user.Role.Should().Be(Role.Admin);
    }
    [Fact]
    public void User_Enable2FA_WithBackupCodes_StoresCodesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        var totpSecret = TotpSecret.FromEncrypted("encrypted_secret_base64");
        var backupCodes = new List<BackupCode>
        {
            BackupCode.FromHashed("hash1"),
            BackupCode.FromHashed("hash2"),
            BackupCode.FromHashed("hash3")
        };

        // Act
        user.Enable2FA(totpSecret, backupCodes);

        // Assert
        user.IsTwoFactorEnabled.Should().BeTrue();
        user.TotpSecret.Should().NotBeNull();
        user.TotpSecret.Should().Be(totpSecret);
        user.BackupCodes.Count.Should().Be(3);
        user.TwoFactorEnabledAt.Should().NotBeNull();
    }

    [Fact]
    public void User_Enable2FA_WithNullBackupCodes_AllowedForTestingScenarios()
    {
        // Arrange
        var user = CreateTestUser();
        var totpSecret = TotpSecret.FromEncrypted("encrypted_secret_base64");

        // Act - null is allowed (optional parameter for testing)
        user.Enable2FA(totpSecret, null);

        // Assert
        user.IsTwoFactorEnabled.Should().BeTrue();
        user.TotpSecret.Should().NotBeNull();
        user.GetUnusedBackupCodesCount().Should().Be(0);
    }

    [Fact]
    public void User_Enable2FA_WithEmptyBackupCodes_AllowedForTestingScenarios()
    {
        // Arrange
        var user = CreateTestUser();
        var totpSecret = TotpSecret.FromEncrypted("encrypted_secret_base64");
        var emptyBackupCodes = new List<BackupCode>();

        // Act - empty list is allowed (optional for testing)
        user.Enable2FA(totpSecret, emptyBackupCodes);

        // Assert
        user.IsTwoFactorEnabled.Should().BeTrue();
        user.GetUnusedBackupCodesCount().Should().Be(0);
    }

    [Fact]
    public void User_Enable2FA_WithUsedBackupCodes_ShouldThrowDomainException()
    {
        // Arrange
        var user = CreateTestUser();
        var totpSecret = TotpSecret.FromEncrypted("encrypted_secret_base64");
        var usedCode = BackupCode.FromHashed("hash1", isUsed: true, usedAt: DateTime.UtcNow);
        var backupCodes = new List<BackupCode> { usedCode };

        // Act & Assert
        var act = () => user.Enable2FA(totpSecret, backupCodes);
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().Contain("used backup codes");
    }

    [Fact]
    public void User_UseBackupCode_WithValidCode_ShouldMarkAsUsed()
    {
        // Arrange
        var user = CreateTestUser();
        var totpSecret = TotpSecret.FromEncrypted("encrypted_secret_base64");
        var codeHash = "hash1";
        var backupCodes = new List<BackupCode> { BackupCode.FromHashed(codeHash) };
        user.Enable2FA(totpSecret, backupCodes);
        var usedAt = DateTime.UtcNow;

        // Act
        user.UseBackupCode(codeHash, usedAt);

        // Assert
        var code = user.BackupCodes.ElementAt(0);
        code.IsUsed.Should().BeTrue();
        code.UsedAt.Should().Be(usedAt);
    }

    [Fact]
    public void User_UseBackupCode_WithInvalidCode_ShouldThrowDomainException()
    {
        // Arrange
        var user = CreateTestUser();
        var totpSecret = TotpSecret.FromEncrypted("encrypted_secret_base64");
        var backupCodes = new List<BackupCode> { BackupCode.FromHashed("hash1") };
        user.Enable2FA(totpSecret, backupCodes);

        // Act & Assert
        var act = () =>
            user.UseBackupCode("invalid_hash", DateTime.UtcNow);
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().Contain("not found");
    }

    [Fact]
    public void User_GetUnusedBackupCodesCount_ShouldReturnCorrectCount()
    {
        // Arrange
        var user = CreateTestUser();
        var totpSecret = TotpSecret.FromEncrypted("encrypted_secret_base64");
        var backupCodes = new List<BackupCode>
        {
            BackupCode.FromHashed("hash1"),
            BackupCode.FromHashed("hash2"),
            BackupCode.FromHashed("hash3")
        };
        user.Enable2FA(totpSecret, backupCodes);

        // Act - Use one code
        user.UseBackupCode("hash1", DateTime.UtcNow);
        var count = user.GetUnusedBackupCodesCount();

        // Assert
        count.Should().Be(2);
    }

    [Fact]
    public void User_HasUnusedBackupCode_WithValidUnusedCode_ShouldReturnTrue()
    {
        // Arrange
        var user = CreateTestUser();
        var totpSecret = TotpSecret.FromEncrypted("encrypted_secret_base64");
        var codeHash = "hash1";
        var backupCodes = new List<BackupCode> { BackupCode.FromHashed(codeHash) };
        user.Enable2FA(totpSecret, backupCodes);

        // Act
        var result = user.HasUnusedBackupCode(codeHash);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void User_HasUnusedBackupCode_WithUsedCode_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateTestUser();
        var totpSecret = TotpSecret.FromEncrypted("encrypted_secret_base64");
        var codeHash = "hash1";
        var backupCodes = new List<BackupCode> { BackupCode.FromHashed(codeHash) };
        user.Enable2FA(totpSecret, backupCodes);
        user.UseBackupCode(codeHash, DateTime.UtcNow);

        // Act
        var result = user.HasUnusedBackupCode(codeHash);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void User_HasUnusedBackupCode_WithInvalidCode_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateTestUser();
        var totpSecret = TotpSecret.FromEncrypted("encrypted_secret_base64");
        var backupCodes = new List<BackupCode> { BackupCode.FromHashed("hash1") };
        user.Enable2FA(totpSecret, backupCodes);

        // Act
        var result = user.HasUnusedBackupCode("invalid_hash");

        // Assert
        result.Should().BeFalse();
    }
    [Fact]
    public void User_UpdateTier_ByAdmin_UpdatesTierSuccessfully()
    {
        // Arrange
        var user = CreateTestUser(tier: UserTier.Free);

        // Act
        user.UpdateTier(UserTier.Premium, Role.Admin);

        // Assert
        user.Tier.Should().Be(UserTier.Premium);
    }

    [Fact]
    public void User_UpdateTier_ByNonAdmin_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser(tier: UserTier.Free);

        // Act & Assert
        var act = () =>
            user.UpdateTier(UserTier.Premium, Role.User);
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().Contain("Only administrators can change user tiers");
    }

    [Fact]
    public void User_UpdateTier_ByEditor_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser(tier: UserTier.Free);

        // Act & Assert
        var act = () =>
            user.UpdateTier(UserTier.Normal, Role.Editor);
        var exception = act.Should().Throw<DomainException>().Which;
        exception.Message.Should().Contain("Only administrators can change user tiers");
    }

    [Fact]
    public void User_UpdateTier_WithNullTier_ThrowsArgumentNullException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var act = () =>
            user.UpdateTier(null!, Role.Admin);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void User_UpdateTier_WithNullRequesterRole_ThrowsArgumentNullException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var act = () =>
            user.UpdateTier(UserTier.Premium, null!);
        act.Should().Throw<ArgumentNullException>();
    }

    [Fact]
    public void User_UpdateTier_WithSameTier_NoChange()
    {
        // Arrange
        var user = CreateTestUser(tier: UserTier.Normal);
        var originalTier = user.Tier;

        // Act
        user.UpdateTier(UserTier.Normal, Role.Admin);

        // Assert
        user.Tier.Should().Be(originalTier);
    }

    [Fact]
    public void User_UpdateTier_FromFreeToNormal_UpdatesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser(tier: UserTier.Free);

        // Act
        user.UpdateTier(UserTier.Normal, Role.Admin);

        // Assert
        user.Tier.Should().Be(UserTier.Normal);
    }

    [Fact]
    public void User_UpdateTier_FromPremiumToFree_Downgrade_UpdatesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser(tier: UserTier.Premium);

        // Act
        user.UpdateTier(UserTier.Free, Role.Admin);

        // Assert
        user.Tier.Should().Be(UserTier.Free);
    }

    [Fact]
    public void User_Constructor_DefaultsToFreeTier()
    {
        // Arrange & Act
        var user = CreateTestUser();

        // Assert
        user.Tier.Should().Be(UserTier.Free);
    }

    [Fact]
    public void User_Constructor_WithSpecificTier_SetsTierCorrectly()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = new Email("test@example.com");
        var passwordHash = PasswordHash.Create("Password123!");

        // Act
        var user = new User(id, email, "Test User", passwordHash, Role.User, UserTier.Premium);

        // Assert
        user.Tier.Should().Be(UserTier.Premium);
    }
    private static User CreateTestUser(
        string password = "TestPassword123!",
        Role? role = null,
        UserTier? tier = null)
    {
        var id = Guid.NewGuid();
        var email = new Email("test@example.com");
        var passwordHash = PasswordHash.Create(password);
        var userRole = role ?? Role.User;
        var userTier = tier ?? UserTier.Free;

        return new User(id, email, "Test User", passwordHash, userRole, userTier);
    }
}
