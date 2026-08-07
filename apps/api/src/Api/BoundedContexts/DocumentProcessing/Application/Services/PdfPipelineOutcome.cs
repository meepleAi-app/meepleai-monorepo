namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Explicit outcome of <see cref="IPdfProcessingPipelineService.ProcessAsync"/> (Issue #3592).
///
/// <para>The pipeline used to return a bare <c>Task</c>, so every early exit — a refused claim, a
/// concurrency abort, a document that vanished — was indistinguishable from a full success. The
/// queue worker inferred success from that silence and marked the job (and all five steps)
/// <c>Completed</c> without anything having been processed: a terminal state no retry reclaims,
/// plus step metrics and ETA fed with invented durations.</para>
///
/// <para>Only <see cref="Processed"/> means the document was actually taken through the pipeline.</para>
/// </summary>
internal enum PdfPipelineOutcome
{
    /// <summary>The full pipeline ran to the end: text extracted, chunked, embedded, indexed.</summary>
    Processed,

    /// <summary>
    /// The atomic Pending-claim refused the document: it is already claimed by another worker or
    /// sits in a terminal state. This job did no work — someone else owns the outcome.
    /// </summary>
    SkippedNotClaimed,

    /// <summary>The row disappeared between the claim and the reload (deleted concurrently).</summary>
    DocumentMissing,

    /// <summary>
    /// The pipeline marked the document Failed itself (no usable chunks, extraction error,
    /// unhandled exception). The failure is already persisted on the document.
    /// </summary>
    Failed,

    /// <summary>
    /// A concurrency conflict aborted the run mid-flight — an admin mutation won the race. The
    /// document keeps its intermediate state deliberately, for the next tick to re-read.
    /// </summary>
    AbortedConcurrency,
}
