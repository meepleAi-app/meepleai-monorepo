using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Tests for TotpSecret value object.
/// Validates encrypted TOTP secret creation and business rules.
/// </summary>
public class TotpSecretTests
{
    [Fact]
    public void FromEncrypted_WithValidValue_ShouldCreateTotpSecret()
    {
        // Arrange
        var encryptedValue = "encrypted_secret_base64";

        // Act
        var secret = TotpSecret.FromEncrypted(encryptedValue);

        // Assert
        Assert.NotNull(secret);
        Assert.Equal(encryptedValue, secret.EncryptedValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromEncrypted_WithInvalidValue_ShouldThrowValidationException(string? invalidValue)
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() =>
            TotpSecret.FromEncrypted(invalidValue!));

        Assert.Contains("TOTP secret", exception.Message);
    }

    [Fact]
    public void ToString_ShouldRedactSecretValue()
    {
        // Arrange
        var secret = TotpSecret.FromEncrypted("encrypted_secret_base64");

        // Act
        var result = secret.ToString();

        // Assert
        Assert.Equal("[TOTP_SECRET_REDACTED]", result);
        Assert.DoesNotContain("encrypted_secret_base64", result);
    }

    [Fact]
    public void ImplicitConversion_ToString_ShouldReturnEncryptedValue()
    {
        // Arrange
        var encryptedValue = "encrypted_secret_base64";
        var secret = TotpSecret.FromEncrypted(encryptedValue);

        // Act
        string result = secret; // Implicit conversion

        // Assert
        Assert.Equal(encryptedValue, result);
    }

    [Fact]
    public void TwoSecretsWithSameValue_ShouldBeEqual()
    {
        // Arrange
        var encryptedValue = "encrypted_secret_base64";
        var secret1 = TotpSecret.FromEncrypted(encryptedValue);
        var secret2 = TotpSecret.FromEncrypted(encryptedValue);

        // Act & Assert
        Assert.Equal(secret1, secret2);
        Assert.True(secret1 == secret2);
        Assert.False(secret1 != secret2);
    }

    [Fact]
    public void TwoSecretsWithDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var secret1 = TotpSecret.FromEncrypted("encrypted_secret_1");
        var secret2 = TotpSecret.FromEncrypted("encrypted_secret_2");

        // Act & Assert
        Assert.NotEqual(secret1, secret2);
        Assert.False(secret1 == secret2);
        Assert.True(secret1 != secret2);
    }
}

