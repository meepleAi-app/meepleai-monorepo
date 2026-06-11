using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.EnrichCatalogCover;

/// <summary>
/// Issue #1823 Phase B M8 — orchestrates the Wikidata L2 cover enrichment
/// pipeline for a single <see cref="SharedGameCatalog.Domain.Aggregates.SharedGame"/>.
/// Reads <c>WikidataQid</c> from the aggregate, fetches the
/// <c>wdt:P18</c> image filename (M3), validates the Commons license against
/// the DEC-3c whitelist (M4), downloads the raw image bytes (M4 extension),
/// generates a 200x300 WebP variant (M6), uploads to R2 with a deterministic
/// per-game key (M7), then stamps the four cover columns +
/// <c>WikidataQidLastVerifiedAt</c> on the aggregate (DEC-3i).
/// </summary>
/// <remarks>
/// Outcome is reported via <see cref="EnrichCatalogCoverResult"/>:
/// <list type="bullet">
///   <item><see cref="EnrichCatalogCoverResult.Success"/> on full pipeline success.</item>
///   <item><see cref="EnrichCatalogCoverResult.Skipped"/> for expected business
///   reasons (QID missing, image not on Wikidata, license not whitelisted, recently
///   enriched, etc.). Per CLAUDE.md pitfall #2568 these are NEVER thrown.</item>
///   <item><see cref="EnrichCatalogCoverResult.Failed"/> for technical errors
///   (image-decoding error, R2 upload error). Idempotent retry is safe because
///   the R2 key is deterministic.</item>
/// </list>
/// <para>
/// Game-not-found surfaces as <see cref="Api.Middleware.Exceptions.NotFoundException"/>
/// (HTTP 404 via the exception middleware).
/// </para>
/// <para>
/// <see cref="ForceRefresh"/> (Wave 3 M12): when <see langword="true"/> the
/// handler skips the 90-day freshness window check (ADR DEC-3i preparation) and
/// always runs the full pipeline. Used by the admin trigger endpoint to force
/// a re-enrichment for dogfood / edge-case testing. The default (<see langword="false"/>)
/// preserves the scheduler's freshness skip semantics.
/// </para>
/// </remarks>
internal sealed record EnrichCatalogCoverCommand(Guid GameId, bool ForceRefresh = false)
    : ICommand<EnrichCatalogCoverResult>;
