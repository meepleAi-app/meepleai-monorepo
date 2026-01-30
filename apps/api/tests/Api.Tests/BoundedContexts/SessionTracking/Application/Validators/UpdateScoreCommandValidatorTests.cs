using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Validators;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class UpdateScoreCommandValidatorTests
{
    private readonly UpdateScoreCommandValidator _validator = new();

    [Fact]
    public void Validate_WithValidCommand_ShouldPass()
    {
        // Arrange
        var command = new UpdateScoreCommand(
            SessionId: Guid.NewGuid(),
            ParticipantId: Guid.NewGuid(),
            RoundNumber: 1,
            Category: "Main",
            ScoreValue: 50
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeTrue();
    }

    [Fact]
    public void Validate_WithEmptySessionId_ShouldFail()
    {
        // Arrange
        var command = new UpdateScoreCommand(
            SessionId: Guid.Empty,
            ParticipantId: Guid.NewGuid(),
            RoundNumber: null,
            Category: null,
            ScoreValue: 50
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.SessionId));
    }

    [Fact]
    public void Validate_WithScoreValueOutOfRange_ShouldFail()
    {
        // Arrange
        var command = new UpdateScoreCommand(
            SessionId: Guid.NewGuid(),
            ParticipantId: Guid.NewGuid(),
            RoundNumber: null,
            Category: null,
            ScoreValue: 100000 // exceeds max
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.ScoreValue));
    }

    [Fact]
    public void Validate_WithNegativeRoundNumber_ShouldFail()
    {
        // Arrange
        var command = new UpdateScoreCommand(
            SessionId: Guid.NewGuid(),
            ParticipantId: Guid.NewGuid(),
            RoundNumber: 0, // invalid
            Category: null,
            ScoreValue: 50
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.RoundNumber));
    }

    [Fact]
    public void Validate_WithCategoryTooLong_ShouldFail()
    {
        // Arrange
        var command = new UpdateScoreCommand(
            SessionId: Guid.NewGuid(),
            ParticipantId: Guid.NewGuid(),
            RoundNumber: null,
            Category: new string('A', 51), // exceeds 50 chars
            ScoreValue: 50
        );

        // Act
        var result = _validator.Validate(command);

        // Assert
        result.IsValid.Should().BeFalse();
        result.Errors.Should().Contain(e => e.PropertyName == nameof(command.Category));
    }
}
