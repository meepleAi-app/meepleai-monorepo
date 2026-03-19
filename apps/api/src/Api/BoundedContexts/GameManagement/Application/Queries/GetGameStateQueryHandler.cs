using Api.BoundedContexts.GameManagement.Application.Mappers;
using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles query to get game session state.
/// Issue #2403: GameSessionState Entity
/// </summary>
internal class GetGameStateQueryHandler : IQueryHandler<GetGameStateQuery, GameSessionStateDto?>
{
    private readonly IGameSessionStateRepository _stateRepository;

    public GetGameStateQueryHandler(IGameSessionStateRepository stateRepository)
    {
        _stateRepository = stateRepository ?? throw new ArgumentNullException(nameof(stateRepository));
    }

    public async Task<GameSessionStateDto?> Handle(GetGameStateQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var state = await _stateRepository.GetBySessionIdAsync(query.GameSessionId, cancellationToken)
            .ConfigureAwait(false);

        return state?.ToDto();
    }
}
