using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Query to get recent dice roll history for a session.
/// </summary>
public record GetDiceRollHistoryQuery(
    Guid SessionId,
    Guid RequestedBy, // #4883: owner/participant-only; derived from the principal, never from the client.
    int Limit = 20
) : IRequest<IEnumerable<DiceRollDto>>;

/// <summary>
/// DTO representing a dice roll.
/// </summary>
public record DiceRollDto(
    Guid Id,
    Guid ParticipantId,
    string ParticipantName,
    string Formula,
    string? Label,
    int[] Rolls,
    int Modifier,
    int Total,
    DateTime Timestamp
);
