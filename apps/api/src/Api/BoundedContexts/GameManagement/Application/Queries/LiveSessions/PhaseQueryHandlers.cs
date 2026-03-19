using Api.BoundedContexts.GameManagement.Application.Queries.LiveSessions;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.LiveSessions;

/// <summary>
/// Handles querying the current turn phases state of a live session.
/// Issue #4761: Turn Phases from TurnTemplate + Event-Triggered Snapshots.
/// </summary>
internal class GetTurnPhasesQueryHandler : IQueryHandler<GetTurnPhasesQuery, TurnPhasesDto>
{
    private readonly ILiveSessionRepository _sessionRepository;

    public GetTurnPhasesQueryHandler(ILiveSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<TurnPhasesDto> Handle(GetTurnPhasesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var session = await _sessionRepository.GetByIdAsync(query.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", query.SessionId.ToString());

        return new TurnPhasesDto(
            CurrentTurnIndex: session.CurrentTurnIndex,
            CurrentPhaseIndex: session.CurrentPhaseIndex,
            CurrentPhaseName: session.GetCurrentPhaseName(),
            PhaseNames: session.PhaseNames,
            TotalPhases: session.PhaseNames.Length,
            HasPhases: session.PhaseNames.Length > 0);
    }
}
