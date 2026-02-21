using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Validators;

/// <summary>
/// Unit tests for player action command validators.
/// Issue #4765 - Player Action Endpoints + Host Validation
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class MarkPlayerReadyCommandValidatorTests
{
    private readonly MarkPlayerReadyCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = new MarkPlayerReadyCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptySessionId_ShouldFail()
    {
        // Arrange
        var command = new MarkPlayerReadyCommand(Guid.Empty, Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SessionId);
    }

    [Fact]
    public void Validate_EmptyParticipantId_ShouldFail()
    {
        // Arrange
        var command = new MarkPlayerReadyCommand(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ParticipantId);
    }

    [Fact]
    public void Validate_EmptyRequesterId_ShouldFail()
    {
        // Arrange
        var command = new MarkPlayerReadyCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RequesterId);
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class KickParticipantCommandValidatorTests
{
    private readonly KickParticipantCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        // Arrange
        var command = new KickParticipantCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptySessionId_ShouldFail()
    {
        // Arrange
        var command = new KickParticipantCommand(Guid.Empty, Guid.NewGuid(), Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.SessionId);
    }

    [Fact]
    public void Validate_EmptyParticipantId_ShouldFail()
    {
        // Arrange
        var command = new KickParticipantCommand(Guid.NewGuid(), Guid.Empty, Guid.NewGuid());

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.ParticipantId);
    }

    [Fact]
    public void Validate_EmptyRequesterId_ShouldFail()
    {
        // Arrange
        var command = new KickParticipantCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor(x => x.RequesterId);
    }

    [Fact]
    public void Validate_SelfKick_ShouldFail()
    {
        // Arrange - ParticipantId == RequesterId
        var sameId = Guid.NewGuid();
        var command = new KickParticipantCommand(Guid.NewGuid(), sameId, sameId);

        // Act
        var result = _validator.TestValidate(command);

        // Assert
        result.ShouldHaveValidationErrorFor("").WithErrorMessage("Cannot kick yourself");
    }
}

// ============================================================================
// Issue #4765 – New player action validators
// ============================================================================

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class UpdatePlayerScoreCommandValidatorTests
{
    private readonly UpdatePlayerScoreCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var cmd = new UpdatePlayerScoreCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m, RoundNumber: 1);
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptySessionId_ShouldFail()
    {
        var cmd = new UpdatePlayerScoreCommand(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), 10m, RoundNumber: 1);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.SessionId);
    }

    [Fact]
    public void Validate_EmptyParticipantId_ShouldFail()
    {
        var cmd = new UpdatePlayerScoreCommand(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), 10m, RoundNumber: 1);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.ParticipantId);
    }

    [Fact]
    public void Validate_EmptyRequesterId_ShouldFail()
    {
        var cmd = new UpdatePlayerScoreCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, 10m, RoundNumber: 1);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.RequesterId);
    }

    [Fact]
    public void Validate_ScoreValueTooHigh_ShouldFail()
    {
        var cmd = new UpdatePlayerScoreCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10000m, RoundNumber: 1);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.ScoreValue);
    }

    [Fact]
    public void Validate_ScoreValueTooLow_ShouldFail()
    {
        var cmd = new UpdatePlayerScoreCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), -10000m, RoundNumber: 1);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.ScoreValue);
    }

    [Fact]
    public void Validate_NeitherRoundNorCategory_ShouldFail()
    {
        var cmd = new UpdatePlayerScoreCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 10m);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void Validate_ScoreValueZero_ShouldFail()
    {
        var cmd = new UpdatePlayerScoreCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0m, RoundNumber: 1);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.ScoreValue);
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class RollSessionDiceCommandValidatorTests
{
    private readonly RollSessionDiceCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var cmd = new RollSessionDiceCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "2d6");
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyFormula_ShouldFail()
    {
        var cmd = new RollSessionDiceCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Formula);
    }

    [Fact]
    public void Validate_FormulaTooLong_ShouldFail()
    {
        var cmd = new RollSessionDiceCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 51));
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Formula);
    }

    [Fact]
    public void Validate_EmptySessionId_ShouldFail()
    {
        var cmd = new RollSessionDiceCommand(Guid.Empty, Guid.NewGuid(), Guid.NewGuid(), "1d6");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.SessionId);
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class DrawSessionCardCommandValidatorTests
{
    private readonly DrawSessionCardCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var cmd = new DrawSessionCardCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 2);
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_CountZero_ShouldFail()
    {
        var cmd = new DrawSessionCardCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 0);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Count);
    }

    [Fact]
    public void Validate_CountTooHigh_ShouldFail()
    {
        var cmd = new DrawSessionCardCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), 11);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Count);
    }

    [Fact]
    public void Validate_EmptyDeckId_ShouldFail()
    {
        var cmd = new DrawSessionCardCommand(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), Guid.NewGuid());
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.DeckId);
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class SessionTimerActionCommandValidatorTests
{
    private readonly SessionTimerActionCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidStartCommand_ShouldPass()
    {
        var cmd = new SessionTimerActionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TimerAction.Start, "P1", 60);
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_ValidPauseCommand_ShouldPass()
    {
        var cmd = new SessionTimerActionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TimerAction.Pause);
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_DurationZeroOnStart_ShouldFail()
    {
        var cmd = new SessionTimerActionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TimerAction.Start, "P1", 0);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.DurationSeconds);
    }

    [Fact]
    public void Validate_DurationTooHighOnStart_ShouldFail()
    {
        var cmd = new SessionTimerActionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), TimerAction.Start, "P1", 3601);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.DurationSeconds);
    }

    [Fact]
    public void Validate_InvalidAction_ShouldFail()
    {
        var cmd = new SessionTimerActionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), (TimerAction)99);
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Action);
    }
}

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class SendChatActionCommandValidatorTests
{
    private readonly SendChatActionCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var cmd = new SendChatActionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "Hello!");
        _validator.TestValidate(cmd).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyContent_ShouldFail()
    {
        var cmd = new SendChatActionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), "");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void Validate_ContentTooLong_ShouldFail()
    {
        var cmd = new SendChatActionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), new string('a', 1001));
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.Content);
    }

    [Fact]
    public void Validate_EmptySenderId_ShouldFail()
    {
        var cmd = new SendChatActionCommand(Guid.NewGuid(), Guid.Empty, Guid.NewGuid(), "Hello");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.SenderId);
    }

    [Fact]
    public void Validate_EmptyRequesterId_ShouldFail()
    {
        var cmd = new SendChatActionCommand(Guid.NewGuid(), Guid.NewGuid(), Guid.Empty, "Hello");
        _validator.TestValidate(cmd).ShouldHaveValidationErrorFor(x => x.RequesterId);
    }
}
