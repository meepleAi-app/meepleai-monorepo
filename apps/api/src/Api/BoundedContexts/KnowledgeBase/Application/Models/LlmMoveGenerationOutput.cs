namespace Api.BoundedContexts.KnowledgeBase.Application.Models;

/// <summary>
/// Structured output from LLM for move generation.
/// Used with ILlmService.GenerateJsonAsync for type-safe deserialization.
/// </summary>
internal sealed record LlmMoveGenerationOutput
{
    /// <summary>
    /// Primary recommended move with highest confidence.
    /// </summary>
    public required PrimarySuggestion PrimarySuggestion { get; init; }

    /// <summary>
    /// Alternative viable moves (2-3 options).
    /// </summary>
    public required List<AlternativeMove> Alternatives { get; init; }

    /// <summary>
    /// High-level strategic advice and context.
    /// </summary>
    public required string StrategicContext { get; init; }
}

/// <summary>
/// Primary move suggestion with detailed analysis.
/// </summary>
internal sealed record PrimarySuggestion
{
    /// <summary>
    /// Specific action description (e.g., "Collect 2 wood from forest").
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Reasoning why this is the optimal move.
    /// </summary>
    public required string Rationale { get; init; }

    /// <summary>
    /// Expected outcome after executing the move.
    /// </summary>
    public required string ExpectedOutcome { get; init; }

    /// <summary>
    /// LLM-generated confidence score (0.0-1.0).
    /// </summary>
    public required double Confidence { get; init; }
}

/// <summary>
/// Alternative move option.
/// </summary>
internal sealed record AlternativeMove
{
    /// <summary>
    /// Specific action description.
    /// </summary>
    public required string Action { get; init; }

    /// <summary>
    /// Reasoning for this alternative.
    /// </summary>
    public required string Rationale { get; init; }

    /// <summary>
    /// Expected outcome.
    /// </summary>
    public required string ExpectedOutcome { get; init; }

    /// <summary>
    /// LLM-generated confidence score (0.0-1.0), lower than primary.
    /// </summary>
    public required double Confidence { get; init; }
}
