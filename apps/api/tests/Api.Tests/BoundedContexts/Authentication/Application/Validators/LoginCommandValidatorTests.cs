using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Unit tests for LoginCommandValidator.
/// Issue #1449: FluentValidation for Authentication CQRS pipeline
/// </summary>
public sealed class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Should_Pass_When_Valid_Email_And_Password()
    {
        // Arrange
        var command = new LoginCommand(
            Email: "test@example.com",
            Password: "ValidPassword123!"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_When_Email_Is_Empty(string? email)
    {
        // Arrange
        var command = new LoginCommand(
            Email: email!,
            Password: "ValidPassword123!"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required");
    }

    [Theory]
    [InlineData("notanemail")]
    [InlineData("@example.com")]
    [InlineData("test@")]
    public void Should_Fail_When_Email_Format_Is_Invalid(string email)
    {
        // Arrange
        var command = new LoginCommand(
            Email: email,
            Password: "ValidPassword123!"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email must be a valid email address");
    }

    [Fact]
    public void Should_Fail_When_Email_Exceeds_Maximum_Length()
    {
        // Arrange
        var longEmail = new string('a', 250) + "@test.com"; // 259 characters
        var command = new LoginCommand(
            Email: longEmail,
            Password: "ValidPassword123!"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email must not exceed 255 characters");
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_When_Password_Is_Empty(string? password)
    {
        // Arrange
        var command = new LoginCommand(
            Email: "test@example.com",
            Password: password!
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password is required");
    }

    [Theory]
    [InlineData("short")]
    [InlineData("1234567")]
    public void Should_Fail_When_Password_Is_Too_Short(string password)
    {
        // Arrange
        var command = new LoginCommand(
            Email: "test@example.com",
            Password: password
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at least 8 characters");
    }

    [Fact]
    public void Should_Pass_With_Valid_Optional_Fields()
    {
        // Arrange
        var command = new LoginCommand(
            Email: "test@example.com",
            Password: "ValidPassword123!",
            IpAddress: "192.168.1.1",
            UserAgent: "Mozilla/5.0"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Pass_With_Null_Optional_Fields()
    {
        // Arrange
        var command = new LoginCommand(
            Email: "test@example.com",
            Password: "ValidPassword123!",
            IpAddress: null,
            UserAgent: null
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Fail_With_Multiple_Validation_Errors()
    {
        // Arrange
        var command = new LoginCommand(
            Email: "invalidemail",
            Password: "short"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
    }
}

