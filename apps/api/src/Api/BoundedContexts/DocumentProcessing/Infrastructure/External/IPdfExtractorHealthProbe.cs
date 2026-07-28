namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Liveness probe for the PDF extraction pipeline's structural (heading-aware) capability.
/// Unlike the Python service's own <c>/health</c> route (liveness-only), this probe exercises
/// the real <c>/api/v1/extract</c> call path with a known tiny PDF and asserts the response
/// actually carries structured <c>elements[]</c>. A stale/misconfigured extractor that only
/// emits flat text (no elements) would silently produce headingless chunks on re-index — this
/// probe lets callers (e.g. the bulk re-index orchestrator) refuse to run in that case.
/// </summary>
internal interface IPdfExtractorHealthProbe
{
    /// <summary>
    /// Returns true when the extractor is reachable and returns at least one structured element
    /// for a known-good sample PDF; false on any failure (network error, timeout, non-success
    /// status, malformed response, or an empty/missing <c>elements</c> array).
    /// </summary>
    Task<bool> IsHealthyAsync(CancellationToken ct);
}
