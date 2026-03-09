using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.SharedKernel.Domain.ValueObjects;
using Api.SharedKernel.Domain.Exceptions;
using System;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Domain;

[Trait("Category", TestCategories.Unit)]
public class PasswordHashTests
{
    [Fact]
    public void Create_WithValidPassword_GeneratesHash()
    {
        // Arrange
        var password = "SecurePassword123!";

        // Act
        var hash = PasswordHash.Create(password);

        // Assert
        Assert.NotNull(hash);
        Assert.NotNull(hash.Value);
        Assert.StartsWith("v1.", hash.Value, StringComparison.OrdinalIgnoreCase);
    }

    [Theory]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_WithEmptyPassword_ThrowsValidationException(string invalidPassword)
    {
        // Act & Assert
        var exception = Assert.Throws<ValidationException>(() => PasswordHash.Create(invalidPassword));
        Assert.Contains("Password cannot be empty", exception.Message);
    }

    [Fact]
    public void Verify_WithCorrectPassword_ReturnsTrue()
    {
        // Arrange
        var password = "SecurePassword123!";
        var hash = PasswordHash.Create(password);

        // Act
        var result = hash.Verify(password);

        // Assert
        Assert.True(result);
    }

    [Fact]
    public void Verify_WithIncorrectPassword_ReturnsFalse()
    {
        // Arrange
        var hash = PasswordHash.Create("CorrectPassword123!");

        // Act
        var result = hash.Verify("WrongPassword456!");

        // Assert
        Assert.False(result);
    }

    [Fact]
    public void ToString_RedactsHashValue()
    {
        // Arrange
        var hash = PasswordHash.Create("SecurePassword123!");

        // Act
        var stringValue = hash.ToString();

        // Assert
        Assert.Equal("[REDACTED]", stringValue);
    }

}
