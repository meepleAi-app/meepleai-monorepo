using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Command to roll dice in a session.
/// </summary>
public record RollDiceCommand(
    Guid SessionId,
    Guid ParticipantId,
    string Formula,
    string? Label = null
) : IRequest<RollDiceResult>;

/// <summary>
/// Result of a dice roll command.
/// </summary>
public record RollDiceResult(
    Guid DiceRollId,
    string Formula,
    int[] Rolls,
    int Modifier,
    int Total,
    DateTime Timestamp
);
