namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.SearchKbChunks;

/// <summary>
/// Represents a single chunk match returned by the G3 FTS search.
/// <see cref="Snippet"/> contains ts_headline output with <c>&lt;mark&gt;</c> tags
/// wrapping the matched terms.
/// </summary>
internal sealed record KbChunkMatchDto(
    Guid ChunkId,
    IReadOnlyList<string> HeadingPath,
    string Snippet,
    float Rank,
    int? PageNumber,
    int Position
);
