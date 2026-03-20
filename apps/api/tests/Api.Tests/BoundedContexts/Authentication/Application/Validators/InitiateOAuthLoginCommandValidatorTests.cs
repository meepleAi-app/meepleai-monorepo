using Api.BoundedContexts.Authentication.Application.Commands.OAuth;
using Api.BoundedContexts.Authentication.Application.Validators;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.Authentication.Application.Validators;

/// <summary>
/// Unit tests for InitiateOAuthLoginCommandValidator.
/// Issue #2646: Code review fix - validator tests for provider validation.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Authentication")]
[Trait("Issue", "2646")]
public sealed class InitiateOAuthLoginCommandValidatorTests
{
    private readonly InitiateOAuthLoginCommandValidator _validator = new();

    [Theory]
    [InlineData("google")]
    [InlineData("github")]
    [InlineData("discord")]
    public void Should_Pass_When_Provider_Is_Supported(string provider)
    {
        // Arrange
        var command = new InitiateOAuthLoginCommand { Provider = provider };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("Google")]
    [InlineData("GITHUB")]
    [InlineData("DiScOrD")]
    public void Should_Pass_When_Provider_Is_CaseInsensitive(string provider)
    {
        // Arrange
        var command = new InitiateOAuthLoginCommand { Provider = provider };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Should_Fail_When_Provider_Is_Empty(string? provider)
    {
        // Arrange
        var command = new InitiateOAuthLoginCommand { Provider = provider! };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Provider)
            .WithErrorMessage("OAuth provider must be specified");
    }

    [Theory]
    [InlineData("facebook")]
    [InlineData("twitter")]
    [InlineData("apple")]
    [InlineData("microsoft")]
    public void Should_Fail_When_Provider_Is_Unsupported(string provider)
    {
        // Arrange
        var command = new InitiateOAuthLoginCommand { Provider = provider };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Provider)
            .WithErrorMessage($"Unsupported OAuth provider: {provider}. Supported providers: google, github, discord");
    }

    [Fact]
    public void Should_Pass_With_Optional_IpAddress()
    {
        // Arrange
        var command = new InitiateOAuthLoginCommand { Provider = "google", IpAddress = "192.168.1.1" };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Should_Pass_With_Null_IpAddress()
    {
        // Arrange
        var command = new InitiateOAuthLoginCommand { Provider = "google", IpAddress = null };

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}