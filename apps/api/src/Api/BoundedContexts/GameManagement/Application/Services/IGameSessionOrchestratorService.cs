using Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;

namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Cross-context orchestrator that builds a unified session context DTO
/// by collecting data from GameManagement, EntityRelationships, SharedGameCatalog, and KnowledgeBase.
/// Uses fail-open pattern: the session always starts even if some data sources are unavailable.
/// Issue #5579: GameSessionContext cross-context orchestrator.
/// </summary>
internal interface IGameSessionOrchestratorService
{
    /// <summary>
    /// Builds a complete GameSessionContext by fetching data from all relevant bounded contexts.
    /// Each data source is independently try-caught so partial failures degrade gracefully.
    /// </summary>
    /// <param name="sessionId">The live session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A fully populated GameSessionContextDto with degradation level.</returns>
    Task<GameSessionContextDto> BuildContextAsync(Guid sessionId, CancellationToken ct = default);

    /// <summary>
    /// Rebuilds the session context (same as BuildContextAsync — no caching in v1).
    /// Provided as a separate method for future cache-invalidation semantics.
    /// </summary>
    /// <param name="sessionId">The live session identifier.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A freshly built GameSessionContextDto.</returns>
    Task<GameSessionContextDto> RefreshContextAsync(Guid sessionId, CancellationToken ct = default);
}
