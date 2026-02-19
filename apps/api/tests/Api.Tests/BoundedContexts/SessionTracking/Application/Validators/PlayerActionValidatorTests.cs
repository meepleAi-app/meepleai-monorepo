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
