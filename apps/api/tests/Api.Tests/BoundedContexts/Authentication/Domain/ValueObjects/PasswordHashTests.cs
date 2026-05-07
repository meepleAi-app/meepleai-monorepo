using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Tests for the PasswordHash value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 20
/// </summary>
[Trait("Category", "Unit")]
public sealed class PasswordHashTests
{
    #region Create Tests

    [Fact]
    public void Create_WithValidPassword_CreatesHash()
    {
        // Arrange
        var password = "SecurePassword123!";

        // Act
        var hash = PasswordHash.Create(password);

        // Assert
        hash.Value.Should().NotBeNullOrWhiteSpace();
        hash.Value.Should().StartWith("v1."); // Version prefix
    }

    [Fact]
    public void Create_WithEmptyPassword_ThrowsValidationException()
    {
        // Act
        var action = () => PasswordHash.Create("");

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Password cannot be empty*");
    }

    [Fact]
    public void Create_WithWhitespacePassword_ThrowsValidationException()
    {
        // Act
        var action = () => PasswordHash.Create("   ");

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Password cannot be empty*");
    }

    [Fact]
    public void Create_WithPasswordLessThan12Characters_ThrowsValidationException()
    {
        // I7 (auth security fixes): minimum length raised from 8 to 12.
        var action = () => PasswordHash.Create("Short1!");

        action.Should().Throw<ValidationException>()
            .WithMessage("*Password must be at least 12 characters*");
    }

    [Fact]
    public void Create_WithPasswordExactly12Characters_Succeeds()
    {
        // I7: 12 chars is the new minimum. The plaintext must also avoid
        // the common-password blocklist — "Exactly12abc" satisfies both.
        var hash = PasswordHash.Create("Exactly12abc");

        hash.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Create_WithPasswordExceeding128Characters_ThrowsValidationException()
    {
        // I7: max length 128 caps PBKDF2 input cost.
        var tooLong = new string('A', 129);

        var action = () => PasswordHash.Create(tooLong);

        action.Should().Throw<ValidationException>()
            .WithMessage("*Password cannot exceed 128 characters*");
    }

    [Fact]
    public void Create_WithCommonPassword_ThrowsValidationException()
    {
        // I7: top-N common-password blocklist. "password1234" is in the
        // shipped seed AND is length-compliant (12 chars), so the blocklist
        // is the gate that catches it (length passes, blocklist rejects).
        var action = () => PasswordHash.Create("password1234");

        action.Should().Throw<ValidationException>()
            .WithMessage("*compromised passwords*");
    }

    [Fact]
    public void Create_WithCommonPasswordCaseVariant_ThrowsValidationException()
    {
        // I7: blocklist lookup is case-insensitive — "PASSWORD1234" and
        // "password1234" are both equally trivial to brute-force.
        var action = () => PasswordHash.Create("PASSWORD1234");

        action.Should().Throw<ValidationException>()
            .WithMessage("*compromised passwords*");
    }

    [Fact]
    public void Create_ProducesUniqueHashesForSamePassword()
    {
        // Arrange - same password
        var password = "SecurePassword123!";

        // Act - create multiple hashes
        var hash1 = PasswordHash.Create(password);
        var hash2 = PasswordHash.Create(password);

        // Assert - different hashes due to random salt
        hash1.Value.Should().NotBe(hash2.Value);
    }

    [Fact]
    public void Create_HashContainsFourParts()
    {
        // Arrange
        var password = "SecurePassword123!";

        // Act
        var hash = PasswordHash.Create(password);

        // Assert - format: version.iterations.salt.hash
        var parts = hash.Value.Split('.');
        parts.Should().HaveCount(4);
    }

    #endregion

    #region FromStored Tests

    [Fact]
    public void FromStored_WithValidStoredHash_ReturnsPasswordHash()
    {
        // Arrange
        var originalHash = PasswordHash.Create("SecurePassword123!");
        var storedValue = originalHash.Value;

        // Act
        var reconstructed = PasswordHash.FromStored(storedValue);

        // Assert
        reconstructed.Value.Should().Be(storedValue);
    }

    [Fact]
    public void FromStored_WithEmptyString_ThrowsValidationException()
    {
        // Act
        var action = () => PasswordHash.FromStored("");

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Password hash cannot be empty*");
    }

    [Fact]
    public void FromStored_WithWhitespace_ThrowsValidationException()
    {
        // Act
        var action = () => PasswordHash.FromStored("   ");

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Password hash cannot be empty*");
    }

    #endregion

    #region Verify Tests

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "SecurePassword123!";
        var hash = PasswordHash.Create(password);

        // Act
        var result = hash.Verify(password);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_WithIncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var hash = PasswordHash.Create("SecurePassword123!");

        // Act
        var result = hash.Verify("WrongPassword!");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_WithEmptyPassword_ReturnsFalse()
    {
        // Arrange
        var hash = PasswordHash.Create("SecurePassword123!");

        // Act
        var result = hash.Verify("");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_WithWhitespacePassword_ReturnsFalse()
    {
        // Arrange
        var hash = PasswordHash.Create("SecurePassword123!");

        // Act
        var result = hash.Verify("   ");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_WithCaseSensitivePassword_ReturnsFalseForWrongCase()
    {
        // Arrange
        var hash = PasswordHash.Create("SecurePassword123!");

        // Act - wrong case
        var result = hash.Verify("SECUREPASSWORD123!");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_WithReconstructedHash_WorksCorrectly()
    {
        // Arrange
        var password = "SecurePassword123!";
        var original = PasswordHash.Create(password);
        var reconstructed = PasswordHash.FromStored(original.Value);

        // Act
        var result = reconstructed.Verify(password);

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public void Verify_WithMalformedHash_ReturnsFalse()
    {
        // Arrange - manually create a malformed hash
        var malformed = PasswordHash.FromStored("not.a.valid.hash");

        // Act
        var result = malformed.Verify("any-password");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_WithWrongVersionHash_ReturnsFalse()
    {
        // Arrange - wrong version prefix
        var wrongVersion = PasswordHash.FromStored("v2.210000.c29tZXNhbHQ=.c29tZWhhc2g=");

        // Act
        var result = wrongVersion.Verify("any-password");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameHashValue_ReturnsTrue()
    {
        // Arrange
        var originalHash = PasswordHash.Create("SecurePassword123!");
        var hash1 = PasswordHash.FromStored(originalHash.Value);
        var hash2 = PasswordHash.FromStored(originalHash.Value);

        // Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Equals_WithDifferentHashValues_ReturnsFalse()
    {
        // Arrange — "Password123!" is in the I7 blocklist; pick a
        // length-compliant non-common password instead.
        var hash1 = PasswordHash.Create("UnusualUniqueP4ssword!");
        var hash2 = PasswordHash.Create("UnusualUniqueP4ssword!");

        // Assert - different due to random salt
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void GetHashCode_WithSameValue_ReturnsSameHash()
    {
        // Arrange
        var originalHash = PasswordHash.Create("SecurePassword123!");
        var hash1 = PasswordHash.FromStored(originalHash.Value);
        var hash2 = PasswordHash.FromStored(originalHash.Value);

        // Assert
        hash1.GetHashCode().Should().Be(hash2.GetHashCode());
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsRedacted()
    {
        // Arrange
        var hash = PasswordHash.Create("SecurePassword123!");

        // Act
        var result = hash.ToString();

        // Assert
        result.Should().Be("[REDACTED]");
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void Value_ReturnsStoredHashString()
    {
        // R1 (auth security fixes): the implicit operator string(PasswordHash)
        // was removed because it silently bypassed ToString() = "[REDACTED]"
        // and could leak the stored hash through any string-typed sink.
        // Callers must reach for .Value explicitly.
        var hash = PasswordHash.Create("SecurePassword123!");

        hash.Value.Should().StartWith("v1.");
        hash.ToString().Should().Be("[REDACTED]",
            "ToString() must NEVER expose the stored hash — only .Value does.");
    }

    #endregion
}
