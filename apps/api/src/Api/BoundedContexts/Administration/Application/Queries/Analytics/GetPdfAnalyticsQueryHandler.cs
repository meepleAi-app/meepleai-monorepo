using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.Analytics;

/// <summary>
/// Handler for PDF analytics query.
/// Queries PdfDocumentEntity for processing statistics.
/// ProcessingState values: Pending, Uploading, Extracting, Chunking, Embedding, Indexing, Ready, Failed
/// </summary>
internal class GetPdfAnalyticsQueryHandler : IQueryHandler<GetPdfAnalyticsQuery, PdfAnalyticsDto>
{
    private readonly MeepleAiDbContext _db;

    public GetPdfAnalyticsQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<PdfAnalyticsDto> Handle(GetPdfAnalyticsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var totalDocuments = await _db.Set<PdfDocumentEntity>().AsNoTracking()
            .CountAsync(cancellationToken).ConfigureAwait(false);

        // "Ready" means successfully processed; "Failed" means failure
        var totalProcessed = await _db.Set<PdfDocumentEntity>().AsNoTracking()
            .CountAsync(d => d.ProcessingState == "Ready" || d.ProcessingState == "Failed", cancellationToken)
            .ConfigureAwait(false);

        var totalSucceeded = await _db.Set<PdfDocumentEntity>().AsNoTracking()
            .CountAsync(d => d.ProcessingState == "Ready", cancellationToken)
            .ConfigureAwait(false);

        var successRate = totalProcessed > 0
            ? Math.Round((double)totalSucceeded / totalProcessed * 100, 2)
            : 0.0;

        // Average processing time for documents that completed (UploadedAt → ProcessedAt).
        // #4678: `Select(...).DefaultIfEmpty(0).AverageAsync()` on a root query is not translatable by EF (the shape #4059
        // fixed in RagExecutionRepository.GetStatsAsync): the handler threw on every call, on an empty table and on a
        // full one. SQL AVG skips the NULLs and gives NULL when no row qualifies, so the nullable average keeps the old
        // meaning (the ready documents with an instant only), and `?? 0` is the zero for "none finished".
        var avgProcessingTimeMs = await _db.Set<PdfDocumentEntity>().AsNoTracking()
            .Where(d => d.ProcessedAt != null && d.ProcessingState == "Ready")
            .Select(d => (double?)(d.ProcessedAt!.Value - d.UploadedAt).TotalMilliseconds)
            .AverageAsync(cancellationToken).ConfigureAwait(false) ?? 0;

        return new PdfAnalyticsDto(
            TotalDocuments: totalDocuments,
            TotalProcessed: totalProcessed,
            SuccessRate: successRate,
            AvgProcessingTimeMs: Math.Round(avgProcessingTimeMs, 0));
    }
}
