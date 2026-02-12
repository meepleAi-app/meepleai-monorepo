namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Enriched game metadata combining PDF extraction with BGG data.
/// Contains merged fields, conflict detection, and enrichment metadata.
/// Issue #4156: BGG Match and Enrichment Command
/// </summary>
public record EnrichedGameDto
{
    /// <summary>
    /// Final merged game title (BGG preferred if both exist)
    /// </summary>
    public required string Title { get; init; }

    /// <summary>
    /// Final merged publication year
    /// </summary>
    public int? Year { get; init; }

    /// <summary>
    /// Final merged minimum players
    /// </summary>
    public int? MinPlayers { get; init; }

    /// <summary>
    /// Final merged maximum players
    /// </summary>
    public int? MaxPlayers { get; init; }

    /// <summary>
    /// Final merged playing time in minutes
    /// </summary>
    public int? PlayingTime { get; init; }

    /// <summary>
    /// Final merged minimum age
    /// </summary>
    public int? MinAge { get; init; }

    /// <summary>
    /// Final merged game description (BGG preferred if both exist)
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// BGG complexity rating (0.0 - 5.0)
    /// </summary>
    public decimal? ComplexityRating { get; init; }

    /// <summary>
    /// BGG game image URL
    /// </summary>
    public string? ImageUrl { get; init; }

    /// <summary>
    /// BGG game thumbnail URL
    /// </summary>
    public string? ThumbnailUrl { get; init; }

    /// <summary>
    /// BGG average rating (1.0 - 10.0)
    /// </summary>
    public decimal? AverageRating { get; init; }

    /// <summary>
    /// BGG rank position
    /// </summary>
    public int? RankPosition { get; init; }

    /// <summary>
    /// BoardGameGeek ID used for enrichment
    /// </summary>
    public int BggId { get; init; }

    /// <summary>
    /// Confidence score from original PDF extraction (0.0 - 1.0)
    /// </summary>
    public double PdfConfidenceScore { get; init; }

    /// <summary>
    /// List of fields where PDF and BGG data conflicted.
    /// Empty if no conflicts or if only one source had data.
    /// </summary>
    public List<MetadataConflict> Conflicts { get; init; } = [];

    /// <summary>
    /// Indicates whether BGG enrichment succeeded
    /// </summary>
    public bool BggEnrichmentSucceeded { get; init; }

    /// <summary>
    /// Warning message if BGG enrichment failed but PDF data was preserved
    /// </summary>
    public string? EnrichmentWarning { get; init; }
}

/// <summary>
/// Represents a conflict between PDF-extracted and BGG data for a specific field.
/// </summary>
public record MetadataConflict
{
    /// <summary>
    /// Name of the conflicting field (e.g., "MinPlayers", "Year")
    /// </summary>
    public required string FieldName { get; init; }

    /// <summary>
    /// Value from BGG (as string for display)
    /// </summary>
    public required string BggValue { get; init; }

    /// <summary>
    /// Value from PDF extraction (as string for display)
    /// </summary>
    public required string PdfValue { get; init; }
}
