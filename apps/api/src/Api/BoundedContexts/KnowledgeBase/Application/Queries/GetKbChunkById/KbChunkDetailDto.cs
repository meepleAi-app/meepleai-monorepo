namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunkById;

/// <summary>
/// Spec-conformant chunk detail DTO for
/// <c>GET /api/v1/kb-docs/{id}/chunks/{chunkId}</c>
/// (Wave 3 Phase 3, PR #732 §6.3.3 / Issue #805).
/// </summary>
/// <remarks>
/// <para>
/// <b>Path A full rewrite</b>: drops admin-gated <c>VectorId</c>,
/// <c>CharacterCount</c>, <c>ElementType</c>, <c>EmbeddingStatus</c>,
/// <c>ParentChunkId</c>, <c>Level</c>. <c>ChunkId</c> renamed to <c>Id</c>,
/// <c>DocId</c> added per spec verbatim.
/// </para>
///
/// <para>
/// <b>Markdown subset</b>: <c>Content</c> is sanitized server-side via
/// <see cref="Api.Infrastructure.Services.MarkdownSubsetSanitizer"/> before
/// emission — H4-H6 demoted, raw HTML stripped, images replaced with
/// <c>[Image: alt]</c>, footnotes stripped.
/// </para>
///
/// <para>
/// <b>Metadata</b>: Gate B v1 carryover — empty dictionary. The
/// <c>TextChunkEntity</c> has no <c>Metadata</c> column in v1; the spec field
/// is reserved for future structured chunk-level annotations
/// (e.g. <c>tableData</c>, <c>diagramRefs</c>).
/// </para>
/// </remarks>
internal sealed record KbChunkDetailDto(
    Guid Id,
    Guid DocId,
    int Position,
    IReadOnlyList<string> HeadingPath,
    string Content,
    int? PageNumber,
    Guid? PrevChunkId,
    Guid? NextChunkId,
    IReadOnlyDictionary<string, object> Metadata
);
