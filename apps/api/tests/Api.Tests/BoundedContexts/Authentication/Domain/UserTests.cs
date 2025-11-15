using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain;

/// <summary>
/// Domain tests for User aggregate.
/// Tests business rules and validation logic.
/// </summary>
public class UserTests
{
    [Fact]
    public void UpdateDisplayName_ValidName_UpdatesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        var newDisplayName = "NewName";

        // Act
        user.UpdateDisplayName(newDisplayName);

        // Assert
        Assert.Equal(newDisplayName, user.DisplayName);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void UpdateDisplayName_EmptyName_ThrowsValidationException(string? invalidName)
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        Assert.Throws<ValidationException>(() => user.UpdateDisplayName(invalidName!));
    }

    [Fact]
    public void UpdateDisplayName_SameName_DoesNothing()
    {
        // Arrange
        var user = CreateTestUser();
        var originalName = user.DisplayName;

        // Act
        user.UpdateDisplayName(originalName);

        // Assert
        Assert.Equal(originalName, user.DisplayName);
    }

    [Fact]
    public void UpdateEmail_ValidEmail_UpdatesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        var newEmail = new Email("newemail@test.com");

        // Act
        user.UpdateEmail(newEmail);

        // Assert
        Assert.Equal(newEmail, user.Email);
    }

    [Fact]
    public void UpdateEmail_SameEmail_DoesNothing()
    {
        // Arrange
        var user = CreateTestUser();
        var originalEmail = user.Email;

        // Act
        user.UpdateEmail(originalEmail);

        // Assert
        Assert.Equal(originalEmail, user.Email);
    }

    [Fact]
    public void ChangePassword_ValidCurrentPassword_ChangesSuccessfully()
    {
        // Arrange
        var user = CreateTestUser();
        var currentPassword = "OldPassword123!";
        var newPasswordHash = PasswordHash.Create("NewPassword456!");

        // Act
        user.ChangePassword(currentPassword, newPasswordHash);

        // Assert
        Assert.True(user.VerifyPassword("NewPassword456!"));
        Assert.False(user.VerifyPassword(currentPassword));
    }

    [Fact]
    public void ChangePassword_IncorrectCurrentPassword_ThrowsDomainException()
    {
        // Arrange
        var user = CreateTestUser();
        var wrongPassword = "WrongPassword123!";
        var newPasswordHash = PasswordHash.Create("NewPassword456!");

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            user.ChangePassword(wrongPassword, newPasswordHash));
        Assert.Contains("Current password is incorrect", exception.Message);
    }

    #region 2FA Tests

    [Fact]
    public void Enable2FA_WithValidData_ShouldEnableTwoFactor()
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
    public void Enable2FA_WithNullSecret_ShouldThrowArgumentNullException()
    {
        // Arrange
        var user = CreateTestUser();
        var backupCodes = new List<BackupCode> { BackupCode.FromHashed("hash1") };

        // Act & Assert
        Assert.Throws<ArgumentNullException>(() => user.Enable2FA(null!, backupCodes));
    }

    [Fact]
    public void Enable2FA_WithNullBackupCodes_AllowedForTestingScenarios()
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
    public void Enable2FA_WithEmptyBackupCodes_AllowedForTestingScenarios()
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
    public void Enable2FA_WithUsedBackupCodes_ShouldThrowDomainException()
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
    public void Enable2FA_WhenAlreadyEnabled_ShouldThrowDomainException()
    {
        // Arrange
        var user = CreateTestUser();
        var totpSecret = TotpSecret.FromEncrypted("encrypted_secret_base64");
        var backupCodes = new List<BackupCode> { BackupCode.FromHashed("hash1") };
        user.Enable2FA(totpSecret, backupCodes);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            user.Enable2FA(totpSecret, backupCodes));
        Assert.Contains("already enabled", exception.Message);
    }

    [Fact]
    public void Disable2FA_WhenEnabled_ShouldDisableTwoFactor()
    {
        // Arrange
        var user = CreateTestUser();
        var totpSecret = TotpSecret.FromEncrypted("encrypted_secret_base64");
        var backupCodes = new List<BackupCode> { BackupCode.FromHashed("hash1") };
        user.Enable2FA(totpSecret, backupCodes);

        // Act
        user.Disable2FA();

        // Assert
        Assert.False(user.IsTwoFactorEnabled);
        Assert.Null(user.TotpSecret);
        Assert.Null(user.TwoFactorEnabledAt);
        Assert.Empty(user.BackupCodes);
    }

    [Fact]
    public void Disable2FA_WhenNotEnabled_ShouldThrowDomainException()
    {
        // Arrange
        var user = CreateTestUser();

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() => user.Disable2FA());
        Assert.Contains("not enabled", exception.Message);
    }

    [Fact]
    public void RequiresTwoFactor_WhenEnabled_ShouldReturnTrue()
    {
        // Arrange
        var user = CreateTestUser();
        var totpSecret = TotpSecret.FromEncrypted("encrypted_secret_base64");
        var backupCodes = new List<BackupCode> { BackupCode.FromHashed("hash1") };
        user.Enable2FA(totpSecret, backupCodes);

        // Act
        var result = user.RequiresTwoFactor();

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void RequiresTwoFactor_WhenNotEnabled_ShouldReturnFalse()
    {
        // Arrange
        var user = CreateTestUser();

        // Act
        var result = user.RequiresTwoFactor();

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void UseBackupCode_WithValidCode_ShouldMarkAsUsed()
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
        var code = user.BackupCodes.First();
        Assert.True(code.IsUsed);
        Assert.Equal(usedAt, code.UsedAt);
    }

    [Fact]
    public void UseBackupCode_WithInvalidCode_ShouldThrowDomainException()
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
    public void GetUnusedBackupCodesCount_ShouldReturnCorrectCount()
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
    public void HasUnusedBackupCode_WithValidUnusedCode_ShouldReturnTrue()
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
    public void HasUnusedBackupCode_WithUsedCode_ShouldReturnFalse()
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
    public void HasUnusedBackupCode_WithInvalidCode_ShouldReturnFalse()
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

    #endregion

    private static User CreateTestUser()
    {
        return new User(
            id: Guid.NewGuid(),
            email: new Email("test@example.com"),
            displayName: "Test User",
            passwordHash: PasswordHash.Create("OldPassword123!"),
            role: Role.User
        );
    }
}
