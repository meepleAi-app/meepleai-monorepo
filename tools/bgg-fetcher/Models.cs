namespace BggFetcher;

public sealed class FetcherManifest
{
    public string Profile { get; set; } = string.Empty;
    public FetcherCatalog Catalog { get; set; } = new();
}

public sealed class FetcherCatalog
{
    public List<FetcherGameEntry> Games { get; set; } = new();
    public FetcherDefaultAgent? DefaultAgent { get; set; }
}

public sealed class FetcherDefaultAgent
{
    public string Name { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public double Temperature { get; set; } = 0.3;
    public int MaxTokens { get; set; } = 2048;
}

public sealed class FetcherGameEntry
{
    public string Title { get; set; } = string.Empty;
    public int? BggId { get; set; }
    public string Language { get; set; } = "en";
    public string? Pdf { get; set; }
    public bool SeedAgent { get; set; }
    public string? FallbackImageUrl { get; set; }
    public string? FallbackThumbnailUrl { get; set; }
    public bool BggEnhanced { get; set; }
    public string? Description { get; set; }
    public int? YearPublished { get; set; }
    public int? MinPlayers { get; set; }
    public int? MaxPlayers { get; set; }
    public int? PlayingTime { get; set; }
    public int? MinAge { get; set; }
    public double? AverageRating { get; set; }
    public double? AverageWeight { get; set; }
    public string? ImageUrl { get; set; }
    public string? ThumbnailUrl { get; set; }
    public string? RulesUrl { get; set; }
    public List<string>? Categories { get; set; }
    public List<string>? Mechanics { get; set; }
    public List<string>? Designers { get; set; }
    public List<string>? Publishers { get; set; }
}
