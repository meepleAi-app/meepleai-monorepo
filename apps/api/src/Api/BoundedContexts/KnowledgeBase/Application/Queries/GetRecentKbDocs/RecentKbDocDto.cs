namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetRecentKbDocs;

/// <summary>
/// Lightweight DTO for the /api/v1/kb-docs/recent endpoint
/// (Wave 3 Phase 1, PR #732 §4.3.5 / Issue #805).
/// </summary>
/// <remarks>
/// Powers the SP4 /discover route's "Recent KB docs" rail (frontend
/// <c>useDiscoverRecentKbDocs</c>).
///
/// <para><b>DocType mapping</b> — the persisted <c>PdfDocumentEntity.DocumentCategory</c>
/// enum collapses into a smaller wire vocabulary expected by the FE:
/// <list type="bullet">
///   <item><c>Rulebook</c> → <c>"rulebook"</c></item>
///   <item><c>Errata</c> → <c>"errata"</c></item>
///   <item><c>Expansion | QuickStart | Reference | PlayerAid | Other</c> → <c>"guide"</c></item>
/// </list>
/// The FE contract also defines <c>"faq"</c> for forward-compat with the
/// <c>GameFaqEntity</c> surface; v1 will not emit it from this endpoint
/// since FAQs are not <see cref="Api.Infrastructure.Entities.PdfDocumentEntity"/> rows.
/// </para>
/// </remarks>
internal sealed record RecentKbDocDto(
    Guid Id,
    string Title,
    Guid? GameId,
    string? GameName,
    string DocType,
    DateTime LastIngestedAt,
    int ChunkCount
);
