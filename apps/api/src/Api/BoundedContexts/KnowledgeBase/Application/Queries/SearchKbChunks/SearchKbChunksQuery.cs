using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.SearchKbChunks;

/// <summary>
/// Query to perform full-text search within a single KB document using PostgreSQL FTS.
/// Used by <c>POST /api/v1/kb-docs/{id}/chunks/search</c> (G3 goal, Issue #730).
///
/// <para>
/// The query is sanitized by <c>plainto_tsquery</c> which treats the input as plain text
/// (no tsquery operator injection possible). Ranking uses <c>ts_rank_cd</c> and snippets
/// are generated with <c>ts_headline</c> producing <c>&lt;mark&gt;</c> tags.
/// </para>
/// </summary>
internal sealed record SearchKbChunksQuery(
    Guid DocumentId,
    string Query,
    int Skip,
    int Take
) : IQuery<KbChunkSearchResultDto>;
