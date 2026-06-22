namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbChunks;

/// <summary>
/// Spec-conformant chunks list response DTO for
/// <c>GET /api/v1/kb-docs/{id}/chunks</c> (Wave 3 Phase 3, PR #732 §6.3.2 /
/// Issue #805).
/// </summary>
/// <remarks>
/// <para>
/// <b>Path A full rewrite</b>: replaces offset pagination (<c>Skip</c>/<c>Take</c>/<c>HasMore</c>)
/// with cursor pagination per spec verbatim. <c>NextCursor</c> is null on the
/// last page. <c>ProcessingState</c> field dropped from the public response —
/// the FE retrieves it via the doc-detail endpoint (which surfaces 423 Locked
/// when the doc isn't ready).
/// </para>
///
/// <para>
/// Renamed from <c>KbChunkListDto</c> to <c>KbChunksListResponse</c> per spec
/// nomenclature. Wire shape: <c>{ items, nextCursor, totalCount }</c>.
/// </para>
/// </remarks>
internal sealed record KbChunksListResponse(
    IReadOnlyList<KbChunkSummaryDto> Items,
    string? NextCursor,
    int TotalCount
);
