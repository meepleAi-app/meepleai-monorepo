using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class AcceptMechanicDraftCommandValidatorTests
{
    private readonly AcceptMechanicDraftCommandValidator _validator;

    public AcceptMechanicDraftCommandValidatorTests()
    {
        _validator = new AcceptMechanicDraftCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        // Arrange
        var command = new AcceptMechanicDraftCommand(
            Guid.NewGuid(), "summary", "AI-generated summary text");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyDraftId_FailsValidation()
    {
        // Arrange
        var command = new AcceptMechanicDraftCommand(
            Guid.Empty, "summary", "Draft content");

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
        var command = new AcceptMechanicDraftCommand(
            Guid.NewGuid(), "", "Draft content");

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
        var command = new AcceptMechanicDraftCommand(
            Guid.NewGuid(), section, "Draft content");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveValidationErrorFor(x => x.Section);
    }

    [Theory]
    [InlineData("invalid")]
    [InlineData("gameplay")]
    public void Validate_WithInvalidSection_FailsValidation(string section)
    {
        // Arrange
        var command = new AcceptMechanicDraftCommand(
            Guid.NewGuid(), section, "Draft content");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.Section)
            .WithErrorMessage("Section must be one of: summary, mechanics, victory, resources, phases, questions");
    }

    [Fact]
    public void Validate_WithEmptyAcceptedDraft_FailsValidation()
    {
        // Arrange
        var command = new AcceptMechanicDraftCommand(
            Guid.NewGuid(), "summary", "");

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.AcceptedDraft)
            .WithErrorMessage("Accepted draft content is required");
    }
}
