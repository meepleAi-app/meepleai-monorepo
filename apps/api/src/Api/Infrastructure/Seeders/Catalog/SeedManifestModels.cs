namespace Api.Infrastructure.Seeders.Catalog;

/// <summary>
/// Root model for shared-games.yaml manifest.
/// </summary>
public sealed class SharedGamesManifest
{
    public List<GameManifestEntry> Games { get; set; } = [];
}

/// <summary>
/// A single game entry in the seed manifest.
///
/// Issue #2123 — BGG ToS compliance: ImageUrl and ThumbnailUrl removed. Covers
/// are now resolved at runtime by <see cref="Api.BoundedContexts.SharedGameCatalog.Application.Services.CoverUrlResolver"/>
/// from R2-hosted assets (PDF, BGG-reuploaded, Wikidata) populated by the
/// catalog enrichment pipeline (#1823 M3-M8). See
/// docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md.
/// </summary>
public sealed class GameManifestEntry
{
    public string Title { get; set; } = string.Empty;
    public int? BggId { get; set; }
    public string Language { get; set; } = "en";
    public string? PdfFile { get; set; }
}
