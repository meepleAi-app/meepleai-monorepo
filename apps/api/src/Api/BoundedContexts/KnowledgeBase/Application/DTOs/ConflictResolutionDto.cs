namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// DTO representing how a rule conflict was resolved.
/// Issue #3761: Arbitro Agent Conflict Resolution and Edge Cases.
/// </summary>
public record ConflictResolutionDto
{
    /// <summary>
    /// Type of conflict (Contradiction, Ambiguity, MissingRule, ExceptionClause)
    /// </summary>
    public required string ConflictType { get; init; }

    /// <summary>
    /// Pattern identifier (e.g., "prohibitive_vs_permissive")
    /// </summary>
    public required string Pattern { get; init; }

    /// <summary>
    /// IDs of rules involved in the conflict
    /// </summary>
    public required List<string> ConflictingRuleIds { get; init; }

    /// <summary>
    /// Description of the conflict
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// How the conflict was resolved (FAQ, LLM, HumanEscalation)
    /// </summary>
    public required string ResolutionStrategy { get; init; }

    /// <summary>
    /// What resolved it (FAQ key, "LLM", or "Escalated")
    /// </summary>
    public string? ResolvedBy { get; init; }
}
