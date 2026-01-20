using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Tests for UpdateLibraryShareLinkCommandValidator.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class UpdateLibraryShareLinkCommandValidatorTests
{
    private readonly UpdateLibraryShareLinkCommandValidator _validator;

    public UpdateLibraryShareLinkCommandValidatorTests()
    {
        _validator = new UpdateLibraryShareLinkCommandValidator();
    }

    #region UserId Validation

    [Fact]
    public void Validate_WithEmptyUserId_ShouldFail()
    {
        // Arrange
        var command = new UpdateLibraryShareLinkCommand(
            Guid.Empty,
            "12345678901234567890123456789012",
            "public",
            null,
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
        var command = new UpdateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "12345678901234567890123456789012",
            null,
            null,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.UserId);
    }

    #endregion

    #region ShareToken Validation

    [Fact]
    public void Validate_WithEmptyShareToken_ShouldFail()
    {
        // Arrange
        var command = new UpdateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "",
            null,
            null,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ShareToken)
            .WithErrorMessage("ShareToken is required");
    }

    [Fact]
    public void Validate_WithNullShareToken_ShouldFail()
    {
        // Arrange
        var command = new UpdateLibraryShareLinkCommand(
            Guid.NewGuid(),
            null!,
            null,
            null,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ShareToken);
    }

    [Fact]
    public void Validate_WithShareTokenTooShort_ShouldFail()
    {
        // Arrange
        var command = new UpdateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "short",
            null,
            null,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ShareToken)
            .WithErrorMessage("ShareToken must be exactly 32 characters");
    }

    [Fact]
    public void Validate_WithShareTokenTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "123456789012345678901234567890123", // 33 characters
            null,
            null,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ShareToken)
            .WithErrorMessage("ShareToken must be exactly 32 characters");
    }

    [Fact]
    public void Validate_WithValidShareToken_ShouldPass()
    {
        // Arrange
        var command = new UpdateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "12345678901234567890123456789012", // Exactly 32 characters
            null,
            null,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.ShareToken);
    }

    #endregion

    #region PrivacyLevel Validation

    [Fact]
    public void Validate_WithNullPrivacyLevel_ShouldPass()
    {
        // Arrange
        var command = new UpdateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "12345678901234567890123456789012",
            null, // Optional field
            null,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PrivacyLevel);
    }

    [Fact]
    public void Validate_WithEmptyPrivacyLevel_ShouldPass()
    {
        // Arrange
        var command = new UpdateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "12345678901234567890123456789012",
            "", // Empty is treated as not provided
            null,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PrivacyLevel);
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
        var command = new UpdateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "12345678901234567890123456789012",
            privacyLevel,
            null,
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
        var command = new UpdateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "12345678901234567890123456789012",
            privacyLevel,
            null,
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
        var command = new UpdateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "12345678901234567890123456789012",
            null,
            null,
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
        var command = new UpdateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "12345678901234567890123456789012",
            null,
            null,
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
        var command = new UpdateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "12345678901234567890123456789012",
            null,
            null,
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
        var command = new UpdateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "12345678901234567890123456789012",
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
        var command = new UpdateLibraryShareLinkCommand(
            Guid.NewGuid(),
            "12345678901234567890123456789012",
            null,
            null,
            null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    #endregion
}
