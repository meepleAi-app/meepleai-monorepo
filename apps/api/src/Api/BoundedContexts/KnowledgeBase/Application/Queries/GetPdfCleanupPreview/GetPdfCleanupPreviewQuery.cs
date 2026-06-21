using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetPdfCleanupPreview;

/// <summary>
/// Issue #1529: Preview of the artifacts that would be removed if the given PDF
/// were deleted (chunks, RAPTOR summaries, graph edges, plus the file size of
/// the source PDF). Powers the FE confirm-delete drawer so the user can see
/// the blast radius before confirming.
///
/// Returns <c>null</c> when the PDF does not exist or the caller is not authorized.
/// Authorization (owner-or-admin) is enforced at the endpoint layer via
/// <c>GetPdfOwnershipQuery</c> — mirrors <c>HandleDeletePdf</c>.
/// </summary>
/// <param name="PdfId">The PDF document id.</param>
internal sealed record GetPdfCleanupPreviewQuery(Guid PdfId)
    : IQuery<PdfCleanupPreviewDto?>;

/// <summary>
/// Issue #1529: Response shape for <c>GET /api/v1/pdf/{pdfId}/cleanup-preview</c>.
///
/// All counts default to <c>0</c> when the underlying table has no rows for the PDF
/// (additive-friendly defaults: a brand-new PDF that has not been chunked yet will
/// show <c>0</c> chunks, <c>0</c> RAPTOR summaries, <c>0</c> graph edges).
///
/// <c>GraphEdgeCount</c> is currently a constant <c>0</c> placeholder because no
/// EntityLink/Edge table exists in the KnowledgeBase BC yet (the spec spans 3
/// future features). When the graph store lands the property can be wired without
/// a contract change.
/// </summary>
internal sealed record PdfCleanupPreviewDto(
    Guid PdfId,
    long PdfFileSizeBytes,
    int ChunkCount,
    int RaptorSummaryCount,
    int GraphEdgeCount);
