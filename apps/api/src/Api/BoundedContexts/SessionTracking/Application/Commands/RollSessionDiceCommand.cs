using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Services;
using FluentValidation;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command for a player to roll dice in a session.
/// Requires at least Player role — spectators cannot roll dice.
/// Issue #4765 - Player Action Endpoints + Host Validation
/// </summary>
public record RollSessionDiceCommand(
    Guid SessionId,
    Guid ParticipantId,
    Guid RequesterId,
    string Formula,
    string? Label = null
) : IRequest<RollSessionDiceResult>, IRequireSessionRole
{
    /// <summary>Minimum role: Player.</summary>
    public ParticipantRole MinimumRole => ParticipantRole.Player;
}

/// <summary>
/// Result of a dice roll player action.
/// </summary>
public record RollSessionDiceResult(
    Guid DiceRollId,
    string Formula,
    int[] Rolls,
    int Modifier,
    int Total,
    DateTime Timestamp
);

/// <summary>
/// FluentValidation validator for RollSessionDiceCommand.
/// </summary>
public class RollSessionDiceCommandValidator : AbstractValidator<RollSessionDiceCommand>
{
    public RollSessionDiceCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("SessionId is required");

        RuleFor(x => x.ParticipantId)
            .NotEmpty()
            .WithMessage("ParticipantId is required");

        RuleFor(x => x.RequesterId)
            .NotEmpty()
            .WithMessage("RequesterId is required");

        RuleFor(x => x.Formula)
            .NotEmpty()
            .WithMessage("Dice formula is required")
            .MaximumLength(50)
            .WithMessage("Formula must be 50 characters or fewer");
    }
}
