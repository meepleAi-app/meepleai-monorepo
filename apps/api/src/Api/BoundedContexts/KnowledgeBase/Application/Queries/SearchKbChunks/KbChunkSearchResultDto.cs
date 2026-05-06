namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.SearchKbChunks;

/// <summary>
/// Paginated result of a full-text search within a KB document (G3, Issue #730).
/// </summary>
internal sealed record KbChunkSearchResultDto(
    IReadOnlyList<KbChunkMatchDto> Matches,
    int TotalCount,
    int Skip,
    int Take
);
