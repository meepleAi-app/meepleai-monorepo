using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using FluentAssertions;
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
        backupCode.Should().NotBeNull();
        backupCode.HashedValue.Should().Be(hashedValue);
        backupCode.IsUsed.Should().BeFalse();
        backupCode.UsedAt.Should().BeNull();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromHashed_WithInvalidHash_ShouldThrowValidationException(string? invalidHash)
    {
        // Act & Assert
        var act = () =>
            BackupCode.FromHashed(invalidHash!);
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().Contain("Backup code");
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
        backupCode.Should().NotBeNull();
        backupCode.HashedValue.Should().Be(hashedValue);
        backupCode.IsUsed.Should().BeTrue();
        backupCode.UsedAt.Should().Be(usedAt);
    }

    [Fact]
    public void FromHashed_WithUsedButNoTimestamp_ShouldThrowValidationException()
    {
        // Arrange
        var hashedValue = "hashed_backup_code_123";

        // Act & Assert
        var act = () =>
            BackupCode.FromHashed(hashedValue, isUsed: true, usedAt: null);
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().Contain("Used backup code must have UsedAt timestamp");
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
        backupCode.IsUsed.Should().BeTrue();
        backupCode.UsedAt.Should().Be(usedAt);
    }

    [Fact]
    public void MarkAsUsed_WithAlreadyUsedCode_ShouldThrowDomainException()
    {
        // Arrange
        var backupCode = BackupCode.FromHashed("hashed_backup_code_123");
        backupCode.MarkAsUsed(DateTime.UtcNow);

        // Act & Assert
        var act = () =>
            backupCode.MarkAsUsed(DateTime.UtcNow);
        var exception = act.Should().Throw<DomainException>().Which;

        exception.Message.Should().Contain("already been used");
    }

    [Fact]
    public void ToString_ForUnusedCode_ShouldShowUnusedStatus()
    {
        // Arrange
        var backupCode = BackupCode.FromHashed("hashed_backup_code_123");

        // Act
        var result = backupCode.ToString();

        // Assert
        result.Should().Be("[BACKUP_CODE_UNUSED]");
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
        result.Should().Contain("[BACKUP_CODE_USED:");
        result.Should().Contain("2025-11-14");
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
        result.Should().Be(hashedValue);
    }

    [Fact]
    public void TwoCodesWithSameValues_ShouldBeEqual()
    {
        // Arrange
        var hashedValue = "hashed_backup_code_123";
        var code1 = BackupCode.FromHashed(hashedValue);
        var code2 = BackupCode.FromHashed(hashedValue);

        // Act & Assert
        code2.Should().Be(code1);
    }

    [Fact]
    public void TwoCodesWithDifferentHashes_ShouldNotBeEqual()
    {
        // Arrange
        var code1 = BackupCode.FromHashed("hashed_backup_code_1");
        var code2 = BackupCode.FromHashed("hashed_backup_code_2");

        // Act & Assert
        code2.Should().NotBe(code1);
    }

    [Fact]
    public void UnusedAndUsedCodesWithSameHash_ShouldNotBeEqual()
    {
        // Arrange
        var hashedValue = "hashed_backup_code_123";
        var unusedCode = BackupCode.FromHashed(hashedValue);
        var usedCode = BackupCode.FromHashed(hashedValue, isUsed: true, usedAt: DateTime.UtcNow);

        // Act & Assert
        usedCode.Should().NotBe(unusedCode);
    }
}