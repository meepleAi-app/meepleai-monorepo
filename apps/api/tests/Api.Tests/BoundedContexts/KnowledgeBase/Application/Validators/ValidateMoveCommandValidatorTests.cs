using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Validators;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Tests for ValidateMoveCommandValidator.
/// Issue #3760: Arbitro Agent Move Validation Logic.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "KnowledgeBase")]
[Trait("Issue", "3760")]
public class ValidateMoveCommandValidatorTests
{
    private readonly ValidateMoveCommandValidator _validator;

    public ValidateMoveCommandValidatorTests()
    {
        _validator = new ValidateMoveCommandValidator();
    }

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = new ValidateMoveCommand
        {
            GameSessionId = Guid.NewGuid(),
            PlayerName = "Alice",
            Action = "roll dice",
            Position = "A5",
            AdditionalContext = new Dictionary<string, string> { { "resource", "wood" } }
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_EmptyGameSessionId_ShouldFail()
    {
        // Arrange
        var command = new ValidateMoveCommand
        {
            GameSessionId = Guid.Empty,
            PlayerName = "Bob",
            Action = "move piece",
            Position = null,
            AdditionalContext = null
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "GameSessionId");
    }

    [Fact]
    public void Validate_EmptyPlayerName_ShouldFail()
    {
        // Arrange
        var command = new ValidateMoveCommand
        {
            GameSessionId = Guid.NewGuid(),
            PlayerName = "",
            Action = "roll dice",
            Position = null,
            AdditionalContext = null
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PlayerName");
    }

    [Fact]
    public void Validate_EmptyAction_ShouldFail()
    {
        // Arrange
        var command = new ValidateMoveCommand
        {
            GameSessionId = Guid.NewGuid(),
            PlayerName = "Charlie",
            Action = "",
            Position = null,
            AdditionalContext = null
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Action");
    }

    [Fact]
    public void Validate_PlayerNameTooLong_ShouldFail()
    {
        // Arrange
        var command = new ValidateMoveCommand
        {
            GameSessionId = Guid.NewGuid(),
            PlayerName = new string('a', 101), // 101 characters
            Action = "move",
            Position = null,
            AdditionalContext = null
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "PlayerName" && e.ErrorMessage.Contains("100"));
    }

    [Fact]
    public void Validate_ActionTooLong_ShouldFail()
    {
        // Arrange
        var command = new ValidateMoveCommand
        {
            GameSessionId = Guid.NewGuid(),
            PlayerName = "Diana",
            Action = new string('x', 201), // 201 characters
            Position = null,
            AdditionalContext = null
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Action" && e.ErrorMessage.Contains("200"));
    }

    [Fact]
    public void Validate_PositionTooLong_ShouldFail()
    {
        // Arrange
        var command = new ValidateMoveCommand
        {
            GameSessionId = Guid.NewGuid(),
            PlayerName = "Eve",
            Action = "move",
            Position = new string('y', 51), // 51 characters
            AdditionalContext = null
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "Position" && e.ErrorMessage.Contains("50"));
    }

    [Fact]
    public void Validate_AdditionalContextExceedsLimit_ShouldFail()
    {
        // Arrange
        var tooManyItems = Enumerable.Range(1, 21)
            .ToDictionary(i => $"key{i}", i => $"value{i}");

        var command = new ValidateMoveCommand
        {
            GameSessionId = Guid.NewGuid(),
            PlayerName = "Frank",
            Action = "complex move",
            Position = null,
            AdditionalContext = tooManyItems
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == "AdditionalContext" && e.ErrorMessage.Contains("20"));
    }

    [Fact]
    public void Validate_NullPosition_ShouldPass()
    {
        // Arrange
        var command = new ValidateMoveCommand
        {
            GameSessionId = Guid.NewGuid(),
            PlayerName = "Grace",
            Action = "end turn",
            Position = null,
            AdditionalContext = null
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_NullAdditionalContext_ShouldPass()
    {
        // Arrange
        var command = new ValidateMoveCommand
        {
            GameSessionId = Guid.NewGuid(),
            PlayerName = "Henry",
            Action = "draw card",
            Position = "deck",
            AdditionalContext = null
        };

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }
}
