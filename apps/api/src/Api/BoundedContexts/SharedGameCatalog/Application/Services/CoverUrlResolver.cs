using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Observability;
using Api.Services.Pdf;
using Api.SharedKernel.Domain.Covers;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Issue #1852 (Gap A) + Gap G2 (2026-06-08): centralizes cover-URL resolution
/// with the priority L3 (user custom) -> L4 (PDF-derived) -> L2.5 (BGG re-uploaded)
/// -> L2 (Wikidata) -> null. Each layer falls through to the next when its R2 key
/// is missing or the blob storage cannot mint a presigned URL (returns null in dev / local).
///
/// P1 fix (2026-07-14): L3/L4/L2 resolve via
/// <see cref="IBlobStorageService.GetPresignedUrlForRawKeyAsync"/>, passing the
/// exact physical object key these layers write deterministically
/// (<c>{key}.webp</c> / <c>{key}-preview.webp</c>). The previous
/// <c>GetPresignedDownloadUrlAsync(fileId, category, resourceKey)</c> call
/// validated BOTH arguments with <c>PathSecurity.ValidateIdentifier</c> (which
/// rejects <c>/</c> and <c>.</c>) and then did categorized prefix discovery —
/// neither of which matches these layers' raw, slash-containing key shape, so
/// the call always threw internally and returned null (silent no-op: covers
/// never resolved).
/// L2.5 (BGG) now resolves via <see cref="IBlobStorageService.GetPresignedUrlForRawKeyAsync"/>
/// using the full deterministic key written by
/// <c>BggCoverUploadPipeline</c> (Issue #2947): <c>bgg-covers/{bggId}/cover{ext}</c>,
/// with no suffix appended (BGG keeps its original image extension).
///
/// Issue #2123 (BGG ToS compliance): every resolution outcome — including the
/// terminal <c>null</c> path that triggers a placeholder render on the FE —
/// emits a <see cref="MeepleAiMetrics.CoverResolution"/> measurement tagged
/// with the winning source layer (<c>r2_user|r2_pdf|r2_bgg|r2_wikidata|placeholder</c>).
/// Ops dashboards alert when <c>source="placeholder"</c> exceeds 80% of total
/// resolutions, signalling that the QID+M8 batch needs to run or that the
/// upstream Wikimedia pipeline is failing. See
/// <c>docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md</c> §6.3.
/// </summary>
internal static class CoverUrlResolver
{
    private const string SourceTag = "source";

    public static async Task<string?> ResolveForUserAsync(
        SharedGameEntity sharedGame,
        UserLibraryEntryEntity? userEntry,
        IBlobStorageService blobStorage)
    {
        ArgumentNullException.ThrowIfNull(sharedGame);
        ArgumentNullException.ThrowIfNull(blobStorage);

        if (!string.IsNullOrWhiteSpace(userEntry?.CustomCoverR2Key))
        {
            var url = await blobStorage
                .GetPresignedUrlForRawKeyAsync(
                    CoverKeyBuilder.PhysicalKeyFor(CoverKind.User, userEntry.CustomCoverR2Key))
                .ConfigureAwait(false);
            if (url is not null)
            {
                EmitResolution("r2_user");
                return url;
            }
            // L3 miss while the key was present (R2 unreachable in dev, blob
            // expired, etc.). Intentionally NO metric emission here — the
            // recursive call to ResolvePublicAsync below emits exactly one
            // CoverResolution event for the winning fallback layer (or
            // "placeholder" if all layers miss), preserving the invariant that
            // every public-facing resolution call increments the counter
            // exactly once. The L3 miss itself is observable via the storage
            // service's own logs / metrics, not duplicated here.
        }

        return await ResolvePublicAsync(sharedGame, blobStorage).ConfigureAwait(false);
    }

    public static async Task<string?> ResolvePublicAsync(
        SharedGameEntity sharedGame,
        IBlobStorageService blobStorage)
    {
        ArgumentNullException.ThrowIfNull(sharedGame);
        ArgumentNullException.ThrowIfNull(blobStorage);

        if (!string.IsNullOrWhiteSpace(sharedGame.PdfCoverR2Key))
        {
            var url = await blobStorage
                .GetPresignedUrlForRawKeyAsync(
                    CoverKeyBuilder.PhysicalKeyFor(CoverKind.Pdf, sharedGame.PdfCoverR2Key))
                .ConfigureAwait(false);
            if (url is not null)
            {
                EmitResolution("r2_pdf");
                return url;
            }
        }

        // L2.5 BGG re-uploaded cover (Gap G2 / Issue #2947)
        // The DB key is the FULL deterministic physical object key composed by
        // BggCoverUploadPipeline (bgg-covers/{bggId}/cover{ext}). Unlike L4/L2,
        // NO suffix is appended: BGG keeps its original image extension, so the
        // stored key IS the physical key. Resolved via the raw-key method (the
        // legacy GetPresignedDownloadUrlAsync validated the key with
        // PathSecurity.ValidateIdentifier, which rejects '/' and '.').
        if (!string.IsNullOrWhiteSpace(sharedGame.BggCoverR2Key))
        {
            var url = await blobStorage
                .GetPresignedUrlForRawKeyAsync(
                    CoverKeyBuilder.PhysicalKeyFor(CoverKind.Bgg, sharedGame.BggCoverR2Key))
                .ConfigureAwait(false);
            if (url is not null)
            {
                EmitResolution("r2_bgg");
                return url;
            }
        }

        // L2 Wikidata cover
        if (!string.IsNullOrWhiteSpace(sharedGame.WikidataCoverR2Key))
        {
            var url = await blobStorage
                .GetPresignedUrlForRawKeyAsync(
                    CoverKeyBuilder.PhysicalKeyFor(CoverKind.Wikidata, sharedGame.WikidataCoverR2Key))
                .ConfigureAwait(false);
            if (url is not null)
            {
                EmitResolution("r2_wikidata");
                return url;
            }
        }

        EmitResolution("placeholder");
        return null;
    }

    private static void EmitResolution(string source)
    {
        MeepleAiMetrics.CoverResolution.Add(1, new KeyValuePair<string, object?>(SourceTag, source));
    }
}
