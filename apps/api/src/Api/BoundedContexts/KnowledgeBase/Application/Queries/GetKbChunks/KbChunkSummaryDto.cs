namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;

/// <summary>
/// Spec-conformant chunk summary DTO for <c>GET /api/v1/kb-docs/{id}/chunks</c>
/// (Wave 3 Phase 3, PR #732 §6.3.2 / Issue #805).
/// </summary>
/// <remarks>
/// <para>
/// <b>Path A full rewrite</b>: drops admin-gated <c>CharacterCount</c>,
/// <c>ElementType</c>, <c>EmbeddingStatus</c>, <c>Level</c> from the public
/// surface. <c>ChunkId</c> renamed to <c>Id</c> per spec verbatim.
/// </para>
///
/// <para>
/// <b>VectorId de-gating</b> — security review:
/// </para>
/// <para>
/// The historical baseline admin-gated <c>VectorId</c> via <c>JsonIgnore</c>
/// (Decision D4). Spec §6.3.2 requires public exposure for cross-reference with
/// semantic search. PR #732 §3.5 API versioning rationale documents this as an
/// intentional supersession: chunk vector IDs are internal references for
/// search-result join joins, not user-sensitive content. Public exposure aligns
/// with spec intent and Wave 3 unblock requirements.
/// </para>
/// </remarks>
internal sealed record KbChunkSummaryDto(
    Guid Id,
    int Position,
    IReadOnlyList<string> HeadingPath,
    string Snippet,
    int? PageNumber,
    string VectorId
);
