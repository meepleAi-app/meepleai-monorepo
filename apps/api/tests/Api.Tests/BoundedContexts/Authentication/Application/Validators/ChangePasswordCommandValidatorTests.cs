using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Validators;
using FluentValidation.TestHelper;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Unit tests for ChangePasswordCommandValidator.
/// Issue #1449: FluentValidation for Authentication CQRS pipeline
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ChangePasswordCommandValidatorTests
{
    private readonly ChangePasswordCommandValidator _validator = new();

    [Fact]
    public void Should_Pass_When_All_Fields_Are_Valid()
    {
        // Arrange
        var command = new ChangePasswordCommand
        {
            UserId = Guid.NewGuid(),
            CurrentPassword = "CurrentPassword123!",
            NewPassword = "NewPassword123!"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
    [Fact]
    public void Should_Fail_When_UserId_Is_Empty()
    {
        // Arrange
        var command = new ChangePasswordCommand
        {
            UserId = Guid.Empty,
            CurrentPassword = "CurrentPassword123!",
            NewPassword = "NewPassword123!"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("User ID is required");
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_When_CurrentPassword_Is_Empty(string? currentPassword)
    {
        // Arrange
        var command = new ChangePasswordCommand
        {
            UserId = Guid.NewGuid(),
            CurrentPassword = currentPassword!,
            NewPassword = "NewPassword123!"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.CurrentPassword)
            .WithErrorMessage("Current password is required");
    }

    [Theory]
    [InlineData("short")]
    [InlineData("1234567")]
    public void Should_Fail_When_CurrentPassword_Is_Too_Short(string currentPassword)
    {
        // Arrange
        var command = new ChangePasswordCommand
        {
            UserId = Guid.NewGuid(),
            CurrentPassword = currentPassword,
            NewPassword = "NewPassword123!"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.CurrentPassword)
            .WithErrorMessage("Current password must be at least 8 characters");
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_When_NewPassword_Is_Empty(string? newPassword)
    {
        // Arrange
        var command = new ChangePasswordCommand
        {
            UserId = Guid.NewGuid(),
            CurrentPassword = "CurrentPassword123!",
            NewPassword = newPassword!
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("New password is required");
    }

    [Theory]
    [InlineData("short")]
    [InlineData("1234567")]
    public void Should_Fail_When_NewPassword_Is_Too_Short(string newPassword)
    {
        // Arrange
        var command = new ChangePasswordCommand
        {
            UserId = Guid.NewGuid(),
            CurrentPassword = "CurrentPassword123!",
            NewPassword = newPassword
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("New password must be at least 8 characters");
    }

    [Fact]
    public void Should_Fail_When_NewPassword_Exceeds_Maximum_Length()
    {
        // Arrange
        var longPassword = new string('a', 130) + "A1!";
        var command = new ChangePasswordCommand
        {
            UserId = Guid.NewGuid(),
            CurrentPassword = "CurrentPassword123!",
            NewPassword = longPassword
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("New password must not exceed 128 characters");
    }

    [Fact]
    public void Should_Fail_When_NewPassword_Has_No_Uppercase()
    {
        // Arrange
        var command = new ChangePasswordCommand
        {
            UserId = Guid.NewGuid(),
            CurrentPassword = "CurrentPassword123!",
            NewPassword = "password123!"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("New password must contain at least one uppercase letter");
    }

    [Fact]
    public void Should_Fail_When_NewPassword_Has_No_Lowercase()
    {
        // Arrange
        var command = new ChangePasswordCommand
        {
            UserId = Guid.NewGuid(),
            CurrentPassword = "CurrentPassword123!",
            NewPassword = "PASSWORD123!"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("New password must contain at least one lowercase letter");
    }

    [Fact]
    public void Should_Fail_When_NewPassword_Has_No_Digit()
    {
        // Arrange
        var command = new ChangePasswordCommand
        {
            UserId = Guid.NewGuid(),
            CurrentPassword = "CurrentPassword123!",
            NewPassword = "PasswordTest!"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("New password must contain at least one digit");
    }

    [Fact]
    public void Should_Fail_When_NewPassword_Has_No_Special_Character()
    {
        // Arrange
        var command = new ChangePasswordCommand
        {
            UserId = Guid.NewGuid(),
            CurrentPassword = "CurrentPassword123!",
            NewPassword = "PasswordTest123"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("New password must contain at least one special character");
    }
    [Fact]
    public void Should_Fail_When_NewPassword_Is_Same_As_CurrentPassword()
    {
        // Arrange
        var samePassword = "SamePassword123!";
        var command = new ChangePasswordCommand
        {
            UserId = Guid.NewGuid(),
            CurrentPassword = samePassword,
            NewPassword = samePassword
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("New password must be different from current password");
    }

    [Fact]
    public void Should_Pass_When_NewPassword_Is_Different_From_CurrentPassword()
    {
        // Arrange
        var command = new ChangePasswordCommand
        {
            UserId = Guid.NewGuid(),
            CurrentPassword = "CurrentPassword123!",
            NewPassword = "NewPassword123!"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
    [Fact]
    public void Should_Fail_With_Multiple_Validation_Errors()
    {
        // Arrange
        var command = new ChangePasswordCommand
        {
            UserId = Guid.Empty,
            CurrentPassword = "short",
            NewPassword = "weak"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
        result.ShouldHaveValidationErrorFor(x => x.CurrentPassword);
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }
}