using Api.BoundedContexts.Authentication.Application.Commands.PasswordReset;
using Api.BoundedContexts.Authentication.Application.Validators;
using FluentValidation.TestHelper;
using Xunit;
using Api.Tests.Constants;

namespace Api.Tests.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Unit tests for ResetPasswordCommandValidator.
/// Issue #1449: FluentValidation for Authentication CQRS pipeline
/// </summary>
[Trait("Category", TestCategories.Unit)]
public sealed class ResetPasswordCommandValidatorTests
{
    private readonly ResetPasswordCommandValidator _validator = new();

    [Fact]
    public void Should_Pass_When_All_Fields_Are_Valid()
    {
        // Arrange
        var command = new ResetPasswordCommand
        {
            Token = Guid.NewGuid().ToString(),
            NewPassword = "NewUnusualPwd123!"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    // Issue #2806: the reset token is a 32-byte secure random value encoded as
    // base64url by PasswordResetService (NOT a GUID). This is the exact shape of
    // every real token. The previous .Must(BeValidGuid) rule rejected all of them,
    // breaking password reset confirm for every user (HTTP 422). The validator MUST
    // accept a base64url token.
    [Theory]
    [InlineData("R6fJNEsX7k4lr8hHxADcWB81K1EDyH0qbW7CNFJYEJU")]
    [InlineData("abc123XYZ_-def456GHI789jklMNO012pqrSTU345vwx")]
    public void Should_Pass_When_Token_Is_Base64Url_From_Service(string token)
    {
        // Arrange
        var command = new ResetPasswordCommand
        {
            Token = token,
            NewPassword = "NewUnusualPwd123!"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Token);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_When_Token_Is_Empty(string? token)
    {
        // Arrange
        var command = new ResetPasswordCommand
        {
            Token = token!,
            NewPassword = "NewUnusualPwd123!"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Token)
            .WithErrorMessage("Reset token is required");
    }

    // Issue #2806: a GUID is still a non-empty string, so it continues to pass.
    // These document backward-compatibility, NOT a GUID requirement — the token
    // is base64url in practice (see Should_Pass_When_Token_Is_Base64Url_From_Service).
    [Fact]
    public void Should_Pass_When_Token_Is_Valid_Guid_With_Hyphens()
    {
        // Arrange
        var command = new ResetPasswordCommand
        {
            Token = "12345678-1234-1234-1234-123456789abc",
            NewPassword = "NewUnusualPwd123!"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Token);
    }

    [Fact]
    public void Should_Pass_When_Token_Is_Valid_Guid_Without_Hyphens()
    {
        // Arrange
        var command = new ResetPasswordCommand
        {
            Token = "12345678123412341234123456789abc",
            NewPassword = "NewUnusualPwd123!"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Token);
    }
    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_When_NewPassword_Is_Empty(string? newPassword)
    {
        // Arrange
        var command = new ResetPasswordCommand
        {
            Token = Guid.NewGuid().ToString(),
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
        var command = new ResetPasswordCommand
        {
            Token = Guid.NewGuid().ToString(),
            NewPassword = newPassword
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("New password must be at least 12 characters");
    }

    [Fact]
    public void Should_Fail_When_NewPassword_Exceeds_Maximum_Length()
    {
        // Arrange
        var longPassword = new string('a', 130) + "A1!";
        var command = new ResetPasswordCommand
        {
            Token = Guid.NewGuid().ToString(),
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
        var command = new ResetPasswordCommand
        {
            Token = Guid.NewGuid().ToString(),
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
        var command = new ResetPasswordCommand
        {
            Token = Guid.NewGuid().ToString(),
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
        var command = new ResetPasswordCommand
        {
            Token = Guid.NewGuid().ToString(),
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
        var command = new ResetPasswordCommand
        {
            Token = Guid.NewGuid().ToString(),
            NewPassword = "PasswordTest123"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("New password must contain at least one special character");
    }
    [Fact]
    public void Should_Fail_With_Multiple_Validation_Errors()
    {
        // Arrange
        var command = new ResetPasswordCommand
        {
            Token = "",
            NewPassword = "weak"
        };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Token);
        result.ShouldHaveValidationErrorFor(x => x.NewPassword);
    }
}
