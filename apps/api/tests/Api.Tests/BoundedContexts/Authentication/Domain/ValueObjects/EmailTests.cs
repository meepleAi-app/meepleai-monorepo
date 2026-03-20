using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Domain.ValueObjects;

/// <summary>
/// Tests for the Email value object.
/// Issue #3025: Backend 90% Coverage Target - Phase 20
/// </summary>
[Trait("Category", "Unit")]
public sealed class EmailTests
{
    #region Constructor Tests

    [Fact]
    public void Constructor_WithValidEmail_CreatesEmail()
    {
        // Act
        var email = new Email("test@example.com");

        // Assert
        email.Value.Should().Be("test@example.com");
    }

    [Theory]
    [InlineData("user@domain.com")]
    [InlineData("user.name@domain.com")]
    [InlineData("user+tag@domain.com")]
    [InlineData("user@subdomain.domain.com")]
    [InlineData("user123@domain.co.uk")]
    public void Constructor_WithVariousValidEmails_Succeeds(string validEmail)
    {
        // Act
        var email = new Email(validEmail);

        // Assert
        email.Value.Should().Be(validEmail.ToLowerInvariant());
    }

    [Fact]
    public void Constructor_WithEmptyString_ThrowsValidationException()
    {
        // Act
        var action = () => new Email("");

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Email cannot be empty*");
    }

    [Fact]
    public void Constructor_WithWhitespace_ThrowsValidationException()
    {
        // Act
        var action = () => new Email("   ");

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Email cannot be empty*");
    }

    [Fact]
    public void Constructor_WithEmailExceeding256Characters_ThrowsValidationException()
    {
        // Arrange - 251 + "@b.com" = 257 chars (exceeds 256)
        var longEmail = new string('a', 251) + "@b.com";

        // Act
        var action = () => new Email(longEmail);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Email cannot exceed 256 characters*");
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("invalid@")]
    [InlineData("@domain.com")]
    [InlineData("user@.com")]
    public void Constructor_WithInvalidFormat_ThrowsValidationException(string invalidEmail)
    {
        // Act
        var action = () => new Email(invalidEmail);

        // Assert
        action.Should().Throw<ValidationException>()
            .WithMessage("*Invalid email format*");
    }

    [Fact]
    public void Constructor_NormalizesToLowercase()
    {
        // Act
        var email = new Email("Test.User@EXAMPLE.COM");

        // Assert
        email.Value.Should().Be("test.user@example.com");
    }

    [Fact]
    public void Constructor_TrimsWhitespace()
    {
        // Act
        var email = new Email("  test@example.com  ");

        // Assert
        email.Value.Should().Be("test@example.com");
    }

    #endregion

    #region Equality Tests

    [Fact]
    public void Equals_WithSameEmail_ReturnsTrue()
    {
        // Arrange
        var email1 = new Email("test@example.com");
        var email2 = new Email("test@example.com");

        // Assert
        email1.Should().Be(email2);
    }

    [Fact]
    public void Equals_WithDifferentCase_ReturnsTrue()
    {
        // Arrange
        var email1 = new Email("test@example.com");
        var email2 = new Email("TEST@EXAMPLE.COM");

        // Assert - both normalized to lowercase
        email1.Should().Be(email2);
    }

    [Fact]
    public void Equals_WithDifferentEmail_ReturnsFalse()
    {
        // Arrange
        var email1 = new Email("user1@example.com");
        var email2 = new Email("user2@example.com");

        // Assert
        email1.Should().NotBe(email2);
    }

    [Fact]
    public void GetHashCode_WithSameEmail_ReturnsSameHash()
    {
        // Arrange
        var email1 = new Email("test@example.com");
        var email2 = new Email("test@example.com");

        // Assert
        email1.GetHashCode().Should().Be(email2.GetHashCode());
    }

    #endregion

    #region ToString and Implicit Conversion Tests

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

    [Fact]
    public void ImplicitConversion_ToString_ReturnsValue()
    {
        // Arrange
        var email = new Email("test@example.com");

        // Act
        string value = email;

        // Assert
        value.Should().Be("test@example.com");
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
    public void Parse_WithInvalidEmail_ThrowsValidationException()
    {
        // Act
        var action = () => Email.Parse("invalid");

        // Assert
        action.Should().Throw<ValidationException>();
    }

    #endregion
}
