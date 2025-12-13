using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
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
        Assert.Equal(id, user.Id);
        Assert.Equal(email, user.Email);
        Assert.Equal(role, user.Role);
        Assert.False(user.IsTwoFactorEnabled);
    }

    [Fact]
    public void User_VerifyPassword_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "SecurePassword123!";
        var user = CreateTestUser(password);

        // Act & Assert
        Assert.True(user.VerifyPassword(password));
    }

    [Fact]
    public void User_VerifyPassword_WithWrongPassword_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser("CorrectPassword123!");

        // Act & Assert
        Assert.False(user.VerifyPassword("WrongPassword!"));
    }

    [Fact]
    public void User_EnableTwoFactor_WhenNotEnabled_EnablesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        user.Enable2FA(TotpSecret.FromEncrypted("encrypted_secret_base64"));

        // Assert
        Assert.True(user.IsTwoFactorEnabled);
        Assert.NotNull(user.TotpSecret?.EncryptedValue);
        Assert.NotNull(user.TwoFactorEnabledAt);
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
        Assert.False(user.IsTwoFactorEnabled);
        Assert.Null(user.TotpSecret?.EncryptedValue);
    }

    [Fact]
    public void User_AssignRole_ByAdmin_UpdatesRole()
    {
        // Arrange
        var user = CreateTestUser(role: Role.User);

        // Act
        user.AssignRole(Role.Editor, Role.Admin);

        // Assert
        Assert.Equal(Role.Editor, user.Role);
    }

    [Fact]
    public void User_AssignRole_ByNonAdmin_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser(role: Role.User);

        // Act & Assert
        Assert.Throws<DomainException>(() =>
            user.AssignRole(Role.Admin, Role.Editor));
    }

    [Fact]
    public void User_AssignRole_AdminToAdmin_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser(role: Role.Admin);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            user.AssignRole(Role.Admin, Role.Admin));
        Assert.Contains("Cannot modify admin role through self-service", exception.Message);
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
        Assert.True(user.VerifyPassword("NewPassword456!"));
        Assert.False(user.VerifyPassword(currentPassword));
    }

    [Fact]
    public void User_ChangePassword_WithIncorrectCurrentPassword_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser("CorrectPassword123!");
        var newPasswordHash = PasswordHash.Create("NewPassword456!");

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            user.ChangePassword("WrongPassword!", newPasswordHash));
        Assert.Contains("Current password is incorrect", exception.Message);
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
        Assert.True(user.VerifyPassword("NewPassword456!"));
        Assert.False(user.VerifyPassword("OldPassword123!"));
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
        Assert.Equal(newEmail, user.Email);
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
        Assert.Equal(originalEmail, user.Email);
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
        Assert.Equal(newName, user.DisplayName);
    }

    [Fact]
    public void User_UpdateDisplayName_WithNull_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            user.UpdateDisplayName(null!));
    }

    [Fact]
    public void User_UpdateDisplayName_WithEmpty_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            user.UpdateDisplayName(string.Empty));
    }

    [Fact]
    public void User_UpdateDisplayName_WithWhitespace_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            user.UpdateDisplayName("   "));
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
        Assert.Equal(originalName, user.DisplayName);
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
        Assert.Equal(Role.Editor, user.Role);
    }

    [Fact]
    public void User_UpdateRole_WithSameRole_NoChange()
    {
        // Arrange
        var user = CreateTestUser(role: Role.User);

        // Act
        user.UpdateRole(Role.User);

        // Assert
        Assert.Equal(Role.User, user.Role);
    }

    // RequiresTwoFactor Tests
    [Fact]
    public void User_RequiresTwoFactor_WhenEnabled_ReturnsTrue()
    {
        // Arrange
        var user = CreateTestUser();
        user.Enable2FA(TotpSecret.FromEncrypted("encrypted_secret"));

        // Act & Assert
        Assert.True(user.RequiresTwoFactor());
    }

    [Fact]
    public void User_RequiresTwoFactor_WhenNotEnabled_ReturnsFalse()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        Assert.False(user.RequiresTwoFactor());
    }

    // EnableTwoFactor Additional Tests
    [Fact]
    public void User_EnableTwoFactor_WithNullSecret_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert - Domain throws ArgumentNullException for null aggregates
        Assert.Throws<ArgumentNullException>(() =>
            user.Enable2FA(null!));
    }

    [Fact]
    public void User_EnableTwoFactor_WithEmptySecret_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            user.Enable2FA(TotpSecret.FromEncrypted(string.Empty)));
    }

    [Fact]
    public void User_EnableTwoFactor_WithWhitespaceSecret_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        Assert.Throws<ValidationException>(() =>
            user.Enable2FA(TotpSecret.FromEncrypted("   ")));
    }

    [Fact]
    public void User_EnableTwoFactor_WhenAlreadyEnabled_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser();
        user.Enable2FA(TotpSecret.FromEncrypted("first_secret"));

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            user.Enable2FA(TotpSecret.FromEncrypted("second_secret")));
        Assert.Contains("Two-factor authentication is already enabled", exception.Message);
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
        Assert.NotNull(user.TwoFactorEnabledAt);
        Assert.True(user.TwoFactorEnabledAt >= beforeEnable);
        Assert.True(user.TwoFactorEnabledAt <= DateTime.UtcNow.AddSeconds(1));
    }

    // DisableTwoFactor Additional Tests
    [Fact]
    public void User_DisableTwoFactor_WhenNotEnabled_ThrowsException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            user.Disable2FA());
        Assert.Contains("Two-factor authentication is not enabled", exception.Message);
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
        Assert.Null(user.TwoFactorEnabledAt);
    }

    // Constructor Validation Tests
    [Fact]
    public void User_Constructor_WithNullEmail_ThrowsException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var passwordHash = PasswordHash.Create("Password123!");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new User(id, null!, "Test User", passwordHash, Role.User));
    }

    [Fact]
    public void User_Constructor_WithNullDisplayName_ThrowsException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = new Email("test@example.com");
        var passwordHash = PasswordHash.Create("Password123!");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new User(id, email, null!, passwordHash, Role.User));
    }

    [Fact]
    public void User_Constructor_WithNullPasswordHash_ThrowsException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = new Email("test@example.com");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new User(id, email, "Test User", null!, Role.User));
    }

    [Fact]
    public void User_Constructor_WithNullRole_ThrowsException()
    {
        // Arrange
        var id = Guid.NewGuid();
        var email = new Email("test@example.com");
        var passwordHash = PasswordHash.Create("Password123!");

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            new User(id, email, "Test User", passwordHash, null!));
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
        Assert.True(user.CreatedAt >= beforeCreate);
        Assert.True(user.CreatedAt <= DateTime.UtcNow.AddSeconds(1));
    }

    [Fact]
    public void User_Constructor_DefaultsTwoFactorToDisabled()
    {
        // Arrange & Act
        var user = CreateTestUser();

        // Assert
        Assert.False(user.IsTwoFactorEnabled);
        Assert.Null(user.TotpSecret?.EncryptedValue);
        Assert.Null(user.TwoFactorEnabledAt);
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
        Assert.True(user.VerifyPassword("Password3!"));
        Assert.False(user.VerifyPassword("Password1!"));
        Assert.False(user.VerifyPassword("Password2!"));
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
        Assert.True(user.IsTwoFactorEnabled);
        Assert.Equal("secret2", user.TotpSecret?.EncryptedValue);
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
        Assert.Equal(Role.Admin, user.Role);
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
        Assert.True(user.IsTwoFactorEnabled);
        Assert.NotNull(user.TotpSecret);
        Assert.Equal(totpSecret, user.TotpSecret);
        Assert.Equal(3, user.BackupCodes.Count);
        Assert.NotNull(user.TwoFactorEnabledAt);
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
        Assert.True(user.IsTwoFactorEnabled);
        Assert.NotNull(user.TotpSecret);
        Assert.Equal(0, user.GetUnusedBackupCodesCount());
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
        Assert.True(user.IsTwoFactorEnabled);
        Assert.Equal(0, user.GetUnusedBackupCodesCount());
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
        var exception = Assert.Throws<DomainException>(() => user.Enable2FA(totpSecret, backupCodes));
        Assert.Contains("used backup codes", exception.Message);
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
        Assert.True(code.IsUsed);
        Assert.Equal(usedAt, code.UsedAt);
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
        var exception = Assert.Throws<DomainException>(() =>
            user.UseBackupCode("invalid_hash", DateTime.UtcNow));
        Assert.Contains("not found", exception.Message);
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
        Assert.Equal(2, count);
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
        Assert.True(result);
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
        Assert.False(result);
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
        Assert.False(result);
    }
    [Fact]
    public void User_UpdateTier_ByAdmin_UpdatesTierSuccessfully()
    {
        // Arrange
        var user = CreateTestUser(tier: UserTier.Free);

        // Act
        user.UpdateTier(UserTier.Premium, Role.Admin);

        // Assert
        Assert.Equal(UserTier.Premium, user.Tier);
    }

    [Fact]
    public void User_UpdateTier_ByNonAdmin_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser(tier: UserTier.Free);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            user.UpdateTier(UserTier.Premium, Role.User));
        Assert.Contains("Only administrators can change user tiers", exception.Message);
    }

    [Fact]
    public void User_UpdateTier_ByEditor_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser(tier: UserTier.Free);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            user.UpdateTier(UserTier.Normal, Role.Editor));
        Assert.Contains("Only administrators can change user tiers", exception.Message);
    }

    [Fact]
    public void User_UpdateTier_WithNullTier_ThrowsArgumentNullException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            user.UpdateTier(null!, Role.Admin));
    }

    [Fact]
    public void User_UpdateTier_WithNullRequesterRole_ThrowsArgumentNullException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() =>
            user.UpdateTier(UserTier.Premium, null!));
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
        Assert.Equal(originalTier, user.Tier);
    }

    [Fact]
    public void User_UpdateTier_FromFreeToNormal_UpdatesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser(tier: UserTier.Free);

        // Act
        user.UpdateTier(UserTier.Normal, Role.Admin);

        // Assert
        Assert.Equal(UserTier.Normal, user.Tier);
    }

    [Fact]
    public void User_UpdateTier_FromPremiumToFree_Downgrade_UpdatesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser(tier: UserTier.Premium);

        // Act
        user.UpdateTier(UserTier.Free, Role.Admin);

        // Assert
        Assert.Equal(UserTier.Free, user.Tier);
    }

    [Fact]
    public void User_Constructor_DefaultsToFreeTier()
    {
        // Arrange & Act
        var user = CreateTestUser();

        // Assert
        Assert.Equal(UserTier.Free, user.Tier);
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
        Assert.Equal(UserTier.Premium, user.Tier);
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