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
/// <param name="TriggeredByUserId">Admin user id captured for audit logging.</param>
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
