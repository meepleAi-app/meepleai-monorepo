using Api.BoundedContexts.Authentication.Application.Commands;
using Api.BoundedContexts.Authentication.Application.Validators;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Validators;

/// <summary>
/// Unit tests for RegisterCommandValidator to ensure input sanitization and XSS protection.
///
/// ANTI-REGRESSION GUARD (Issue #2307):
/// These tests protect against validator registration failures that could allow XSS attacks.
/// If these tests fail, it indicates FluentValidation DI registration is broken.
/// Root cause: AddValidatorsFromAssemblyContaining() requires includeInternalTypes: true
/// for internal sealed class validators.
/// </summary>
public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator;

    public RegisterCommandValidatorTests()
    {
        _validator = new RegisterCommandValidator();
    }

    [Theory]
    [InlineData("<script>alert('xss')</script>")]
    [InlineData("<img src=x onerror=alert(1)>")]
    [InlineData("<svg onload=alert(1)>")]
    [InlineData("'; DROP TABLE users; --")]
    [InlineData("<iframe src='javascript:alert(1)'>")]
    public async Task DisplayName_WithHtmlTags_ShouldFail(string maliciousInput)
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "SecureP@ssw0rd123!",
            DisplayName: maliciousInput,
            Role: null,
            IpAddress: null,
            UserAgent: null
        );

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert - CRITICAL ANTI-XSS PROTECTION
        result.ShouldHaveValidationErrorFor(x => x.DisplayName)
            .WithErrorMessage("Display name can only contain letters, numbers, spaces, hyphens, underscores, and periods");
    }

    [Theory]
    [InlineData("ValidUser123")]
    [InlineData("User Name")]
    [InlineData("User-Name_123")]
    [InlineData("User.Name")]
    [InlineData("AB")] // Minimum length
    public async Task DisplayName_WithValidInput_ShouldPass(string validInput)
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "SecureP@ssw0rd123!",
            DisplayName: validInput,
            Role: null,
            IpAddress: null,
            UserAgent: null
        );

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.DisplayName);
    }

    [Theory]
    [InlineData("")] // Empty
    [InlineData(" ")] // Whitespace only
    [InlineData("A")] // Too short
    public async Task DisplayName_WithInvalidLength_ShouldFail(string invalidInput)
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "SecureP@ssw0rd123!",
            DisplayName: invalidInput,
            Role: null,
            IpAddress: null,
            UserAgent: null
        );

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public async Task Email_WithInvalidFormat_ShouldFail()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "not-an-email",
            Password: "SecureP@ssw0rd123!",
            DisplayName: "ValidName",
            Role: null,
            IpAddress: null,
            UserAgent: null
        );

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Email);
    }

    [Fact]
    public async Task Password_WithoutUppercase_ShouldFail()
    {
        // Arrange
        var command = new RegisterCommand(
            Email: "test@example.com",
            Password: "securep@ssw0rd123!",
            DisplayName: "ValidName",
            Role: null,
            IpAddress: null,
            UserAgent: null
        );

        // Act
        var result = await _validator.TestValidateAsync(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password must contain at least one uppercase letter");
    }
}
