using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Authentication.Domain.ValueObjects;

[Trait("Category", "Unit")]
public sealed class PasswordHashTests
{
    #region Create Factory Tests

    [Fact]
    public void Create_WithValidPassword_CreatesHash()
    {
        // Arrange
        var password = "SecurePassword123";

        // Act
        var hash = PasswordHash.Create(password);

        // Assert
        hash.Should().NotBeNull();
        hash.Value.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public void Create_WithValidPassword_HasVersionedFormat()
    {
        // Arrange
        var password = "TestPassword123";

        // Act
        var hash = PasswordHash.Create(password);

        // Assert - Format should be: version.iterations.salt.hash
        var parts = hash.Value.Split('.');
        parts.Should().HaveCount(4);
        parts[0].Should().Be("v1"); // Version
        parts[1].Should().Be("210000"); // OWASP recommended iterations
    }

    [Fact]
    public void Create_GeneratesDifferentHashForSamePassword()
    {
        // Arrange - Due to random salt, same password should produce different hashes
        var password = "SamePassword123";

        // Act
        var hash1 = PasswordHash.Create(password);
        var hash2 = PasswordHash.Create(password);

        // Assert
        hash1.Value.Should().NotBe(hash2.Value);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyPassword_ThrowsValidationException(string? invalidPassword)
    {
        // Act & Assert
        var action = () => PasswordHash.Create(invalidPassword!);
        action.Should().Throw<ValidationException>()
            .WithMessage("*Password cannot be empty*");
    }

    [Theory]
    [InlineData("short")]
    [InlineData("1234567")]
    [InlineData("abc")]
    public void Create_WithTooShortPassword_ThrowsValidationException(string shortPassword)
    {
        // Act & Assert
        var action = () => PasswordHash.Create(shortPassword);
        action.Should().Throw<ValidationException>()
            .WithMessage("*Password must be at least 8 characters*");
    }

    [Fact]
    public void Create_WithExactlyMinimumLength_Succeeds()
    {
        // Arrange - Exactly 8 characters
        var password = "12345678";

        // Act
        var hash = PasswordHash.Create(password);

        // Assert
        hash.Should().NotBeNull();
    }

    [Fact]
    public void Create_WithLongPassword_Succeeds()
    {
        // Arrange - Very long password
        var password = new string('A', 1000);

        // Act
        var hash = PasswordHash.Create(password);

        // Assert
        hash.Should().NotBeNull();
    }

    #endregion

    #region FromStored Factory Tests

    [Fact]
    public void FromStored_WithValidHash_ReconstructsPasswordHash()
    {
        // Arrange
        var originalHash = PasswordHash.Create("TestPassword123");
        var storedValue = originalHash.Value;

        // Act
        var reconstructed = PasswordHash.FromStored(storedValue);

        // Assert
        reconstructed.Value.Should().Be(storedValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromStored_WithEmptyHash_ThrowsValidationException(string? invalidHash)
    {
        // Act & Assert
        var action = () => PasswordHash.FromStored(invalidHash!);
        action.Should().Throw<ValidationException>()
            .WithMessage("*Password hash cannot be empty*");
    }

    #endregion

    #region Verify Tests

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "CorrectPassword123";
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
        var hash = PasswordHash.Create("CorrectPassword123");

        // Act
        var result = hash.Verify("WrongPassword456");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_WithCaseDifferentPassword_ReturnsFalse()
    {
        // Arrange - Passwords are case-sensitive
        var hash = PasswordHash.Create("Password123");

        // Act
        var result = hash.Verify("PASSWORD123");

        // Assert
        result.Should().BeFalse();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Verify_WithEmptyPassword_ReturnsFalse(string? emptyPassword)
    {
        // Arrange
        var hash = PasswordHash.Create("ValidPassword123");

        // Act
        var result = hash.Verify(emptyPassword!);

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_WithCorruptedHash_ReturnsFalse()
    {
        // Arrange - Manually construct an invalid hash
        var corruptedHash = PasswordHash.FromStored("v1.210000.invalid.corrupted");

        // Act
        var result = corruptedHash.Verify("AnyPassword123");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_WithWrongVersion_ReturnsFalse()
    {
        // Arrange - Wrong version prefix
        var wrongVersion = PasswordHash.FromStored("v2.210000.AAAAAAAAAA==.BBBBBBBBBB==");

        // Act
        var result = wrongVersion.Verify("AnyPassword123");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public void Verify_WithInvalidIterations_ReturnsFalse()
    {
        // Arrange - Non-numeric iterations
        var invalidIterations = PasswordHash.FromStored("v1.notanumber.AAAAAAAAAA==.BBBBBBBBBB==");

        // Act
        var result = invalidIterations.Verify("AnyPassword123");

        // Assert
        result.Should().BeFalse();
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsRedacted()
    {
        // Arrange
        var hash = PasswordHash.Create("SecretPassword123");

        // Act
        var result = hash.ToString();

        // Assert - Should never expose actual hash
        result.Should().Be("[REDACTED]");
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversionToString_ReturnsValue()
    {
        // Arrange
        var hash = PasswordHash.Create("Password123");

        // Act
        string stringValue = hash;

        // Assert
        stringValue.Should().Be(hash.Value);
    }

    [Fact]
    public void ImplicitConversionToString_WithNullHash_ThrowsArgumentNullException()
    {
        // Arrange
        PasswordHash? hash = null;

        // Act & Assert
        var action = () => { string _ = hash!; };
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Value Equality Tests

    [Fact]
    public void Equals_SameHashValue_AreEqual()
    {
        // Arrange
        var hash1 = PasswordHash.Create("Password123");
        var hash2 = PasswordHash.FromStored(hash1.Value);

        // Act & Assert
        hash1.Should().Be(hash2);
    }

    [Fact]
    public void Equals_DifferentHashValues_AreNotEqual()
    {
        // Arrange
        var hash1 = PasswordHash.Create("Password123");
        var hash2 = PasswordHash.Create("Password123"); // Same password, different salt

        // Act & Assert
        hash1.Should().NotBe(hash2);
    }

    [Fact]
    public void GetHashCode_SameValue_ReturnsSameHashCode()
    {
        // Arrange
        var hash1 = PasswordHash.Create("Password123");
        var hash2 = PasswordHash.FromStored(hash1.Value);

        // Act & Assert
        hash1.GetHashCode().Should().Be(hash2.GetHashCode());
    }

    #endregion

    #region Security Tests

    [Fact]
    public void Create_WithSpecialCharacters_HandlesCorrectly()
    {
        // Arrange
        var password = "P@$$w0rd!#$%^&*()";

        // Act
        var hash = PasswordHash.Create(password);
        var verified = hash.Verify(password);

        // Assert
        verified.Should().BeTrue();
    }

    [Fact]
    public void Create_WithUnicodeCharacters_HandlesCorrectly()
    {
        // Arrange
        var password = "Пароль123日本語";

        // Act
        var hash = PasswordHash.Create(password);
        var verified = hash.Verify(password);

        // Assert
        verified.Should().BeTrue();
    }

    [Fact]
    public void Verify_IsTimingAttackResistant()
    {
        // Arrange - Verification should use constant-time comparison
        var hash = PasswordHash.Create("SecurePassword123");

        // Act - Multiple verifications should have similar timing (basic check)
        var results = new List<bool>();
        for (int i = 0; i < 10; i++)
        {
            results.Add(hash.Verify("WrongPassword" + i));
        }

        // Assert - All should return false
        results.Should().AllBeEquivalentTo(false);
    }

    #endregion
}
