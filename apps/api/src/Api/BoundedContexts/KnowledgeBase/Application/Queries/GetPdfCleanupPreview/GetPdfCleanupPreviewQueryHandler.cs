using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries.GetPdfCleanupPreview;

/// <summary>
/// Handles <see cref="GetPdfCleanupPreviewQuery"/>. Issue #1529.
///
/// Reads PdfDocument file size + counts of dependent rows (TextChunks, RaptorSummaries)
/// in a single round trip per table. Returns <c>null</c> when the PDF does not exist
/// so the endpoint can map to 404 without leaking existence to unauthorized callers.
/// </summary>
internal sealed class GetPdfCleanupPreviewQueryHandler
    : IQueryHandler<GetPdfCleanupPreviewQuery, PdfCleanupPreviewDto?>
{
    private readonly MeepleAiDbContext _db;

    public GetPdfCleanupPreviewQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<PdfCleanupPreviewDto?> Handle(
        GetPdfCleanupPreviewQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var pdfFileSize = await _db.PdfDocuments
            .AsNoTracking()
            .Where(p => p.Id == query.PdfId)
            .Select(p => (long?)p.FileSizeBytes)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pdfFileSize is null)
            return null;

        // COUNT(*) per dependent table — keeps the query plan simple (B-tree index scans).
        // No need to LEFT JOIN with the PDF row because we already verified existence above.
        var chunkCount = await _db.TextChunks
            .AsNoTracking()
            .CountAsync(tc => tc.PdfDocumentId == query.PdfId, cancellationToken)
            .ConfigureAwait(false);

        var raptorSummaryCount = await _db.RaptorSummaries
            .AsNoTracking()
            .CountAsync(r => r.PdfDocumentId == query.PdfId, cancellationToken)
            .ConfigureAwait(false);

        // GraphEdgeCount: constant 0 placeholder — no EntityLink/Edge table exists yet
        // in the KnowledgeBase BC. Tracked as a future BE follow-up; field is exposed
        // today so the FE confirm-delete drawer can render the row without a contract
        // change once the graph store lands.
        const int graphEdgeCount = 0;

        return new PdfCleanupPreviewDto(
            PdfId: query.PdfId,
            PdfFileSizeBytes: pdfFileSize.Value,
            ChunkCount: chunkCount,
            RaptorSummaryCount: raptorSummaryCount,
            GraphEdgeCount: graphEdgeCount);
    }
}
