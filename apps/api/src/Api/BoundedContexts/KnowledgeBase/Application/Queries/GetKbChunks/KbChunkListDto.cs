namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;

/// <summary>
/// Paginated list of text chunk summaries for a single KB document.
/// Returned by <c>GET /api/v1/kb-docs/{id}/chunks</c> (G1 goal).
/// </summary>
internal sealed record KbChunkListDto(
    IReadOnlyList<KbChunkSummaryDto> Chunks,
    int TotalCount,
    int Skip,
    int Take,
    bool HasMore,
    string ProcessingState
);
