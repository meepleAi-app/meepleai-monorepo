using Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;
using Api.BoundedContexts.GameManagement.Application.Queries.GameSessionContext;
using Api.BoundedContexts.GameManagement.Application.Services;
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
/// Issue #5579: GameSessionContext cross-context orchestrator.
/// </summary>
internal sealed class RefreshGameSessionContextQueryHandler
    : IQueryHandler<RefreshGameSessionContextQuery, GameSessionContextDto>
{
    private readonly IGameSessionOrchestratorService _orchestrator;

    public RefreshGameSessionContextQueryHandler(IGameSessionOrchestratorService orchestrator)
    {
        _orchestrator = orchestrator ?? throw new ArgumentNullException(nameof(orchestrator));
    }

    public async Task<GameSessionContextDto> Handle(
        RefreshGameSessionContextQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        return await _orchestrator.RefreshContextAsync(query.SessionId, cancellationToken).ConfigureAwait(false);
    }
}
