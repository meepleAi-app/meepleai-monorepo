using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetUserGameKbStatus;

/// <summary>
/// Query to retrieve the KB indexing status for a specific game as seen by a regular user.
/// KB-03: Returns document count, coverage score, coverage level, and suggested questions.
/// Issue #1529: Extended with chunk/embedding counts, last-reindex/RAPTOR timestamps,
/// and lifetime/last-7-days cost aggregates to power the KB Hub presentational components
/// that #1481 (PR #1528) wired only partially.
/// </summary>
internal sealed record GetUserGameKbStatusQuery(Guid GameId)
    : IQuery<UserGameKbStatusDto>;

/// <summary>
/// DTO representing the KB status for a game.
///
/// Issue #1529 deferred fields (additive, backward-compatible — never reorder or
/// rename pre-existing fields):
/// - <see cref="ChunksCount"/> / <see cref="EmbeddingsCount"/>: 0 when KB is not yet indexed.
///   Embeddings derive 1:1 from chunks via pgvector (every text chunk gets one embedding row).
/// - <see cref="LastReindexAt"/>: <c>null</c> when no VectorDocument has ever been indexed
///   for this game; otherwise <c>MAX(VectorDocument.IndexedAt)</c>.
/// - <see cref="RaptorLastRebuildAt"/>: <c>null</c> when RAPTOR tree has never been built;
///   otherwise <c>MAX(RaptorSummary.CreatedAt)</c> across all tree levels for the game.
/// - <see cref="LifetimeCostUsd"/>: <c>0.00m</c> placeholder. Per-game cost attribution does
///   not yet exist (LlmCostLogEntity has no GameId column); the field is wired as constant
///   <c>0.00m</c> so the FE Sparkline + KB cost card can be lit without a contract change
///   once attribution is added (tracked as a follow-up for a future BE iteration).
/// - <see cref="CostHistoryLast7Days"/>: empty array <c>[]</c> when no cost data exists for
///   this game (same root cause as <see cref="LifetimeCostUsd"/>). Per spec, <c>[]</c> means
///   "no data ever" while <c>[0,0,0,0,0,0,0]</c> would mean "KB used but zero cost in last 7
///   days" — we cannot currently distinguish, so we return <c>[]</c>.
/// </summary>
internal sealed record UserGameKbStatusDto(
    Guid GameId,
    bool IsIndexed,
    int DocumentCount,
    int CoverageScore,
    string CoverageLevel,
    List<string> SuggestedQuestions,
    int ChunksCount,
    int EmbeddingsCount,
    DateTime? LastReindexAt,
    DateTime? RaptorLastRebuildAt,
    decimal LifetimeCostUsd,
    IReadOnlyList<decimal> CostHistoryLast7Days);
