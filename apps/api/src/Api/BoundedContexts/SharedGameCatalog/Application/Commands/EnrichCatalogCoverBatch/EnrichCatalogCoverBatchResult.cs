namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCoverBatch;

/// <summary>
/// Aggregated outcome of <see cref="EnrichCatalogCoverBatchCommand"/>. Carries
/// per-bucket counters and a per-game audit trail so the admin UI can render a
/// progress summary plus a drill-down list. Issue #2123.
/// </summary>
/// <param name="TotalRequested">Equals <c>GameIds.Count</c> — even when the handler short-circuits on cancellation, this reflects the requested batch size.</param>
/// <param name="SuccessCount">Number of games where M8 returned <see cref="EnrichCatalogCover.EnrichCatalogCoverResult.Success"/>.</param>
/// <param name="SkippedCount">Number of games where M8 returned <see cref="EnrichCatalogCover.EnrichCatalogCoverResult.Skipped"/> for an expected business reason (missing QID, license not whitelisted, recently enriched, …).</param>
/// <param name="FailedCount">Number of games where M8 returned <see cref="EnrichCatalogCover.EnrichCatalogCoverResult.Failed"/> OR threw an unhandled exception captured by the batch.</param>
/// <param name="PerGame">Ordered per-game outcomes, enumeration matches the input <c>GameIds</c> order. Useful for UI drill-down + retry filtering.</param>
internal sealed record EnrichCatalogCoverBatchResult(
    int TotalRequested,
    int SuccessCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<EnrichCatalogCoverBatchEntry> PerGame);

/// <summary>
/// Per-game audit entry produced by <see cref="EnrichCatalogCoverBatchCommand"/>.
/// </summary>
/// <param name="GameId">The <see cref="SharedGameCatalog.Domain.Aggregates.SharedGame"/> id this entry refers to.</param>
/// <param name="Outcome">Stable, machine-readable bucket: <c>success</c> | <c>skipped</c> | <c>failed</c>.</param>
/// <param name="Reason">Stable, machine-readable reason / skip-code propagated from the M8 single-entry result. <see langword="null"/> only for <see cref="Outcome"/> = <c>"success"</c>.</param>
internal sealed record EnrichCatalogCoverBatchEntry(
    Guid GameId,
    string Outcome,
    string? Reason);
