namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Outcome of a PDF dedup evaluation: whether the caller should reuse an
/// existing <see cref="Domain.Entities.PdfDocument"/> or proceed with a new upload.
/// </summary>
public enum PdfDedupDecision
{
    NewUpload,
    ReuseExisting
}

/// <summary>
/// Result of <see cref="IPdfDeduplicationService.EvaluateAsync"/>: the decision,
/// the matched document id (when reusable), and the content hash that was evaluated.
/// </summary>
public sealed record PdfDedupResult(PdfDedupDecision Decision, Guid? ExistingPdfDocumentId, string ContentHash);

/// <summary>
/// Centralizes the PDF deduplication rule (SHA-256 content hash based) that was
/// previously duplicated and divergent between <c>AddRulebookCommandHandler</c>
/// (reuse via EntityLink) and <c>CompleteChunkedUploadCommandHandler</c> (reject).
///
/// Rule: catalog uploads (<c>sharedGameId</c> set) dedup GLOBALLY across the
/// catalog; private uploads (<c>privateGameId</c> set) dedup PER-USER only.
/// A match left in <see cref="Domain.Enums.PdfProcessingState.Failed"/> is treated
/// as not reusable (a fresh upload is required).
/// </summary>
public interface IPdfDeduplicationService
{
    /// <summary>Computes the SHA-256 hex digest (lowercase) of the given content stream.</summary>
    Task<string> ComputeContentHashAsync(Stream content, CancellationToken cancellationToken);

    /// <summary>
    /// Evaluates whether an existing PDF with the same content hash can be reused,
    /// scoping the lookup to the catalog (global) or the uploading user (private).
    /// </summary>
    Task<PdfDedupResult> EvaluateAsync(
        string contentHash,
        Guid? sharedGameId,
        Guid? privateGameId,
        Guid userId,
        CancellationToken cancellationToken);
}
