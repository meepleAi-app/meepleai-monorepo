namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Atomically claims a PDF document for processing.
///
/// Issue #892 (follow-up to #890/#891): extracted from
/// <see cref="PdfProcessingPipelineService.ProcessAsync"/> to eliminate the
/// runtime branch on <c>_db.Database.IsRelational()</c>. The atomic-claim semantics is the
/// load-bearing concurrency invariant of the pipeline — exposing it as a typed seam ensures
/// the invariant is preserved by interface, not by a conditional.
///
/// Production code is wired exclusively to the relational (raw SQL UPDATE) implementation.
/// Tests inject an in-memory implementation that uses tracked Find + SaveChanges.
/// </summary>
internal interface IPdfClaimService
{
    /// <summary>
    /// Attempts to transition the PDF document from <c>Pending</c> to <c>Extracting</c>.
    /// Clears <c>ProcessingError</c> on success.
    /// </summary>
    /// <returns>
    /// <c>true</c> when the claim succeeded and this caller owns the job;
    /// <c>false</c> if the PDF is not in <c>Pending</c> state (already claimed, terminal, or missing).
    /// </returns>
    Task<bool> TryClaimPendingAsync(Guid pdfDocumentId, CancellationToken cancellationToken);
}
