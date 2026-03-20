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
    public void Create_WithPasswordLessThan8Characters_ThrowsValidationException()
    {
        // Act
        var action = () => PasswordHash.Create("Short1!");

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Password must be at least 8 characters*");
    }

    [Fact]
    public void Create_WithPasswordExactly8Characters_Succeeds()
    {
        // Act
        var hash = PasswordHash.Create("Exactly8");

        // Assert
        hash.Value.Should().NotBeNullOrWhiteSpace();
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
        // Arrange
        var hash1 = PasswordHash.Create("Password123!");
        var hash2 = PasswordHash.Create("Password123!");

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
    public void ImplicitConversion_ToString_ReturnsHashValue()
    {
        // Arrange
        var hash = PasswordHash.Create("SecurePassword123!");

        // Act
        string value = hash;

        // Assert
        value.Should().Be(hash.Value);
        value.Should().StartWith("v1.");
    }

    #endregion
}
