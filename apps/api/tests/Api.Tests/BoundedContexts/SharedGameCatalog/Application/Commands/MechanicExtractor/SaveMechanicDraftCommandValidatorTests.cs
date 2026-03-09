using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SaveMechanicDraftCommandValidatorTests
{
    private readonly SaveMechanicDraftCommandValidator _validator;

    public SaveMechanicDraftCommandValidatorTests()
    {
        _validator = new SaveMechanicDraftCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        // Arrange
        var command = new SaveMechanicDraftCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", Guid.NewGuid(),
            "summary", "mechanics", "victory", "resources", "phases", "questions");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptySharedGameId_FailsValidation()
    {
        // Arrange
        var command = new SaveMechanicDraftCommand(
            Guid.Empty, Guid.NewGuid(), "Catan", Guid.NewGuid(),
            "", "", "", "", "", "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SharedGameId)
            .WithErrorMessage("Shared game ID is required");
    }

    [Fact]
    public void Validate_WithEmptyPdfDocumentId_FailsValidation()
    {
        // Arrange
        var command = new SaveMechanicDraftCommand(
            Guid.NewGuid(), Guid.Empty, "Catan", Guid.NewGuid(),
            "", "", "", "", "", "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.PdfDocumentId)
            .WithErrorMessage("PDF document ID is required");
    }

    [Fact]
    public void Validate_WithEmptyGameTitle_FailsValidation()
    {
        // Arrange
        var command = new SaveMechanicDraftCommand(
            Guid.NewGuid(), Guid.NewGuid(), "", Guid.NewGuid(),
            "", "", "", "", "", "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GameTitle)
            .WithErrorMessage("Game title is required");
    }

    [Fact]
    public void Validate_WithTooLongGameTitle_FailsValidation()
    {
        // Arrange
        var command = new SaveMechanicDraftCommand(
            Guid.NewGuid(), Guid.NewGuid(), new string('A', 301), Guid.NewGuid(),
            "", "", "", "", "", "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GameTitle)
            .WithErrorMessage("Game title cannot exceed 300 characters");
    }

    [Fact]
    public void Validate_WithEmptyUserId_FailsValidation()
    {
        // Arrange
        var command = new SaveMechanicDraftCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", Guid.Empty,
            "", "", "", "", "", "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("User ID is required");
    }

    [Fact]
    public void Validate_WithEmptyNotes_PassesValidation()
    {
        // Notes can be empty (auto-save scenario)
        var command = new SaveMechanicDraftCommand(
            Guid.NewGuid(), Guid.NewGuid(), "Catan", Guid.NewGuid(),
            "", "", "", "", "", "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
