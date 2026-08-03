using System.Globalization;

namespace Api.SharedKernel.Domain.Covers;

/// <summary>
/// The four cover sources resolved by <c>CoverUrlResolver</c>, in precedence
/// order User (L3) → Pdf (L4) → Bgg (L2.5) → Wikidata (L2). Each source uses a
/// distinct R2 physical-key suffix convention (ADR-087 D5-A, issue #3384).
/// </summary>
internal enum CoverKind
{
    /// <summary>User-uploaded custom cover — physical key adds <c>.webp</c>.</summary>
    User,

    /// <summary>PDF-derived cover (rulebook page) — physical key adds <c>-preview.webp</c>.</summary>
    Pdf,

    /// <summary>BGG re-uploaded cover — physical key IS the DB key (extension already embedded).</summary>
    Bgg,

    /// <summary>Wikidata/Commons cover — physical key adds <c>.webp</c>.</summary>
    Wikidata,

    /// <summary>Admin manual-URL cover (epic #3470): fetched + re-hosted on R2 — physical key adds <c>.webp</c>.</summary>
    Manual,
}

/// <summary>
/// A cover's storage identity: the suffix-free <see cref="DbKey"/> persisted in
/// the DB column, and the <see cref="PhysicalKey"/> actually PUT to / GET from R2.
/// </summary>
internal readonly record struct CoverKey(CoverKind Kind, string DbKey, string PhysicalKey);

/// <summary>
/// Issue #3384 (ADR-087 D5-A) — the SINGLE source of truth for the cover R2 key
/// convention. Every writer (<c>CoverR2UploadPipeline</c>,
/// <c>BggCoverUploadPipeline</c>, <c>PdfCoverUploadPipeline</c>,
/// <c>UploadCustomCoverCommandHandler</c>) and the read side
/// (<c>CoverUrlResolver</c> + the PDF orphan / delete / materialize sites)
/// derive keys through here, so the write-key and the read-key coincide by
/// construction. This makes the <c>.webp.webp</c> double-suffix drift — the bug
/// whose root cause (<c>PathSecurity.ValidateIdentifier</c> rejecting the
/// suffix/slash) recurred three times — structurally impossible.
/// </summary>
internal static class CoverKeyBuilder
{
    private const string DefaultBggExtension = ".jpg";

    /// <summary>User custom cover (L3): <c>user-covers/{userId:D}/{gameId:D}/cover</c>.</summary>
    public static CoverKey ForUser(Guid userId, Guid gameId) =>
        Build(CoverKind.User, $"user-covers/{userId:D}/{gameId:D}/cover");

    /// <summary>PDF-derived cover (L4): <c>covers/pdf/{pdfDocumentId:D}/cover</c>.</summary>
    public static CoverKey ForPdf(Guid pdfDocumentId) =>
        Build(CoverKind.Pdf, $"covers/pdf/{pdfDocumentId:D}/cover");

    /// <summary>Wikidata/Commons cover (L2): <c>covers/{gameId:D}/cover</c>.</summary>
    public static CoverKey ForWikidata(Guid gameId) =>
        Build(CoverKind.Wikidata, $"covers/{gameId:D}/cover");

    /// <summary>Admin manual-URL cover (epic #3470): <c>covers/manual/{gameId:D}/cover</c>.</summary>
    public static CoverKey ForManual(Guid gameId) =>
        Build(CoverKind.Manual, $"covers/manual/{gameId:D}/cover");

    /// <summary>
    /// BGG re-uploaded cover (L2.5): <c>bgg-covers/{bggId}/cover{ext}</c>. The
    /// extension is embedded in the key (BGG keeps its original image format), so
    /// the physical key equals the DB key — no suffix is appended at read time.
    /// </summary>
    public static CoverKey ForBgg(int bggId, string sourceExtension) =>
        Build(
            CoverKind.Bgg,
            $"bgg-covers/{bggId.ToString(CultureInfo.InvariantCulture)}/cover{NormalizeBggExtension(sourceExtension)}");

    /// <summary>
    /// Reconstructs the physical R2 object key from a DB-stored key. This is the
    /// ONE place the per-kind suffix rule lives — the resolver calls this instead
    /// of hardcoding <c>+ ".webp"</c> / <c>+ "-preview.webp"</c> / nothing.
    /// </summary>
    public static string PhysicalKeyFor(CoverKind kind, string dbKey)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(dbKey);
        return dbKey + SuffixFor(kind);
    }

    /// <summary>
    /// Normalizes a BGG source image extension to a supported, dot-prefixed,
    /// lowercase form; unsupported or empty extensions fall back to <c>.jpg</c>.
    /// Exposed so <c>BggCoverUploadPipeline</c> can derive the matching
    /// Content-Type from the SAME normalized extension that shapes the key.
    /// </summary>
    public static string NormalizeBggExtension(string? extension)
    {
        if (string.IsNullOrWhiteSpace(extension))
        {
            return DefaultBggExtension;
        }

        var ext = extension.StartsWith('.') ? extension : "." + extension;
        ext = ext.ToLowerInvariant();
        return ext switch
        {
            ".jpg" or ".jpeg" or ".png" or ".gif" or ".webp" => ext,
            _ => DefaultBggExtension,
        };
    }

    private static string SuffixFor(CoverKind kind) => kind switch
    {
        CoverKind.User => ".webp",
        CoverKind.Pdf => "-preview.webp",
        CoverKind.Bgg => string.Empty,
        CoverKind.Wikidata => ".webp",
        CoverKind.Manual => ".webp",
        _ => throw new ArgumentOutOfRangeException(nameof(kind), kind, "Unknown cover kind."),
    };

    private static CoverKey Build(CoverKind kind, string dbKey) =>
        new(kind, dbKey, dbKey + SuffixFor(kind));
}
