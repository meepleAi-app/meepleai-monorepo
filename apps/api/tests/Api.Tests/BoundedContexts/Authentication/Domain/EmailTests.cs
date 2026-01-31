using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Domain;

[Trait("Category", TestCategories.Unit)]
public class EmailTests
{
    [Fact]
    public void Email_WithValidEmail_CreatesSuccessfully()
    {
        // Arrange & Act
        var email = new Email("test@example.com");

        // Assert
        Assert.Equal("test@example.com", email.Value);
    }

    [Fact]
    public void Email_NormalizesToLowercase()
    {
        // Arrange & Act
        var email = new Email("Test@EXAMPLE.COM");

        // Assert
        Assert.Equal("test@example.com", email.Value);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Email_WithEmptyValue_ThrowsValidationException(string invalidEmail)
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new Email(invalidEmail));
        Assert.Contains("Email cannot be empty", exception.Message);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("@example.com")]
    [InlineData("test@")]
    [InlineData("test @example.com")]
    [InlineData("test@.com")]
    public void Email_WithInvalidFormat_ThrowsValidationException(string invalidEmail)
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => new Email(invalidEmail));
        Assert.Contains("Invalid email format", exception.Message);
    }

    [Fact]
    public void Email_EqualityComparison_WorksCorrectly()
    {
        // Arrange
        var email1 = new Email("test@example.com");
        var email2 = new Email("TEST@EXAMPLE.COM");
        var email3 = new Email("other@example.com");

        // Act & Assert
        Assert.Equal(email1, email2);
        Assert.NotEqual(email1, email3);
    }
}
