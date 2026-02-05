using MediatR;
using Api.BoundedContexts.SessionTracking.Application.Queries;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.Middleware.Exceptions;

namespace Api.BoundedContexts.SessionTracking.Application.Handlers;

/// <summary>
/// Handler for GetDiceRollHistoryQuery.
/// Returns recent dice rolls for a session.
/// </summary>
public class GetDiceRollHistoryQueryHandler : IRequestHandler<GetDiceRollHistoryQuery, IEnumerable<DiceRollDto>>
{
    private readonly ISessionRepository _sessionRepository;
    private readonly IDiceRollRepository _diceRollRepository;

    public GetDiceRollHistoryQueryHandler(
        ISessionRepository sessionRepository,
        IDiceRollRepository diceRollRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
        _diceRollRepository = diceRollRepository ?? throw new ArgumentNullException(nameof(diceRollRepository));
    }

    public async Task<IEnumerable<DiceRollDto>> Handle(GetDiceRollHistoryQuery request, CancellationToken cancellationToken)
    {
        // Verify session exists
        var session = await _sessionRepository.GetByIdAsync(request.SessionId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Session {request.SessionId} not found");

        // Get recent dice rolls
        var diceRolls = await _diceRollRepository.GetRecentBySessionIdAsync(
            request.SessionId,
            request.Limit,
            cancellationToken).ConfigureAwait(false);

        // Map to DTOs with participant names
        var participantLookup = session.Participants.ToDictionary(p => p.Id, p => p.DisplayName);

        return diceRolls.Select(roll => new DiceRollDto(
            roll.Id,
            roll.ParticipantId,
            participantLookup.TryGetValue(roll.ParticipantId, out var name) ? name : "Unknown",
            roll.Formula,
            roll.Label,
            roll.GetRolls(),
            roll.Modifier,
            roll.Total,
            roll.Timestamp
        ));
    }
}
