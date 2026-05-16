using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Production <see cref="IPdfClaimService"/> implementation backed by a raw SQL
/// atomic UPDATE against PostgreSQL. Row-level locking guarantees that exactly one
/// worker can transition <c>Pending → Extracting</c> for a given PDF document,
/// preventing duplicate processing under concurrent claims.
///
/// Issue #892: extracted from the runtime branch previously embedded in
/// <see cref="Api.BoundedContexts.DocumentProcessing.Application.Services.PdfProcessingPipelineService.ProcessAsync"/>.
/// The SQL body is unchanged from PR #891 — only the seam has moved.
/// </summary>
public sealed class RelationalPdfClaimService : IPdfClaimService
{
    private readonly MeepleAiDbContext _db;

    public RelationalPdfClaimService(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<bool> TryClaimPendingAsync(Guid pdfDocumentId, CancellationToken cancellationToken)
    {
        // Atomic transition Pending → Extracting in a single UPDATE.
        //   - Returns 1 row claimed if this worker won the race for a Pending PDF.
        //   - Returns 0 rows if the PDF is in any other state (already claimed by another
        //     worker, terminal, or missing). Stuck-state recovery is RetryFailedPdfsJob's
        //     responsibility, not the claim service.
        // FindAsync would suffer L1-cache staleness when another scope writes the row
        // mid-flight; an atomic UPDATE bypasses that entirely.
        var rowsClaimed = await _db.Database.ExecuteSqlInterpolatedAsync(
            $@"UPDATE pdf_documents
               SET processing_state = 'Extracting', ""ProcessingError"" = NULL
               WHERE ""Id"" = {pdfDocumentId} AND processing_state = 'Pending'",
            cancellationToken).ConfigureAwait(false);

        return rowsClaimed > 0;
    }
}
