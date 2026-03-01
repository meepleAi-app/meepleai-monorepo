using Api.BoundedContexts.SessionTracking.Application.Commands;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.Tests.Constants;
using FluentValidation.TestHelper;
using Xunit;

namespace Api.Tests.BoundedContexts.SessionTracking.Application.Validators;

/// <summary>
/// Unit tests for JoinSessionByCodeCommandValidator.
/// Issue #4766 - Session Join via Code + Active Player Roles
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class JoinSessionByCodeCommandValidatorTests
{
    private readonly JoinSessionByCodeCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var command = new JoinSessionByCodeCommand("ABC123", Guid.NewGuid(), "Player 1");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptySessionCode_ShouldFail()
    {
        var command = new JoinSessionByCodeCommand("", Guid.NewGuid(), "Player 1");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SessionCode);
    }

    [Fact]
    public void Validate_TooLongSessionCode_ShouldFail()
    {
        var command = new JoinSessionByCodeCommand("ABCDEFG", Guid.NewGuid(), "Player 1");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SessionCode);
    }

    [Fact]
    public void Validate_TooShortSessionCode_ShouldFail()
    {
        var command = new JoinSessionByCodeCommand("ABC", Guid.NewGuid(), "Player 1");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SessionCode);
    }

    [Fact]
    public void Validate_NonAlphanumericSessionCode_ShouldFail()
    {
        var command = new JoinSessionByCodeCommand("AB-12!", Guid.NewGuid(), "Player 1");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SessionCode);
    }

    [Fact]
    public void Validate_LowercaseSessionCode_ShouldPass()
    {
        var command = new JoinSessionByCodeCommand("abc123", Guid.NewGuid(), "Player 1");
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptyUserId_ShouldFail()
    {
        var command = new JoinSessionByCodeCommand("ABC123", Guid.Empty, "Player 1");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.UserId);
    }

    [Fact]
    public void Validate_EmptyDisplayName_ShouldFail()
    {
        var command = new JoinSessionByCodeCommand("ABC123", Guid.NewGuid(), "");
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }

    [Fact]
    public void Validate_TooLongDisplayName_ShouldFail()
    {
        var command = new JoinSessionByCodeCommand("ABC123", Guid.NewGuid(), new string('A', 51));
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.DisplayName);
    }
}

/// <summary>
/// Unit tests for AssignParticipantRoleCommandValidator.
/// Issue #4766 - Session Join via Code + Active Player Roles
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SessionTracking")]
public class AssignParticipantRoleCommandValidatorTests
{
    private readonly AssignParticipantRoleCommandValidator _validator = new();

    [Fact]
    public void Validate_ValidCommand_ShouldPass()
    {
        var command = new AssignParticipantRoleCommand(
            Guid.NewGuid(), Guid.NewGuid(), ParticipantRole.Player, Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_EmptySessionId_ShouldFail()
    {
        var command = new AssignParticipantRoleCommand(
            Guid.Empty, Guid.NewGuid(), ParticipantRole.Player, Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.SessionId);
    }

    [Fact]
    public void Validate_EmptyParticipantId_ShouldFail()
    {
        var command = new AssignParticipantRoleCommand(
            Guid.NewGuid(), Guid.Empty, ParticipantRole.Player, Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.ParticipantId);
    }

    [Theory]
    [InlineData((ParticipantRole)99)]
    [InlineData((ParticipantRole)(-1))]
    public void Validate_InvalidRole_ShouldFail(ParticipantRole invalidRole)
    {
        var command = new AssignParticipantRoleCommand(
            Guid.NewGuid(), Guid.NewGuid(), invalidRole, Guid.NewGuid());
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.NewRole);
    }

    [Fact]
    public void Validate_EmptyRequesterId_ShouldFail()
    {
        var command = new AssignParticipantRoleCommand(
            Guid.NewGuid(), Guid.NewGuid(), ParticipantRole.Host, Guid.Empty);
        var result = _validator.TestValidate(command);
        result.ShouldHaveValidationErrorFor(x => x.RequesterId);
    }
}
