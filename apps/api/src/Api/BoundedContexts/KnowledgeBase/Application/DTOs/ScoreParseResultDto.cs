namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO representing the result of parsing and optionally recording a score from natural language.
/// </summary>
public sealed record ScoreParseResultDto
{
    /// <summary>
    /// Status of the parse operation: "parsed", "recorded", "ambiguous", or "unrecognized".
    /// </summary>
    public required string Status { get; init; }

    /// <summary>
    /// The resolved player name, if identified.
    /// </summary>
    public string? PlayerName { get; init; }

    /// <summary>
    /// The resolved player ID, if identified.
    /// </summary>
    public Guid? PlayerId { get; init; }

    /// <summary>
    /// The scoring dimension extracted (e.g., "points", "roads").
    /// </summary>
    public string? Dimension { get; init; }

    /// <summary>
    /// The score value extracted.
    /// </summary>
    public int? Value { get; init; }

    /// <summary>
    /// The round number for the score.
    /// </summary>
    public int? Round { get; init; }

    /// <summary>
    /// Confidence level of the parse (0.0 to 1.0).
    /// </summary>
    public float Confidence { get; init; }

    /// <summary>
    /// Whether the user should confirm before the score is recorded.
    /// </summary>
    public bool RequiresConfirmation { get; init; }

    /// <summary>
    /// Human-readable message describing the result.
    /// </summary>
    public string? Message { get; init; }

    /// <summary>
    /// When status is "ambiguous", lists the candidate player names.
    /// </summary>
    public IReadOnlyList<string> AmbiguousCandidates { get; init; } = [];
}
