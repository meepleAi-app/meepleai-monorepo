namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetKbDocumentById;

/// <summary>
/// Spec-conformant document detail DTO for <c>GET /api/v1/kb-docs/{id}</c>
/// (Wave 3 Phase 3, PR #732 §6.3.1 / Issue #805).
/// </summary>
/// <remarks>
/// <para>
/// <b>Path A full rewrite</b>: this replaces the previous Issue #730 baseline DTO
/// (<c>KbDocumentDto</c> with <c>SharedGameId</c> / <c>DocumentCategory</c> raw enum,
/// <c>IndexedAt</c>, <c>ProcessingError</c>, <c>RetryCount</c>, <c>FailedAtState</c>).
/// All admin-gated fields are dropped from the public surface per spec §6.3.1
/// verbatim. The 423 Locked status semantically distinguishes "in elaborazione"
/// (processingStatus != 'ready') from 404 "not found" (Nygard operational note).
/// </para>
///
/// <para>
/// <b>Field mapping</b>:
/// <list type="bullet">
///   <item><c>DocType</c> — collapses 7-value <c>DocumentCategory</c> enum into
///         the 4-value FE wire vocabulary <c>{rulebook, faq, errata, guide}</c>
///         (Phase 1 P21 reuse from <c>RecentKbDocDto</c>).</item>
///   <item><c>ProcessingStatus</c> — collapses raw 8-state <c>ProcessingState</c>
///         into the 4-value FE wire vocabulary <c>{queued, processing, ready, failed}</c>.</item>
///   <item><c>GameName</c> — joined from <c>SharedGameCatalog</c> via
///         <c>ISharedGameRepository.GetNamesByIdsAsync</c> (single batch query, no N+1).</item>
///   <item><c>UploaderName</c> — joined from <c>UserEntity.DisplayName</c> with
///         email fallback (graceful degradation when account removed).</item>
///   <item><c>LastIngestedAt</c> — non-null fallback to <c>UploadedAt</c> when
///         <c>ProcessedAt</c> is null (matches Phase 1 RecentKbDocs convention).</item>
///   <item><c>Tags</c> — Gate B v1 carryover: empty array (<c>PdfDocumentEntity</c>
///         has no <c>Tags</c> column in v1).</item>
/// </list>
/// </para>
///
/// <para>
/// <b>Decision D4 (admin-gated fields) supersedes</b>: the historical baseline
/// admin-gated <c>ProcessingError</c>, <c>RetryCount</c>, <c>FailedAtState</c>
/// via <c>JsonIgnore</c>. Spec §6.3.1 omits these from the public surface, so
/// they're removed entirely (admins must use a separate <c>/admin/kb-docs/{id}</c>
/// endpoint when one exists — out of scope for Wave 3 Phase 3).
/// </para>
/// </remarks>
internal sealed record KbDocumentDto(
    Guid Id,
    string Title,
    string DocType,
    Guid? GameId,
    string? GameName,
    string UploaderName,
    DateTime UploadedAt,
    DateTime LastIngestedAt,
    string ProcessingStatus,
    int ChunkCount,
    int? PageCount,
    string Language,
    IReadOnlyList<string> Tags
);
