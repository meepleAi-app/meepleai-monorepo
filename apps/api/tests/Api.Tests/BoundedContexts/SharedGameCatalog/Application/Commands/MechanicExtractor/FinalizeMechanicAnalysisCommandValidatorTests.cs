using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class FinalizeMechanicAnalysisCommandValidatorTests
{
    private readonly FinalizeMechanicAnalysisCommandValidator _validator;

    public FinalizeMechanicAnalysisCommandValidatorTests()
    {
        _validator = new FinalizeMechanicAnalysisCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        // Arrange
        var command = new FinalizeMechanicAnalysisCommand(Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyDraftId_FailsValidation()
    {
        // Arrange
        var command = new FinalizeMechanicAnalysisCommand(Guid.Empty, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.DraftId)
            .WithErrorMessage("Draft ID is required");
    }

    [Fact]
    public void Validate_WithEmptyUserId_FailsValidation()
    {
        // Arrange
        var command = new FinalizeMechanicAnalysisCommand(Guid.NewGuid(), Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.UserId)
            .WithErrorMessage("User ID is required");
    }
}
