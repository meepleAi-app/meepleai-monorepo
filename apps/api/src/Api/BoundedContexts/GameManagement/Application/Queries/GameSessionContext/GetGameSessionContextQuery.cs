using Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameSessionContext;

/// <summary>
/// Query to build the GameSessionContext for a live session.
/// Issue #5579: GameSessionContext cross-context orchestrator.
/// </summary>
internal record GetGameSessionContextQuery(Guid SessionId) : IQuery<GameSessionContextDto>;

/// <summary>
/// Query to refresh (rebuild) the GameSessionContext for a live session.
/// Issue #5579: GameSessionContext cross-context orchestrator.
/// </summary>
internal record RefreshGameSessionContextQuery(Guid SessionId) : IQuery<GameSessionContextDto>;
