using Api.BoundedContexts.Authentication.Application.Commands.EmailVerification;
using Api.BoundedContexts.Authentication.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Validators.EmailVerification;

/// <summary>
/// Unit tests for ResendVerificationCommandValidator.
/// ISSUE-3071: Email verification backend implementation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
public class ResendVerificationCommandValidatorTests
{
    private readonly ResendVerificationCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidEmail_ShouldNotHaveErrors()
    {
        // Arrange
        var command = new ResendVerificationCommand { Email = "test@example.com" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyEmail_ShouldHaveError()
    {
        // Arrange
        var command = new ResendVerificationCommand { Email = "" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email is required");
    }

    [Fact]
    public void Validate_WithInvalidEmailFormat_ShouldHaveError()
    {
        // Arrange
        var command = new ResendVerificationCommand { Email = "not-an-email" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Invalid email format");
    }

    [Fact]
    public void Validate_WithTooLongEmail_ShouldHaveError()
    {
        // Arrange
        var command = new ResendVerificationCommand { Email = $"{new string('a', 250)}@example.com" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email)
            .WithErrorMessage("Email must not exceed 255 characters");
    }

    [Theory]
    [InlineData("test@example.com")]
    [InlineData("user.name@domain.co.uk")]
    [InlineData("test123@test-domain.org")]
    public void Validate_WithValidEmailFormats_ShouldNotHaveErrors(string email)
    {
        // Arrange
        var command = new ResendVerificationCommand { Email = email };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("plaintext")]
    [InlineData("@missing-local-part.com")]
    [InlineData("missing-at-sign.com")]
    [InlineData("double@@at.com")]
    public void Validate_WithInvalidEmailFormats_ShouldHaveErrors(string email)
    {
        // Arrange
        var command = new ResendVerificationCommand { Email = email };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
