using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Handles query to get all snapshots for a game session state.
/// Issue #2403: GameSessionState Entity (list snapshots for history)
/// </summary>
internal class GetStateSnapshotsQueryHandler : IQueryHandler<GetStateSnapshotsQuery, List<GameStateSnapshotDto>>
{
    private readonly IGameSessionStateRepository _stateRepository;

    public GetStateSnapshotsQueryHandler(IGameSessionStateRepository stateRepository)
    {
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
    }

    public async Task<List<GameStateSnapshotDto>> Handle(GetStateSnapshotsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var state = await _stateRepository.GetByIdAsync(query.SessionStateId, cancellationToken)
            .ConfigureAwait(false);

        if (state == null)
            throw new NotFoundException("GameSessionState", query.SessionStateId.ToString());

        return state.Snapshots.Select(s => s.ToDto()).ToList();
    }
}
