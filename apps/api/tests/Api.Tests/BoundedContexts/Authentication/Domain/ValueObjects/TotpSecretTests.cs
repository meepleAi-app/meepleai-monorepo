using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using FluentAssertions;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Tests for TotpSecret value object.
/// Validates encrypted TOTP secret creation and business rules.
/// </summary>
[Trait("Category", TestCategories.Unit)]
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
        secret.Should().NotBeNull();
        secret.EncryptedValue.Should().Be(encryptedValue);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void FromEncrypted_WithInvalidValue_ShouldThrowValidationException(string? invalidValue)
    {
        // Act & Assert
        var act = () =>
            TotpSecret.FromEncrypted(invalidValue!);
        var exception = act.Should().Throw<ValidationException>().Which;

        exception.Message.Should().Contain("TOTP secret");
    }

    [Fact]
    public void ToString_ShouldRedactSecretValue()
    {
        // Arrange
        var secret = TotpSecret.FromEncrypted("encrypted_secret_base64");

        // Act
        var result = secret.ToString();

        // Assert
        result.Should().Be("[TOTP_SECRET_REDACTED]");
        result.Should().NotContain("encrypted_secret_base64");
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
        result.Should().Be(encryptedValue);
    }

    [Fact]
    public void TwoSecretsWithSameValue_ShouldBeEqual()
    {
        // Arrange
        var encryptedValue = "encrypted_secret_base64";
        var secret1 = TotpSecret.FromEncrypted(encryptedValue);
        var secret2 = TotpSecret.FromEncrypted(encryptedValue);

        // Act & Assert
        secret2.Should().Be(secret1);
        (secret1 == secret2).Should().BeTrue();
        (secret1 != secret2).Should().BeFalse();
    }

    [Fact]
    public void TwoSecretsWithDifferentValues_ShouldNotBeEqual()
    {
        // Arrange
        var secret1 = TotpSecret.FromEncrypted("encrypted_secret_1");
        var secret2 = TotpSecret.FromEncrypted("encrypted_secret_2");

        // Act & Assert
        secret2.Should().NotBe(secret1);
        (secret1 == secret2).Should().BeFalse();
        (secret1 != secret2).Should().BeTrue();
    }
}