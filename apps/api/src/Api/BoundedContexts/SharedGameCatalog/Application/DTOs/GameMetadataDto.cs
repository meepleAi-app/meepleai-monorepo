namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Game metadata extracted from PDF using AI parsing.
/// Contains structured game information with confidence scoring.
/// Issue #4155: Extract Game Metadata Query
/// </summary>
public record GameMetadataDto
{
    /// <summary>
    /// Game title extracted from PDF
    /// </summary>
    public string? Title { get; init; }

    /// <summary>
    /// Publication year (e.g., 2024)
    /// </summary>
    public int? Year { get; init; }

    /// <summary>
    /// Minimum number of players
    /// </summary>
    public int? MinPlayers { get; init; }

    /// <summary>
    /// Maximum number of players
    /// </summary>
    public int? MaxPlayers { get; init; }

    /// <summary>
    /// Average playing time in minutes
    /// </summary>
    public int? PlayingTime { get; init; }

    /// <summary>
    /// Minimum recommended age
    /// </summary>
    public int? MinAge { get; init; }

    /// <summary>
    /// Game description or overview
    /// </summary>
    public string? Description { get; init; }

    /// <summary>
    /// Confidence score (0.0 - 1.0) indicating extraction quality.
    /// ≥0.80: High quality, all key fields extracted
    /// 0.50-0.79: Medium quality, some fields missing
    /// &lt;0.50: Low quality, manual review recommended
    /// 0.0: Extraction failed completely
    /// </summary>
    public double ConfidenceScore { get; init; }

    /// <summary>
    /// Creates empty metadata DTO for extraction failures
    /// </summary>
    public static GameMetadataDto CreateEmpty() => new()
    {
        Title = null,
        Year = null,
        MinPlayers = null,
        MaxPlayers = null,
        PlayingTime = null,
        MinAge = null,
        Description = null,
        ConfidenceScore = 0.0
    };
}
