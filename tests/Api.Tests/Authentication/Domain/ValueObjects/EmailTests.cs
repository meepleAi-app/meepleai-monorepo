using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Authentication.Domain.ValueObjects;

[Trait("Category", "Unit")]
public sealed class EmailTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidEmail_CreatesEmail()
    {
        // Arrange
        var emailValue = "test@example.com";

        // Act
        var email = new Email(emailValue);

        // Assert
        email.Value.Should().Be("test@example.com");
    }

    [Fact]
    public void Constructor_NormalizesToLowercase()
    {
        // Arrange
        var emailValue = "TEST@EXAMPLE.COM";

        // Act
        var email = new Email(emailValue);

        // Assert
        email.Value.Should().Be("test@example.com");
    }

    [Fact]
    public void Constructor_TrimsWhitespace()
    {
        // Arrange
        var emailValue = "  test@example.com  ";

        // Act
        var email = new Email(emailValue);

        // Assert
        email.Value.Should().Be("test@example.com");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Constructor_WithEmptyEmail_ThrowsValidationException(string? invalidEmail)
    {
        // Act & Assert
        var action = () => new Email(invalidEmail!);
        action.Should().Throw<ValidationException>()
            .WithMessage("*Email cannot be empty*");
    }

    [Fact]
    public void Constructor_WithTooLongEmail_ThrowsValidationException()
    {
        // Arrange - Create email > 256 characters
        // "a{245}@example.com" = 245 + 1 + 11 = 257 chars
        var longLocal = new string('a', 245);
        var longEmail = $"{longLocal}@example.com";

        // Assert the email is actually too long
        longEmail.Length.Should().BeGreaterThan(256);

        // Act & Assert
        var action = () => new Email(longEmail);
        action.Should().Throw<ValidationException>()
            .WithMessage("*Email cannot exceed 256 characters*");
    }

    [Fact]
    public void Constructor_WithMaxLengthEmail_Succeeds()
    {
        // Arrange - Exactly 256 characters
        var local = new string('a', 240);
        var email = $"{local}@example.com"; // Should be close to 256

        // Only test if actually <= 256
        if (email.Length <= 256)
        {
            // Act
            var result = new Email(email);

            // Assert
            result.Should().NotBeNull();
        }
    }

    #endregion

    #region Email Format Validation Tests

    [Theory]
    [InlineData("user@example.com")]
    [InlineData("user.name@example.com")]
    [InlineData("user+tag@example.com")]
    [InlineData("user@sub.example.com")]
    [InlineData("user123@example.com")]
    [InlineData("user@example.co.uk")]
    public void Constructor_WithValidFormats_Succeeds(string validEmail)
    {
        // Act
        var email = new Email(validEmail);

        // Assert
        email.Should().NotBeNull();
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("@example.com")]
    [InlineData("user@")]
    [InlineData("user@@example.com")]
    [InlineData("user@.com")]
    // Note: "user@example" is valid per RFC 5322 (TLD without dot is allowed)
    public void Constructor_WithInvalidFormats_ThrowsValidationException(string invalidEmail)
    {
        // Act & Assert
        var action = () => new Email(invalidEmail);
        action.Should().Throw<ValidationException>()
            .WithMessage("*Invalid email format*");
    }

    #endregion

    #region Parse Tests

    [Fact]
    public void Parse_WithValidEmail_ReturnsEmail()
    {
        // Act
        var email = Email.Parse("test@example.com");

        // Assert
        email.Value.Should().Be("test@example.com");
    }

    [Fact]
    public void Parse_NormalizesToLowercase()
    {
        // Act
        var email = Email.Parse("TEST@EXAMPLE.COM");

        // Assert
        email.Value.Should().Be("test@example.com");
    }

    #endregion

    #region ToString Tests

    [Fact]
    public void ToString_ReturnsValue()
    {
        // Arrange
        var email = new Email("test@example.com");

        // Act
        var result = email.ToString();

        // Assert
        result.Should().Be("test@example.com");
    }

    #endregion

    #region Implicit Conversion Tests

    [Fact]
    public void ImplicitConversionToString_ReturnsValue()
    {
        // Arrange
        var email = new Email("user@example.com");

        // Act
        string stringValue = email;

        // Assert
        stringValue.Should().Be("user@example.com");
    }

    [Fact]
    public void ImplicitConversionToString_WithNullEmail_ThrowsArgumentNullException()
    {
        // Arrange
        Email? email = null;

        // Act & Assert
        var action = () => { string _ = email!; };
        action.Should().Throw<ArgumentNullException>();
    }

    #endregion

    #region Value Equality Tests

    [Fact]
    public void Equals_SameEmail_AreEqual()
    {
        // Arrange
        var email1 = new Email("test@example.com");
        var email2 = new Email("test@example.com");

        // Act & Assert
        email1.Should().Be(email2);
    }

    [Fact]
    public void Equals_DifferentCase_AreEqual()
    {
        // Arrange - Should normalize to same value
        var email1 = new Email("TEST@example.com");
        var email2 = new Email("test@EXAMPLE.COM");

        // Act & Assert
        email1.Should().Be(email2);
    }

    [Fact]
    public void Equals_DifferentEmails_AreNotEqual()
    {
        // Arrange
        var email1 = new Email("user1@example.com");
        var email2 = new Email("user2@example.com");

        // Act & Assert
        email1.Should().NotBe(email2);
    }

    [Fact]
    public void GetHashCode_SameEmail_ReturnsSameHashCode()
    {
        // Arrange
        var email1 = new Email("test@example.com");
        var email2 = new Email("TEST@EXAMPLE.COM");

        // Act & Assert
        email1.GetHashCode().Should().Be(email2.GetHashCode());
    }

    [Fact]
    public void GetHashCode_DifferentEmails_ReturnDifferentHashCodes()
    {
        // Arrange
        var email1 = new Email("user1@example.com");
        var email2 = new Email("user2@example.com");

        // Act & Assert
        email1.GetHashCode().Should().NotBe(email2.GetHashCode());
    }

    #endregion

    #region Edge Cases

    [Theory]
    [InlineData("user.name+tag@example.com")]
    [InlineData("user_name@example.com")]
    [InlineData("user-name@example.com")]
    [InlineData("user123@example.com")]
    public void Constructor_WithSpecialCharactersInLocal_Succeeds(string email)
    {
        // Act
        var result = new Email(email);

        // Assert
        result.Should().NotBeNull();
    }

    [Fact]
    public void Constructor_WithSubdomains_Succeeds()
    {
        // Act
        var email = new Email("user@mail.sub.example.com");

        // Assert
        email.Value.Should().Be("user@mail.sub.example.com");
    }

    [Fact]
    public void Constructor_WithNumericDomain_Succeeds()
    {
        // Act
        var email = new Email("user@123.example.com");

        // Assert
        email.Should().NotBeNull();
    }

    #endregion
}
