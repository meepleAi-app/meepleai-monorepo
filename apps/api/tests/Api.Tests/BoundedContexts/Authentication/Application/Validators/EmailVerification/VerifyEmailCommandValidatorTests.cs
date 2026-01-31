using Api.BoundedContexts.Authentication.Application.Commands.EmailVerification;
using Api.BoundedContexts.Authentication.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Validators.EmailVerification;

/// <summary>
/// Unit tests for VerifyEmailCommandValidator.
/// ISSUE-3071: Email verification backend implementation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class VerifyEmailCommandValidatorTests
{
    private readonly VerifyEmailCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidToken_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new VerifyEmailCommand { Token = "valid-token-1234567890" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyToken_ShouldHaveError()
    {
        // Arrange
        var command = new VerifyEmailCommand { Token = "" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Token)
            .WithErrorMessage("Verification token is required");
    }

    [Fact]
    public void Validate_WithTooShortToken_ShouldHaveError()
    {
        // Arrange
        var command = new VerifyEmailCommand { Token = "short" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Token)
            .WithErrorMessage("Invalid verification token format");
    }

    [Fact]
    public void Validate_WithTooLongToken_ShouldHaveError()
    {
        // Arrange
        var command = new VerifyEmailCommand { Token = new string('a', 300) };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Token)
            .WithErrorMessage("Invalid verification token format");
    }

    [Theory]
    [InlineData("valid-token-1234567890")]
    [InlineData("ABCD1234567890EFGH")]
    [InlineData("token_with_underscore")]
    public void Validate_WithValidTokenFormats_ShouldNotHaveErrors(string token)
    {
        // Arrange
        var command = new VerifyEmailCommand { Token = token };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
