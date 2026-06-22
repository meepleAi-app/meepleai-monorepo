using YamlDotNet.Serialization;

namespace Api.Infrastructure.Seeders;

/// <summary>
/// YAML manifest model for catalog seeding. Deserialized from Manifests/{profile}.yml.
/// </summary>
internal sealed class SeedManifest
{
    public string Profile { get; set; } = string.Empty;
    public SeedManifestCatalog Catalog { get; set; } = new();

    /// <summary>
    /// Validates manifest integrity. Returns list of error messages (empty = valid).
    /// </summary>
    public List<string> Validate(SeedProfile? expectedProfile = null)
    {
        var errors = new List<string>();

        if (expectedProfile.HasValue &&
            !string.Equals(Profile, expectedProfile.Value.ToString(), StringComparison.OrdinalIgnoreCase))
        {
            errors.Add($"Profile mismatch: manifest says '{Profile}' but expected '{expectedProfile}'");
        }

        if (Catalog?.Games is null || Catalog.Games.Count == 0)
            return errors;

        var seenBggIds = new HashSet<int>();
        foreach (var game in Catalog.Games)
        {
            if (string.IsNullOrWhiteSpace(game.Title))
                errors.Add($"Game with bggId={game.BggId} has empty title");

            if (game.BggId.HasValue && game.BggId.Value <= 0)
                errors.Add($"Game '{game.Title}' has invalid bggId={game.BggId}");

            if (game.BggId.HasValue && !seenBggIds.Add(game.BggId.Value))
                errors.Add($"Duplicate bggId={game.BggId} ('{game.Title}')");

            if (string.IsNullOrWhiteSpace(game.Language))
                game.Language = "en";

            // Issue #2123: "bggEnhanced" flag retired in favor of presence/absence of Description.
            // No invariant check needed: an entry without description is a legitimate minimal
            // entry handled by GameSeeder.CreateMinimalGame.
        }

        if (Catalog.Games.Any(g => g.SeedAgent) && Catalog.DefaultAgent is null)
            errors.Add("Games with seedAgent=true require a defaultAgent section");

        if (Catalog.DefaultAgent is not null)
        {
            if (string.IsNullOrWhiteSpace(Catalog.DefaultAgent.Name))
                errors.Add("defaultAgent.name is required");
            if (string.IsNullOrWhiteSpace(Catalog.DefaultAgent.Model))
                errors.Add("defaultAgent.model is required");
        }

        return errors;
    }
}

internal sealed class SeedManifestCatalog
{
    public List<SeedManifestGame> Games { get; set; } = new();
    public SeedManifestAgent? DefaultAgent { get; set; }
}

internal sealed class SeedManifestGame
{
    public string Title { get; set; } = string.Empty;
    public int? BggId { get; set; }
    public string Language { get; set; } = "en";
    public string? Pdf { get; set; }
    public bool SeedAgent { get; set; }

    // Issue #2123 — BGG ToS compliance: image URL properties removed.
    // The catalog covers pipeline (#1823 M3-M8) produces R2-hosted covers
    // resolved at runtime via CoverUrlResolver. Seed manifests no longer
    // carry cover URLs; SharedGameEntity.ImageUrl/ThumbnailUrl are nullable
    // and stay null until M8 populates WikidataCoverR2Key or a PDF upload
    // populates PdfCoverR2Key. Frontend renders deterministic placeholders
    // (cover-utils.ts) when CoverUrl is null.
    //
    // Removed properties (would silently re-introduce BGG URLs):
    //   - BggEnhanced       → semantically replaced by presence of Description
    //   - ImageUrl
    //   - ThumbnailUrl
    //   - FallbackImageUrl
    //   - FallbackThumbnailUrl

    // Enhanced metadata fields (all optional, populated by bgg-fetcher tool or manual editing)
    public string? Description { get; set; }
    public int? YearPublished { get; set; }
    public int? MinPlayers { get; set; }
    public int? MaxPlayers { get; set; }
    public int? PlayingTime { get; set; }
    public int? MinAge { get; set; }
    public double? AverageRating { get; set; }
    public double? AverageWeight { get; set; }
    public string? RulesUrl { get; set; }
    public List<string>? Categories { get; set; }
    public List<string>? Mechanics { get; set; }
    public List<string>? Designers { get; set; }
    public List<string>? Publishers { get; set; }

    // Blob-based PDF seeding
    public string? PdfBlobKey { get; set; }
    public string? PdfSha256 { get; set; }
    public string? PdfVersion { get; set; }
}

internal sealed class SeedManifestAgent
{
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 2048;
}
