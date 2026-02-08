using Api.BoundedContexts.UserLibrary.Application.Commands.PrivateGames;
using Api.BoundedContexts.UserLibrary.Application.Validators.PrivateGames;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Validators.PrivateGames;

/// <summary>
/// Unit tests for DeletePrivateGameCommandValidator.
/// Issue #3670: Phase 9 - Testing &amp; Polish.
/// Tests all validation rules for deleting private games.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public sealed class DeletePrivateGameCommandValidatorTests
{
    private readonly DeletePrivateGameCommandValidator _validator;

    public DeletePrivateGameCommandValidatorTests()
    {
        _validator = new DeletePrivateGameCommandValidator();
    }

    #region PrivateGameId Validation

    [Fact]
    public void PrivateGameId_Empty_HasValidationError()
    {
        // Arrange
        var command = new DeletePrivateGameCommand(Guid.Empty, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PrivateGameId)
            .WithErrorMessage("PrivateGameId is required");
    }

    [Fact]
    public void PrivateGameId_Valid_NoValidationError()
    {
        // Arrange
        var command = new DeletePrivateGameCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.PrivateGameId);
    }

    #endregion

    #region UserId Validation

    [Fact]
    public void UserId_Empty_HasValidationError()
    {
        // Arrange
        var command = new DeletePrivateGameCommand(Guid.NewGuid(), Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("UserId is required");
    }

    [Fact]
    public void UserId_Valid_NoValidationError()
    {
        // Arrange
        var command = new DeletePrivateGameCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.UserId);
    }

    #endregion

    #region Full Valid Command Tests

    [Fact]
    public void ValidDeleteCommand_NoValidationErrors()
    {
        // Arrange
        var command = new DeletePrivateGameCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void BothFieldsEmpty_HasMultipleValidationErrors()
    {
        // Arrange
        var command = new DeletePrivateGameCommand(Guid.Empty, Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PrivateGameId);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
        result.Errors.Should().HaveCount(2);
    }

    #endregion
}
