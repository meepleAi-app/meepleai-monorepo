using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
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

    /// <summary>
    /// The outcome of a source-aware resolution: the presigned <see cref="Url"/>
    /// (null on placeholder/miss) and the <see cref="Kind"/> of the layer that won
    /// (null on placeholder). Lets the caller gate license/attribution on the actual
    /// winning source rather than emitting it unconditionally (epic #3470 Slice 1d-a).
    /// </summary>
    internal readonly record struct ResolvedCover(string? Url, CoverKind? Kind);

    /// <summary>
    /// Per-user resolution with the full layering (epic #3470 Slice 2c / SD3 / AC-4):
    /// L3 user-custom cover → admin per-<paramref name="context"/> override → implicit
    /// precedence. The user cover outranks the admin override; on an L3 miss the call
    /// falls through to <see cref="ResolveForContextAsync"/> (which honors the admin
    /// override then the implicit chain), NOT the context-blind <see cref="ResolvePublicAsync"/>.
    /// Owns exactly one <see cref="MeepleAiMetrics.CoverResolution"/> emission per call.
    /// </summary>
    public static async Task<string?> ResolveForUserAsync(
        SharedGameEntity sharedGame,
        UserLibraryEntryEntity? userEntry,
        CoverContext context,
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
            // ResolveForContextAsync call below emits exactly one CoverResolution
            // event for the winning layer (admin override / implicit / "placeholder"),
            // preserving the invariant that every public-facing resolution call
            // increments the counter exactly once. The L3 miss itself is observable
            // via the storage service's own logs / metrics, not duplicated here.
        }

        // L3 absent/unresolvable → the admin per-context override sits BELOW L3 (SD3),
        // so fall through to the context path (admin override → implicit precedence),
        // NOT the context-blind ResolvePublicAsync.
        return await ResolveForContextAsync(sharedGame, context, blobStorage).ConfigureAwait(false);
    }

    public static async Task<string?> ResolvePublicAsync(
        SharedGameEntity sharedGame,
        IBlobStorageService blobStorage) =>
        (await ResolvePublicWithSourceAsync(sharedGame, blobStorage).ConfigureAwait(false)).Url;

    /// <summary>
    /// Source-aware variant of <see cref="ResolvePublicAsync"/> (epic #3470 Slice 1d-a):
    /// returns both the URL and the winning <see cref="CoverKind"/> so callers can render
    /// a source-correct attribution footer instead of crediting Wikidata unconditionally.
    /// Owns the single <see cref="MeepleAiMetrics.CoverResolution"/> emission — the string
    /// overload delegates here, preserving the exactly-one-event-per-call invariant.
    /// </summary>
    public static async Task<ResolvedCover> ResolvePublicWithSourceAsync(
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
                return new ResolvedCover(url, CoverKind.Pdf);
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
                return new ResolvedCover(url, CoverKind.Bgg);
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
                return new ResolvedCover(url, CoverKind.Wikidata);
            }
        }

        EmitResolution("placeholder");
        return new ResolvedCover(null, null);
    }

    /// <summary>
    /// Epic #3470 (Slice 1c) — context-aware resolution. When the admin has pinned
    /// a <see cref="GameCoverAssignmentEntity"/> for <paramref name="context"/>, that
    /// override wins: first the rendered per-context crop (<c>GeneratedR2Key</c>),
    /// then the pinned source's base cover key (via <see cref="CoverKeyBuilder"/>).
    /// If neither resolves (crop stale AND base key absent/unreachable) the call
    /// FALLS THROUGH to the implicit precedence of <see cref="ResolvePublicAsync"/>
    /// — never a placeholder while another layer could still serve a cover.
    ///
    /// The override sits BELOW the L3 user-custom cover (SD3): this method is the
    /// public/no-user context path, mirroring <see cref="ResolvePublicAsync"/>; the
    /// per-user layering stays in <see cref="ResolveForUserAsync"/>.
    ///
    /// Metric invariant (Issue #2123): exactly one <see cref="MeepleAiMetrics.CoverResolution"/>
    /// event per call — the override emits its pinned-source tag on a win; on a
    /// fall-through the single event comes from <see cref="ResolvePublicAsync"/>.
    ///
    /// Epic #3470 (Slice 2): delegates to <see cref="ResolveForContextWithSourceAsync"/>
    /// so per-context render surfaces that also need the winning source (attribution)
    /// share one code path and one metric emission.
    /// </summary>
    public static async Task<string?> ResolveForContextAsync(
        SharedGameEntity sharedGame,
        CoverContext context,
        IBlobStorageService blobStorage) =>
        (await ResolveForContextWithSourceAsync(sharedGame, context, blobStorage).ConfigureAwait(false)).Url;

    /// <summary>
    /// Source-aware variant of <see cref="ResolveForContextAsync"/> (epic #3470 Slice 2):
    /// returns both the URL and the winning <see cref="CoverKind"/> so per-context render
    /// surfaces (e.g. the detail Hero, the catalog Card) can credit the correct source in
    /// their attribution footer instead of the implicit-precedence winner. On an override
    /// win the Kind is the pinned source's kind; on a fall-through it is the implicit
    /// precedence winner from <see cref="ResolvePublicWithSourceAsync"/>. Owns exactly one
    /// <see cref="MeepleAiMetrics.CoverResolution"/> emission per call.
    /// </summary>
    public static async Task<ResolvedCover> ResolveForContextWithSourceAsync(
        SharedGameEntity sharedGame,
        CoverContext context,
        IBlobStorageService blobStorage)
    {
        ArgumentNullException.ThrowIfNull(sharedGame);
        ArgumentNullException.ThrowIfNull(blobStorage);

        var assignment = sharedGame.CoverAssignments?
            .FirstOrDefault(a => a.Context == context);

        if (assignment is not null)
        {
            var overrideUrl = await ResolveAssignmentAsync(sharedGame, assignment, blobStorage)
                .ConfigureAwait(false);
            if (overrideUrl is not null)
            {
                EmitResolution(SourceTagFor(assignment.Source));
                return new ResolvedCover(overrideUrl, assignment.Source.ToCoverKind());
            }
            // Override present but unresolvable (crop stale AND base key
            // absent/unreachable). Intentionally NO metric emission here — the
            // ResolvePublicWithSourceAsync call below emits exactly one CoverResolution
            // event for the winning implicit layer (or "placeholder"), preserving the
            // one-event-per-call invariant.
        }

        return await ResolvePublicWithSourceAsync(sharedGame, blobStorage).ConfigureAwait(false);
    }

    /// <summary>
    /// Resolves a single admin assignment to a presigned URL, or null when it
    /// cannot serve a cover: the rendered per-context crop first, then the pinned
    /// source's base cover key. Returns null WITHOUT emitting a metric so the caller
    /// can fall through to the implicit precedence.
    /// </summary>
    private static async Task<string?> ResolveAssignmentAsync(
        SharedGameEntity sharedGame,
        GameCoverAssignmentEntity assignment,
        IBlobStorageService blobStorage)
    {
        // 1) The rendered per-context WebP crop (produced with the focal point) is
        //    a full physical R2 key — resolve it verbatim.
        if (!string.IsNullOrWhiteSpace(assignment.GeneratedR2Key))
        {
            var cropUrl = await blobStorage
                .GetPresignedUrlForRawKeyAsync(assignment.GeneratedR2Key)
                .ConfigureAwait(false);
            if (cropUrl is not null)
            {
                return cropUrl;
            }
        }

        // 2) Fall back to the pinned source's base cover key. The source→kind map
        //    is explicit (the enums do not share numeric values); the base DB key
        //    lives on the entity column for that source.
        var kind = assignment.Source.ToCoverKind();
        var baseKey = SourceDbKey(sharedGame, kind);
        if (string.IsNullOrWhiteSpace(baseKey))
        {
            return null;
        }

        return await blobStorage
            .GetPresignedUrlForRawKeyAsync(CoverKeyBuilder.PhysicalKeyFor(kind, baseKey))
            .ConfigureAwait(false);
    }

    /// <summary>Selects the entity column holding the base (suffix-free) DB key for a source kind.</summary>
    private static string? SourceDbKey(SharedGameEntity sharedGame, CoverKind kind) => kind switch
    {
        CoverKind.Pdf => sharedGame.PdfCoverR2Key,
        CoverKind.Bgg => sharedGame.BggCoverR2Key,
        CoverKind.Wikidata => sharedGame.WikidataCoverR2Key,
        CoverKind.Manual => sharedGame.ManualCoverR2Key,
        // CoverKind.User (L3) is per-user and never a catalog assignment source.
        _ => null,
    };

    /// <summary>Maps a pinned source to its <c>source</c> metric tag (mirrors the implicit-layer tags).</summary>
    private static string SourceTagFor(CoverAssignmentSource source) => source switch
    {
        CoverAssignmentSource.Pdf => "r2_pdf",
        CoverAssignmentSource.Bgg => "r2_bgg",
        CoverAssignmentSource.Wikidata => "r2_wikidata",
        CoverAssignmentSource.Manual => "r2_manual",
        _ => throw new ArgumentOutOfRangeException(nameof(source), source, "Unknown cover assignment source."),
    };

    private static void EmitResolution(string source)
    {
        MeepleAiMetrics.CoverResolution.Add(1, new KeyValuePair<string, object?>(SourceTag, source));
    }
}
