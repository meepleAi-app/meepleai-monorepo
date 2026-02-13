using Api.BoundedContexts.UserLibrary.Application.Commands;
using Api.BoundedContexts.UserLibrary.Application.Validators;
using Api.BoundedContexts.UserLibrary.Domain.Enums;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.UserLibrary.Application.Validators;

/// <summary>
/// Unit tests for AddToCollectionCommandValidator.
/// Issue #4263: Phase 2 - Generic UserCollection System
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "UserLibrary")]
public class AddToCollectionCommandValidatorTests
{
    private readonly AddToCollectionCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        // Arrange
        var command = new AddToCollectionCommand(
            Guid.NewGuid(),
            EntityType.Player,
            Guid.NewGuid(),
            false,
            "Test notes"
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyUserId_FailsValidation()
    {
        // Arrange
        var command = new AddToCollectionCommand(
            Guid.Empty,
            EntityType.Player,
            Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("User ID is required");
    }

    [Fact]
    public void Validate_WithEmptyEntityId_FailsValidation()
    {
        // Arrange
        var command = new AddToCollectionCommand(
            Guid.NewGuid(),
            EntityType.Event,
            Guid.Empty
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EntityId)
            .WithErrorMessage("Entity ID is required");
    }

    [Theory]
    [InlineData((EntityType)999)]
    [InlineData((EntityType)(-1))]
    public void Validate_WithInvalidEntityType_FailsValidation(EntityType invalidType)
    {
        // Arrange
        var command = new AddToCollectionCommand(
            Guid.NewGuid(),
            invalidType,
            Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EntityType)
            .WithErrorMessage("Invalid entity type");
    }

    [Fact]
    public void Validate_WithNotesExceeding2000Characters_FailsValidation()
    {
        // Arrange
        var longNotes = new string('A', 2001);
        var command = new AddToCollectionCommand(
            Guid.NewGuid(),
            EntityType.Agent,
            Guid.NewGuid(),
            false,
            longNotes
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Notes)
            .WithErrorMessage("Notes cannot exceed 2000 characters");
    }

    [Fact]
    public void Validate_WithNotesAt2000Characters_PassesValidation()
    {
        // Arrange
        var maxNotes = new string('A', 2000);
        var command = new AddToCollectionCommand(
            Guid.NewGuid(),
            EntityType.Document,
            Guid.NewGuid(),
            false,
            maxNotes
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }

    [Fact]
    public void Validate_WithNullNotes_PassesValidation()
    {
        // Arrange
        var command = new AddToCollectionCommand(
            Guid.NewGuid(),
            EntityType.Session,
            Guid.NewGuid(),
            false,
            null
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Notes);
    }

    [Theory]
    [InlineData(EntityType.Game)]
    [InlineData(EntityType.Player)]
    [InlineData(EntityType.Event)]
    [InlineData(EntityType.Session)]
    [InlineData(EntityType.Agent)]
    [InlineData(EntityType.Document)]
    [InlineData(EntityType.ChatSession)]
    public void Validate_WithAllValidEntityTypes_PassesValidation(EntityType entityType)
    {
        // Arrange
        var command = new AddToCollectionCommand(
            Guid.NewGuid(),
            entityType,
            Guid.NewGuid()
        );

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
