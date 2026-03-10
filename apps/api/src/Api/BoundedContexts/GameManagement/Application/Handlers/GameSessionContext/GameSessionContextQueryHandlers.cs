using Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;
using Api.BoundedContexts.GameManagement.Application.Queries.GameSessionContext;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.GameSessionContext;

/// <summary>
/// Handles GetGameSessionContextQuery by delegating to the orchestrator service.
/// Issue #5579: GameSessionContext cross-context orchestrator.
/// </summary>
internal sealed class GetGameSessionContextQueryHandler
    : IQueryHandler<GetGameSessionContextQuery, GameSessionContextDto>
{
    private readonly IGameSessionOrchestratorService _orchestrator;

    public GetGameSessionContextQueryHandler(IGameSessionOrchestratorService orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    public async Task<GameSessionContextDto> Handle(
        GetGameSessionContextQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await _orchestrator.BuildContextAsync(query.SessionId, cancellationToken).ConfigureAwait(false);
    }
}

/// <summary>
/// Handles RefreshGameSessionContextQuery by delegating to the orchestrator service.
/// Also invalidates the HybridCache entry so session-aware RAG picks up the fresh context.
/// Issue #5579: GameSessionContext cross-context orchestrator.
/// Issue #5580: Session-aware RAG chat — cache invalidation on refresh.
/// </summary>
internal sealed class RefreshGameSessionContextQueryHandler
    : IQueryHandler<RefreshGameSessionContextQuery, GameSessionContextDto>
{
    private readonly IGameSessionOrchestratorService _orchestrator;
    private readonly IHybridCacheService _hybridCache;

    public RefreshGameSessionContextQueryHandler(
        IGameSessionOrchestratorService orchestrator,
        IHybridCacheService hybridCache)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
        _hybridCache = hybridCache ?? throw new ArgumentNullException(nameof(hybridCache));
    }

    public async Task<GameSessionContextDto> Handle(
        RefreshGameSessionContextQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Issue #5580: Invalidate cached session context so RAG picks up fresh data
        var cacheKey = $"game-session-context:{query.SessionId}";
        await _hybridCache.RemoveAsync(cacheKey, cancellationToken).ConfigureAwait(false);

        return await _orchestrator.RefreshContextAsync(query.SessionId, cancellationToken).ConfigureAwait(false);
    }
}
