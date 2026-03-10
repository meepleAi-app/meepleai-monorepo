namespace Api.BoundedContexts.GameManagement.Application.DTOs.GameSessionContext;

/// <summary>
/// Cross-context DTO aggregating data from multiple bounded contexts for a live game session.
/// Provides the AI agent and frontend with a unified view of the session's available resources.
/// Issue #5579: GameSessionContext cross-context orchestrator.
/// </summary>
/// <param name="SessionId">The live session identifier.</param>
/// <param name="PrimaryGameId">The primary game being played (from LiveGameSession.GameId).</param>
/// <param name="ExpansionGameIds">Expansion game IDs linked via EntityLink (ExpansionOf).</param>
/// <param name="AllGameIds">All game IDs (primary + expansions) for convenience.</param>
/// <param name="KbCardIds">VectorDocument GameIds — games that have indexed KB cards.</param>
/// <param name="CurrentPhase">Current phase name from the live session.</param>
/// <param name="PrimaryRules">Rulebook analysis summary for the primary game (null if no analysis exists).</param>
/// <param name="ExpansionRules">Rulebook analysis summaries for expansion games.</param>
/// <param name="MissingAnalysisGameNames">Names of expansion games that have no indexed PDF.</param>
/// <param name="GamesWithoutPdf">IDs of games (primary or expansion) that have no indexed vector documents.</param>
/// <param name="DegradationLevel">Degradation level indicating how much AI capability is available.</param>
public sealed record GameSessionContextDto(
    Guid SessionId,
    Guid? PrimaryGameId,
    IReadOnlyList<Guid> ExpansionGameIds,
    IReadOnlyList<Guid> AllGameIds,
    IReadOnlyList<Guid> KbCardIds,
    string? CurrentPhase,
    RulebookAnalysisSummaryDto? PrimaryRules,
    IReadOnlyList<RulebookAnalysisSummaryDto> ExpansionRules,
    IReadOnlyList<string> MissingAnalysisGameNames,
    IReadOnlyList<Guid> GamesWithoutPdf,
    SessionDegradationLevel DegradationLevel);
