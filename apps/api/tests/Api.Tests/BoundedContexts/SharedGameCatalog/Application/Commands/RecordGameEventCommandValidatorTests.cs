using Api.BoundedContexts.SharedGameCatalog.Application.Commands.RecordGameEvent;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands;

[Trait("Category", TestCategories.Unit)]
public class RecordGameEventCommandValidatorTests
{
    private readonly RecordGameEventCommandValidator _validator;

    public RecordGameEventCommandValidatorTests()
    {
        _validator = new RecordGameEventCommandValidator();
    }

    [Fact]
    public void Validate_WithValidCommand_PassesValidation()
    {
        // Arrange
        var command = new RecordGameEventCommand(Guid.NewGuid(), GameEventType.View, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WithEmptyGameId_FailsValidation()
    {
        // Arrange
        var command = new RecordGameEventCommand(Guid.Empty, GameEventType.View);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.GameId)
            .WithErrorMessage("Game ID is required.");
    }

    [Fact]
    public void Validate_WithInvalidEventType_FailsValidation()
    {
        // Arrange
        var command = new RecordGameEventCommand(Guid.NewGuid(), (GameEventType)99);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.EventType)
            .WithErrorMessage("Invalid event type.");
    }

    [Fact]
    public void Validate_WithNullUserId_PassesValidation()
    {
        // Arrange
        var command = new RecordGameEventCommand(Guid.NewGuid(), GameEventType.Search, null);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData(GameEventType.Search)]
    [InlineData(GameEventType.View)]
    [InlineData(GameEventType.LibraryAdd)]
    [InlineData(GameEventType.Play)]
    public void Validate_AllValidEventTypes_PassValidation(GameEventType eventType)
    {
        // Arrange
        var command = new RecordGameEventCommand(Guid.NewGuid(), eventType);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }
}
