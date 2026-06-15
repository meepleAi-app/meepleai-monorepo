namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCoverBatch;

/// <summary>
/// Aggregated outcome of <see cref="EnrichCatalogCoverBatchCommand"/>. Carries
/// per-bucket counters and a per-game audit trail so the admin UI can render a
/// progress summary plus a drill-down list. Issue #2123 + #2157.
/// </summary>
/// <param name="TotalRequested">Equals <c>GameIds.Count</c> — even when the handler short-circuits on cancellation, this reflects the requested batch size.</param>
/// <param name="SuccessCount">Number of games where M8 returned <see cref="EnrichCatalogCover.EnrichCatalogCoverResult.Success"/>.</param>
/// <param name="SkippedCount">Number of games where M8 returned <see cref="EnrichCatalogCover.EnrichCatalogCoverResult.Skipped"/> for an expected business reason (missing QID, license not whitelisted, recently enriched, …).</param>
/// <param name="FailedCount">Number of games where M8 returned <see cref="EnrichCatalogCover.EnrichCatalogCoverResult.Failed"/> OR threw an unhandled exception captured by the batch.</param>
/// <param name="PerGame">Ordered per-game outcomes, enumeration matches the input <c>GameIds</c> order. Useful for UI drill-down + retry filtering.</param>
/// <param name="FailedDetails">Issue #2157 — exception drill-down payload for each per-game failure path (child-timeout / http-error / circuit-open / unhandled-exception). <see langword="null"/> when no exception was caught (every failure originated from an in-band <see cref="EnrichCatalogCover.EnrichCatalogCoverResult.Failed"/> result, which already carries its own reason on <see cref="EnrichCatalogCoverBatchEntry.Reason"/>). Optional + null default preserves backward compat for the JSON contract.</param>
internal sealed record EnrichCatalogCoverBatchResult(
    int TotalRequested,
    int SuccessCount,
    int SkippedCount,
    int FailedCount,
    IReadOnlyList<EnrichCatalogCoverBatchEntry> PerGame,
    IReadOnlyList<EnrichCatalogCoverBatchFailedDetail>? FailedDetails = null);

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

/// <summary>
/// Issue #2157 — exception drill-down for a single per-game failure. Captured
/// only when the batch handler's defense-in-depth catch hierarchy fires
/// (HTTP timeout leak, HttpRequestException, BrokenCircuitException, or any
/// other unhandled exception). The admin UI uses this payload to distinguish
/// transient (retryable) vs permanent (data-fix-required) failures inside the
/// <c>failed</c> bucket of <see cref="EnrichCatalogCoverBatchResult"/>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="ExceptionMessage"/> is the raw <see cref="Exception.Message"/>.
/// The producing exceptions (<see cref="System.Net.Http.HttpRequestException"/>,
/// <see cref="System.Threading.Tasks.TaskCanceledException"/>, Polly
/// <c>BrokenCircuitException</c>) do NOT include internal file paths or
/// credentials in their message text — they describe the failure mode only.
/// If a future exception type with sensitive payload is added, sanitisation
/// MUST happen at the catch site before constructing this record.
/// </para>
/// </remarks>
/// <param name="GameId">The game id this failure refers to (matches a <see cref="EnrichCatalogCoverBatchEntry.GameId"/>).</param>
/// <param name="ExceptionType">Short type name of the exception (e.g. <c>"TaskCanceledException"</c>, <c>"HttpRequestException"</c>, <c>"BrokenCircuitException"</c>).</param>
/// <param name="ExceptionMessage">Sanitized exception message — see remarks for sanitisation policy.</param>
internal sealed record EnrichCatalogCoverBatchFailedDetail(
    Guid GameId,
    string ExceptionType,
    string ExceptionMessage);
