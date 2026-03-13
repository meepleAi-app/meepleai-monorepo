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
/// </summary>
public sealed class GameManifestEntry
{
    public string Title { get; set; } = string.Empty;
    public int? BggId { get; set; }
    public string Language { get; set; } = "en";
    public string? PdfFile { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
}
