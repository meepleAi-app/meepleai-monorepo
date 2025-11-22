using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Validators;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Unit tests for RegisterCommandValidator.
/// Issue #1449: FluentValidation for Authentication CQRS pipeline
/// </summary>
public sealed class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void Should_Pass_When_All_Required_Fields_Are_Valid()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "ValidPassword123!",
            DisplayName: "Test User"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #region Email Validation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_When_Email_Is_Empty(string? email)
    {
        // Arrange
        var command = new RegisterCommand(
            Email: email!,
            Password: "ValidPassword123!",
            DisplayName: "Test User"
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
        var command = new RegisterCommand(
            Email: email,
            Password: "ValidPassword123!",
            DisplayName: "Test User"
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
        var longEmail = new string('a', 250) + "@test.com";
        var command = new RegisterCommand(
            Email: longEmail,
            Password: "ValidPassword123!",
            DisplayName: "Test User"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    #endregion

    #region Password Validation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_When_Password_Is_Empty(string? password)
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: password!,
            DisplayName: "Test User"
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
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: password,
            DisplayName: "Test User"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must be at least 8 characters");
    }

    [Fact]
    public void Should_Fail_When_Password_Exceeds_Maximum_Length()
    {
        // Arrange
        var longPassword = new string('a', 130) + "A1!";
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: longPassword,
            DisplayName: "Test User"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must not exceed 128 characters");
    }

    [Fact]
    public void Should_Fail_When_Password_Has_No_Uppercase()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "password123!",
            DisplayName: "Test User"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one uppercase letter");
    }

    [Fact]
    public void Should_Fail_When_Password_Has_No_Lowercase()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "PASSWORD123!",
            DisplayName: "Test User"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one lowercase letter");
    }

    [Fact]
    public void Should_Fail_When_Password_Has_No_Digit()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "PasswordTest!",
            DisplayName: "Test User"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one digit");
    }

    [Fact]
    public void Should_Fail_When_Password_Has_No_Special_Character()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "PasswordTest123",
            DisplayName: "Test User"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one special character");
    }

    #endregion

    #region DisplayName Validation

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_When_DisplayName_Is_Empty(string? displayName)
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "ValidPassword123!",
            DisplayName: displayName!
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
            .WithErrorMessage("Display name is required");
    }

    [Fact]
    public void Should_Fail_When_DisplayName_Is_Too_Short()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "ValidPassword123!",
            DisplayName: "A"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
            .WithErrorMessage("Display name must be at least 2 characters");
    }

    [Fact]
    public void Should_Fail_When_DisplayName_Exceeds_Maximum_Length()
    {
        // Arrange
        var longName = new string('a', 101);
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "ValidPassword123!",
            DisplayName: longName
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
            .WithErrorMessage("Display name must not exceed 100 characters");
    }

    [Theory]
    [InlineData("Valid_Name")]
    [InlineData("Valid-Name")]
    [InlineData("Valid.Name")]
    [InlineData("Valid Name")]
    [InlineData("Valid123")]
    public void Should_Pass_When_DisplayName_Has_Valid_Characters(string displayName)
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "ValidPassword123!",
            DisplayName: displayName
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }

    [Theory]
    [InlineData("Invalid@Name")]
    [InlineData("Invalid#Name")]
    [InlineData("Invalid$Name")]
    [InlineData("Invalid%Name")]
    public void Should_Fail_When_DisplayName_Has_Invalid_Characters(string displayName)
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "ValidPassword123!",
            DisplayName: displayName
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
            .WithErrorMessage("Display name can only contain letters, numbers, spaces, hyphens, underscores, and periods");
    }

    #endregion

    #region Role Validation

    [Theory]
    [InlineData("user")]
    [InlineData("editor")]
    [InlineData("admin")]
    [InlineData("USER")]
    [InlineData("EDITOR")]
    [InlineData("ADMIN")]
    public void Should_Pass_When_Role_Is_Valid(string role)
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "ValidPassword123!",
            DisplayName: "Test User",
            Role: role
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Role);
    }

    [Fact]
    public void Should_Pass_When_Role_Is_Null()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "ValidPassword123!",
            DisplayName: "Test User",
            Role: null
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Role);
    }

    [Theory]
    [InlineData("superadmin")]
    [InlineData("moderator")]
    [InlineData("guest")]
    public void Should_Fail_When_Role_Is_Invalid(string role)
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "ValidPassword123!",
            DisplayName: "Test User",
            Role: role
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Role)
            .WithErrorMessage("Role must be one of: user, editor, admin");
    }

    #endregion

    [Fact]
    public void Should_Fail_With_Multiple_Validation_Errors()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "invalidemail",
            Password: "weak",
            DisplayName: "A"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
        result.ShouldHaveValidationErrorFor(x => x.Password);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }
}