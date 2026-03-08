using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class AiAssistMechanicDraftCommandValidatorTests
{
    private readonly AiAssistMechanicDraftCommandValidator _validator;

    public AiAssistMechanicDraftCommandValidatorTests()
    {
        _validator = new AiAssistMechanicDraftCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        // Arrange
        var command = new AiAssistMechanicDraftCommand(
            Guid.NewGuid(), "summary", "This is a detailed note about the game summary section", "Catan");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyDraftId_FailsValidation()
    {
        // Arrange
        var command = new AiAssistMechanicDraftCommand(
            Guid.Empty, "summary", "Detailed notes here", "Catan");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DraftId)
            .WithErrorMessage("Draft ID is required");
    }

    [Fact]
    public void Validate_WithEmptySection_FailsValidation()
    {
        // Arrange
        var command = new AiAssistMechanicDraftCommand(
            Guid.NewGuid(), "", "Detailed notes here", "Catan");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Section);
    }

    [Theory]
    [InlineData("summary")]
    [InlineData("mechanics")]
    [InlineData("victory")]
    [InlineData("resources")]
    [InlineData("phases")]
    [InlineData("questions")]
    public void Validate_WithValidSection_PassesValidation(string section)
    {
        // Arrange
        var command = new AiAssistMechanicDraftCommand(
            Guid.NewGuid(), section, "Detailed notes about this section", "Catan");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Section);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("overview")]
    [InlineData("rules")]
    public void Validate_WithInvalidSection_FailsValidation(string section)
    {
        // Arrange
        var command = new AiAssistMechanicDraftCommand(
            Guid.NewGuid(), section, "Detailed notes here", "Catan");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Section)
            .WithErrorMessage("Section must be one of: summary, mechanics, victory, resources, phases, questions");
    }

    [Fact]
    public void Validate_WithEmptyHumanNotes_FailsValidation()
    {
        // Arrange
        var command = new AiAssistMechanicDraftCommand(
            Guid.NewGuid(), "summary", "", "Catan");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.HumanNotes)
            .WithErrorMessage("Human notes are required for AI assistance");
    }

    [Fact]
    public void Validate_WithTooShortHumanNotes_FailsValidation()
    {
        // Arrange
        var command = new AiAssistMechanicDraftCommand(
            Guid.NewGuid(), "summary", "Short", "Catan");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.HumanNotes)
            .WithErrorMessage("Notes must be at least 10 characters for meaningful AI assistance");
    }

    [Fact]
    public void Validate_WithExactly10CharNotes_PassesValidation()
    {
        // Arrange
        var command = new AiAssistMechanicDraftCommand(
            Guid.NewGuid(), "summary", "1234567890", "Catan");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.HumanNotes);
    }

    [Fact]
    public void Validate_WithEmptyGameTitle_FailsValidation()
    {
        // Arrange
        var command = new AiAssistMechanicDraftCommand(
            Guid.NewGuid(), "summary", "Detailed notes here", "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GameTitle)
            .WithErrorMessage("Game title is required");
    }
}
