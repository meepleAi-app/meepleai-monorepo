using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

/// <summary>
/// Issue #1823 Phase E F2 — admin bulk re-trigger of Wikidata cover enrichment
/// for a set of dead-letter attempts. Each input id is resolved to the parent
/// <c>SharedGameId</c> and re-dispatched through
/// <see cref="Services.IWikidataCoverEnrichmentRunner"/> with
/// <c>forceRefresh=true</c>, producing a fresh
/// <see cref="Domain.Aggregates.WikidataCoverEnrichmentAttempt"/> row per
/// invocation. The previous dead-letter row is retained for the 7-day DEC-3j
/// retention window so the audit trail is preserved.
/// </summary>
/// <param name="AttemptIds">Dead-letter attempt ids selected on the admin page (max 50 enforced by the validator).</param>
/// <param name="TriggeredByUserId">
/// Admin user id of the operator who initiated the bulk-retry. Captured in the
/// structured log line AND threaded into each per-row runner invocation so the
/// new attempt rows persist <c>TriggeredByAdminUserId</c>. Same semantics as
/// <see cref="AdminEnrichWikidataCoverCommand.TriggeredByUserId"/>; surfaces on
/// the F6 timeline drawer + F4 SSE payload. Issue #1823 Phase F F6.
/// </param>
internal sealed record AdminBulkRetryWikidataCoverCommand(
    IReadOnlyList<Guid> AttemptIds,
    Guid TriggeredByUserId) : ICommand<AdminBulkRetryWikidataCoverResult>;

/// <summary>
/// Per-row outcome on the bulk-retry result envelope.
/// </summary>
public sealed record AdminBulkRetryRow(
    Guid AttemptId,
    Guid? GameId,
    string Outcome,
    string? Reason)
{
    /// <summary>Re-dispatched and the runner produced a Success / Skipped / Failed-with-retry row.</summary>
    public const string OutcomeTriggered = "triggered";

    /// <summary>The supplied attempt id was not found (already swept, or typo).</summary>
    public const string OutcomeNotFound = "not-found";

    /// <summary>The runner threw before completing — the attempt was NOT re-dispatched.</summary>
    public const string OutcomeFailed = "failed";
}

/// <summary>
/// Result envelope for <see cref="AdminBulkRetryWikidataCoverCommand"/>.
/// </summary>
public sealed record AdminBulkRetryWikidataCoverResult(
    int TriggeredCount,
    int NotFoundCount,
    int FailedCount,
    IReadOnlyList<AdminBulkRetryRow> Rows);
