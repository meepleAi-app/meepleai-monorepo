using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Tests for CreateLibraryShareLinkCommandValidator.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CreateLibraryShareLinkCommandValidatorTests
{
    private readonly CreateLibraryShareLinkCommandValidator _validator;

    public CreateLibraryShareLinkCommandValidatorTests()
    {
        _validator = new CreateLibraryShareLinkCommandValidator();
    }

    #region UserId Validation

    [Fact]
    public void Validate_WithEmptyUserId_ShouldFail()
    {
        // Arrange
        var command = new CreateLibraryShareLinkCommand(
            Guid.Empty,
            "public",
            true,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }

    [Fact]
    public void Validate_WithValidUserId_ShouldPass()
    {
        // Arrange
        var command = new CreateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "public",
            true,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.UserId);
    }

    #endregion

    #region PrivacyLevel Validation

    [Fact]
    public void Validate_WithEmptyPrivacyLevel_ShouldFail()
    {
        // Arrange
        var command = new CreateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "",
            true,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PrivacyLevel)
            .WithErrorMessage("PrivacyLevel is required");
    }

    [Fact]
    public void Validate_WithNullPrivacyLevel_ShouldFail()
    {
        // Arrange
        var command = new CreateLibraryShareLinkCommand(
            Guid.NewGuid(),
            null!,
            true,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PrivacyLevel);
    }

    [Theory]
    [InlineData("public")]
    [InlineData("PUBLIC")]
    [InlineData("Public")]
    [InlineData("unlisted")]
    [InlineData("UNLISTED")]
    [InlineData("Unlisted")]
    public void Validate_WithValidPrivacyLevel_ShouldPass(string privacyLevel)
    {
        // Arrange
        var command = new CreateLibraryShareLinkCommand(
            Guid.NewGuid(),
            privacyLevel,
            true,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PrivacyLevel);
    }

    [Theory]
    [InlineData("private")]
    [InlineData("secret")]
    [InlineData("invalid")]
    [InlineData("123")]
    public void Validate_WithInvalidPrivacyLevel_ShouldFail(string privacyLevel)
    {
        // Arrange
        var command = new CreateLibraryShareLinkCommand(
            Guid.NewGuid(),
            privacyLevel,
            true,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PrivacyLevel)
            .WithErrorMessage("PrivacyLevel must be 'public' or 'unlisted'");
    }

    #endregion

    #region ExpiresAt Validation

    [Fact]
    public void Validate_WithNullExpiresAt_ShouldPass()
    {
        // Arrange
        var command = new CreateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "public",
            true,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ExpiresAt);
    }

    [Fact]
    public void Validate_WithFutureExpiresAt_ShouldPass()
    {
        // Arrange
        var command = new CreateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "public",
            true,
            DateTime.UtcNow.AddDays(7));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ExpiresAt);
    }

    [Fact]
    public void Validate_WithPastExpiresAt_ShouldFail()
    {
        // Arrange
        var command = new CreateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "public",
            true,
            DateTime.UtcNow.AddDays(-1));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ExpiresAt)
            .WithErrorMessage("ExpiresAt must be in the future");
    }

    #endregion

    #region Valid Command

    [Fact]
    public void Validate_WithAllValidFields_ShouldPass()
    {
        // Arrange
        var command = new CreateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "public",
            true,
            DateTime.UtcNow.AddDays(30));

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithMinimalValidFields_ShouldPass()
    {
        // Arrange
        var command = new CreateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "unlisted",
            false,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}
