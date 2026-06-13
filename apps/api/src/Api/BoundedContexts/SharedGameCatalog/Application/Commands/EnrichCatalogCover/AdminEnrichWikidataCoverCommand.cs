using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

/// <summary>
/// Issue #1823 Wave 3 M12 — admin-triggered Wikidata cover enrichment for a
/// single shared game. Delegates the actual enrich+record workflow to
/// <see cref="Services.IWikidataCoverEnrichmentRunner"/> so the admin path
/// produces the same audit trail (<c>WikidataCoverEnrichmentAttempt</c> row)
/// as the M9 scheduler tick.
/// </summary>
/// <param name="GameId">Target shared-game id.</param>
/// <param name="ForceRefresh">When <see langword="true"/>, the M8 90-day freshness window is bypassed.</param>
/// <param name="TriggeredByUserId">
/// Admin user id of the operator pressing the trigger button. Captured in the
/// structured log line emitted by the handler (<c>AdminEnrichWikidataCover:
/// triggered by user {UserId} for game {GameId}</c>) AND threaded into the
/// runner so it is persisted on the new
/// <c>WikidataCoverEnrichmentAttempt.TriggeredByAdminUserId</c> column —
/// surfacing "triggered by admin X" on the F6 timeline drawer + F4 SSE payload
/// without a separate audit table. Issue #1823 Phase F F6 (was log-only in M12,
/// promoted to persisted in F6 per #2255 carry-forward).
/// </param>
internal sealed record AdminEnrichWikidataCoverCommand(
    Guid GameId,
    bool ForceRefresh,
    Guid TriggeredByUserId) : ICommand<AdminEnrichWikidataCoverResult>;

/// <summary>
/// Flat DTO returned to the admin endpoint. Encodes the three terminal outcome
/// categories produced by <see cref="EnrichCatalogCoverCommand"/> + the optional
/// success payload.
/// </summary>
/// <param name="Outcome">Either <c>"success"</c>, <c>"skipped"</c>, or <c>"failed"</c>.</param>
/// <param name="Reason">Skip / failure reason; <see langword="null"/> for success.</param>
/// <param name="Details">Optional failure detail (typically an exception message).</param>
/// <param name="R2Key">R2 key on success (WITHOUT <c>.webp</c> suffix); <see langword="null"/> otherwise.</param>
/// <param name="License">Whitelisted license string on success.</param>
/// <param name="Attribution">Optional Artist credit on success.</param>
/// <param name="SourceUrl">Wikidata entity URL on success.</param>
internal sealed record AdminEnrichWikidataCoverResult(
    string Outcome,
    string? Reason,
    string? Details,
    string? R2Key,
    string? License,
    string? Attribution,
    string? SourceUrl)
{
    public const string OutcomeSuccess = "success";
    public const string OutcomeSkipped = "skipped";
    public const string OutcomeFailed = "failed";

    public static AdminEnrichWikidataCoverResult FromSuccess(EnrichCatalogCoverResult.Success success) =>
        new(OutcomeSuccess, Reason: null, Details: null,
            R2Key: success.R2Key,
            License: success.License,
            Attribution: success.Attribution,
            SourceUrl: success.SourceUrl);

    public static AdminEnrichWikidataCoverResult FromSkipped(EnrichCatalogCoverResult.Skipped skipped) =>
        new(OutcomeSkipped, Reason: skipped.Reason, Details: null,
            R2Key: null, License: null, Attribution: null, SourceUrl: null);

    public static AdminEnrichWikidataCoverResult FromFailed(EnrichCatalogCoverResult.Failed failed) =>
        new(OutcomeFailed, Reason: failed.Reason, Details: failed.Details,
            R2Key: null, License: null, Attribution: null, SourceUrl: null);
}
