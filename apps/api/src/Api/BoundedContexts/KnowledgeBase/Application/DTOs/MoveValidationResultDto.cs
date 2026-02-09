namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Response for Arbitro Agent move validation
/// Issue #3760: Arbitro Agent Move Validation Logic
/// </summary>
public record MoveValidationResultDto
{
    /// <summary>
    /// Validation decision: VALID, INVALID, or UNCERTAIN
    /// </summary>
    public required string Decision { get; init; }

    /// <summary>
    /// Confidence score (0.0-1.0)
    /// </summary>
    public required double Confidence { get; init; }

    /// <summary>
    /// Reasoning for the decision (max 200 chars)
    /// </summary>
    public required string Reasoning { get; init; }

    /// <summary>
    /// List of violated rule keys (empty if valid)
    /// </summary>
    public required List<string> ViolatedRules { get; init; }

    /// <summary>
    /// Suggestions for valid alternatives (null if valid)
    /// </summary>
    public List<string>? Suggestions { get; init; }

    /// <summary>
    /// Applicable rules that were considered
    /// </summary>
    public required List<ArbitroRuleAtomDto> ApplicableRules { get; init; }

    /// <summary>
    /// Token usage for this validation
    /// </summary>
    public required TokenUsageDto TokenUsage { get; init; }

    /// <summary>
    /// Cost breakdown in USD
    /// </summary>
    public required CostBreakdownDto CostBreakdown { get; init; }

    /// <summary>
    /// Total latency in milliseconds
    /// </summary>
    public required int LatencyMs { get; init; }

    /// <summary>
    /// Timestamp of validation (UTC)
    /// </summary>
    public required DateTime Timestamp { get; init; }
}

/// <summary>
/// DTO for RuleAtom used in Arbitro Agent validation responses.
/// Separate from GameManagement.RuleAtomDto to maintain bounded context independence.
/// </summary>
public record ArbitroRuleAtomDto(
    string Id,
    string Text,
    string? Section,
    string? Page,
    string? Line
);
