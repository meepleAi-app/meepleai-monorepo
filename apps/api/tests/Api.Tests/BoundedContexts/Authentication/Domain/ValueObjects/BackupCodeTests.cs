using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Tests for BackupCode value object.
/// Validates backup code creation, usage tracking, and business rules.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class BackupCodeTests
{
    [Fact]
    public void FromHashed_WithValidHash_ShouldCreateBackupCode()
    {
        // Arrange
        var hashedValue = "hashed_backup_code_123";

        // Act
        var backupCode = BackupCode.FromHashed(hashedValue);

        // Assert
        Assert.NotNull(backupCode);
        Assert.Equal(hashedValue, backupCode.HashedValue);
        Assert.False(backupCode.IsUsed);
        Assert.Null(backupCode.UsedAt);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromHashed_WithInvalidHash_ShouldThrowValidationException(string? invalidHash)
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            BackupCode.FromHashed(invalidHash!));

        Assert.Contains("Backup code", exception.Message);
    }

    [Fact]
    public void FromHashed_WithUsedState_ShouldCreateUsedBackupCode()
    {
        // Arrange
        var hashedValue = "hashed_backup_code_123";
        var usedAt = DateTime.UtcNow;

        // Act
        var backupCode = BackupCode.FromHashed(hashedValue, isUsed: true, usedAt: usedAt);

        // Assert
        Assert.NotNull(backupCode);
        Assert.Equal(hashedValue, backupCode.HashedValue);
        Assert.True(backupCode.IsUsed);
        Assert.Equal(usedAt, backupCode.UsedAt);
    }

    [Fact]
    public void FromHashed_WithUsedButNoTimestamp_ShouldThrowValidationException()
    {
        // Arrange
        var hashedValue = "hashed_backup_code_123";

        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            BackupCode.FromHashed(hashedValue, isUsed: true, usedAt: null));

        Assert.Contains("Used backup code must have UsedAt timestamp", exception.Message);
    }

    [Fact]
    public void MarkAsUsed_WithUnusedCode_ShouldMarkCodeAsUsed()
    {
        // Arrange
        var backupCode = BackupCode.FromHashed("hashed_backup_code_123");
        var usedAt = DateTime.UtcNow;

        // Act
        backupCode.MarkAsUsed(usedAt);

        // Assert
        Assert.True(backupCode.IsUsed);
        Assert.Equal(usedAt, backupCode.UsedAt);
    }

    [Fact]
    public void MarkAsUsed_WithAlreadyUsedCode_ShouldThrowDomainException()
    {
        // Arrange
        var backupCode = BackupCode.FromHashed("hashed_backup_code_123");
        backupCode.MarkAsUsed(DateTime.UtcNow);

        // Act & Assert
        var exception = Assert.Throws<DomainException>(() =>
            backupCode.MarkAsUsed(DateTime.UtcNow));

        Assert.Contains("already been used", exception.Message);
    }

    [Fact]
    public void ToString_ForUnusedCode_ShouldShowUnusedStatus()
    {
        // Arrange
        var backupCode = BackupCode.FromHashed("hashed_backup_code_123");

        // Act
        var result = backupCode.ToString();

        // Assert
        Assert.Equal("[BACKUP_CODE_UNUSED]", result);
    }

    [Fact]
    public void ToString_ForUsedCode_ShouldShowUsedStatusWithTimestamp()
    {
        // Arrange
        var usedAt = new DateTime(2025, 11, 14, 12, 30, 0);
        var backupCode = BackupCode.FromHashed("hashed_backup_code_123", isUsed: true, usedAt: usedAt);

        // Act
        var result = backupCode.ToString();

        // Assert
        Assert.Contains("[BACKUP_CODE_USED:", result);
        Assert.Contains("2025-11-14", result);
    }

    [Fact]
    public void ImplicitConversion_ToString_ShouldReturnHashedValue()
    {
        // Arrange
        var hashedValue = "hashed_backup_code_123";
        var backupCode = BackupCode.FromHashed(hashedValue);

        // Act
        string result = backupCode; // Implicit conversion

        // Assert
        Assert.Equal(hashedValue, result);
    }

    [Fact]
    public void TwoCodesWithSameValues_ShouldBeEqual()
    {
        // Arrange
        var hashedValue = "hashed_backup_code_123";
        var code1 = BackupCode.FromHashed(hashedValue);
        var code2 = BackupCode.FromHashed(hashedValue);

        // Act & Assert
        Assert.Equal(code1, code2);
    }

    [Fact]
    public void TwoCodesWithDifferentHashes_ShouldNotBeEqual()
    {
        // Arrange
        var code1 = BackupCode.FromHashed("hashed_backup_code_1");
        var code2 = BackupCode.FromHashed("hashed_backup_code_2");

        // Act & Assert
        Assert.NotEqual(code1, code2);
    }

    [Fact]
    public void UnusedAndUsedCodesWithSameHash_ShouldNotBeEqual()
    {
        // Arrange
        var hashedValue = "hashed_backup_code_123";
        var unusedCode = BackupCode.FromHashed(hashedValue);
        var usedCode = BackupCode.FromHashed(hashedValue, isUsed: true, usedAt: DateTime.UtcNow);

        // Act & Assert
        Assert.NotEqual(unusedCode, usedCode);
    }
}
