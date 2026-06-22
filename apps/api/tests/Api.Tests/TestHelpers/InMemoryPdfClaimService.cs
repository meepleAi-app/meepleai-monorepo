using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.Tests.TestHelpers;

/// <summary>
/// Test-only <see cref="IPdfClaimService"/> implementation backed by the EF Core
/// InMemory provider. Uses tracked <c>Find</c> + <c>SaveChanges</c> because the
/// InMemory provider does not support relational <c>ExecuteSqlInterpolatedAsync</c>.
///
/// Issue #892: replaces the runtime <c>IsRelational()</c> branch previously
/// embedded in <c>PdfProcessingPipelineService.ProcessAsync</c>.
/// </summary>
internal sealed class InMemoryPdfClaimService : IPdfClaimService
{
    private readonly MeepleAiDbContext _db;

    public InMemoryPdfClaimService(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<bool> TryClaimPendingAsync(Guid pdfDocumentId, CancellationToken cancellationToken)
    {
        var existing = await _db.PdfDocuments
            .FindAsync(new object[] { pdfDocumentId }, cancellationToken)
            .ConfigureAwait(false);

        if (existing == null || !string.Equals(existing.ProcessingState, "Pending", StringComparison.Ordinal))
        {
            return false;
        }

        existing.ProcessingState = "Extracting";
        existing.ProcessingError = null;
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        return true;
    }
}
