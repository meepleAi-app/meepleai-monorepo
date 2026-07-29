using Api.BoundedContexts.DocumentProcessing.Domain.Enums;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// #3373 D1 (ADR-087): bounded-retry classification for PDF cover generation.
///
/// The two cover generators — <c>PdfProcessingPipelineService</c> (inline ingest) and
/// <c>BackfillPdfCoversJob</c> — distinguish a TRANSIENT infra failure (R2 upload / DB
/// save threw, PDF binary transiently unreachable) from a PERMANENT one (extractor
/// rejected the pages, corrupt image). A transient failure is retry-eligible: the PDF
/// returns to <see cref="PdfCoverGenerationStatus.Pending"/> so <c>BackfillPdfCoversJob</c>
/// re-picks it on its next 30-min tick (a natural backoff), until <see cref="MaxAttempts"/>
/// is reached — then it becomes terminal <see cref="PdfCoverGenerationStatus.Failed"/>.
/// Permanent failures call the Failed transition directly and never reach this policy.
///
/// This replaces the pre-D1 behaviour where every failure was immediately terminal, so a
/// momentary R2 blip during ingest permanently blocked a cover until a manual operator reset.
/// </summary>
public static class PdfCoverRetryPolicy
{
    /// <summary>Retry budget before a transient failure becomes terminal (DEC-3j mirror: 3).</summary>
    public const int MaxAttempts = 3;

    /// <summary>
    /// Given the attempt count BEFORE this failure, returns the incremented count and the
    /// resulting status: <see cref="PdfCoverGenerationStatus.Pending"/> (retry-eligible) while
    /// under <see cref="MaxAttempts"/>, else terminal <see cref="PdfCoverGenerationStatus.Failed"/>.
    /// </summary>
    public static (PdfCoverGenerationStatus Status, int Attempts) NextAfterTransientFailure(int currentAttempts)
    {
        var attempts = currentAttempts + 1;
        var status = attempts >= MaxAttempts
            ? PdfCoverGenerationStatus.Failed
            : PdfCoverGenerationStatus.Pending;
        return (status, attempts);
    }
}
