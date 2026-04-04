using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Repositories;

/// <summary>
/// Infrastructure adapter that bridges the KnowledgeBase domain's IGameSessionStateReader
/// to the GameManagement BC's IGameSessionStateRepository.
/// Prevents the KB domain layer from taking a direct dependency on the GameManagement BC.
/// </summary>
internal sealed class GameSessionStateReaderAdapter : IGameSessionStateReader
{
    private readonly IGameSessionStateRepository _gmRepository;

    public GameSessionStateReaderAdapter(IGameSessionStateRepository gmRepository)
    {
        _gmRepository = gmRepository ?? throw new ArgumentNullException(nameof(gmRepository));
    }

    /// <inheritdoc/>
    public async Task<GameSessionStateSnapshot?> GetBySessionIdAsync(
        Guid gameSessionId,
        CancellationToken cancellationToken = default)
    {
        var state = await _gmRepository.GetBySessionIdAsync(gameSessionId, cancellationToken)
            .ConfigureAwait(false);

        if (state is null)
            return null;

        return new GameSessionStateSnapshot(
            Id: state.Id,
            GameSessionId: gameSessionId,
            CurrentState: state.CurrentState,
            LastUpdatedAt: state.LastUpdatedAt);
    }
}
