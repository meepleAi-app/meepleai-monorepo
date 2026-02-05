using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Tests for RemovePrivatePdfCommandValidator.
/// Issue #3651: Validates the private PDF removal command.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class RemovePrivatePdfCommandValidatorTests
{
    private readonly RemovePrivatePdfCommandValidator _validator;

    public RemovePrivatePdfCommandValidatorTests()
    {
        _validator = new RemovePrivatePdfCommandValidator();
    }

    #region UserId Validation

    [Fact]
    public void Validate_WithEmptyUserId_ShouldFail()
    {
        // Arrange
        var command = new RemovePrivatePdfCommand(Guid.Empty, Guid.NewGuid());

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
        var command = new RemovePrivatePdfCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.UserId);
    }

    #endregion

    #region EntryId Validation

    [Fact]
    public void Validate_WithEmptyEntryId_ShouldFail()
    {
        // Arrange
        var command = new RemovePrivatePdfCommand(Guid.NewGuid(), Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EntryId)
            .WithErrorMessage("EntryId is required");
    }

    [Fact]
    public void Validate_WithValidEntryId_ShouldPass()
    {
        // Arrange
        var command = new RemovePrivatePdfCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.EntryId);
    }

    #endregion

    #region Complete Validation

    [Fact]
    public void Validate_WithAllEmptyGuids_ShouldFailBothValidations()
    {
        // Arrange
        var command = new RemovePrivatePdfCommand(Guid.Empty, Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId);
        result.ShouldHaveValidationErrorFor(x => x.EntryId);
    }

    [Fact]
    public void Validate_WithValidCommand_ShouldPassAll()
    {
        // Arrange
        var command = new RemovePrivatePdfCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    #endregion
}
