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

            if (game.BggId <= 0)
                errors.Add($"Game '{game.Title}' has invalid bggId={game.BggId}");

            if (!seenBggIds.Add(game.BggId) && game.BggId > 0)
                errors.Add($"Duplicate bggId={game.BggId} ('{game.Title}')");

            if (string.IsNullOrWhiteSpace(game.Language))
                game.Language = "en";
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
    public int BggId { get; set; }
    public string Language { get; set; } = "en";
    public string? Pdf { get; set; }
    public bool SeedAgent { get; set; }
    public string? FallbackImageUrl { get; set; }
    public string? FallbackThumbnailUrl { get; set; }
}

internal sealed class SeedManifestAgent
{
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 2048;
}
