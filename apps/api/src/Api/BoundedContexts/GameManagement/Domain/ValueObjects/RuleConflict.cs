using Api.SharedKernel.Domain.ValueObjects;

namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Value object representing a detected conflict between game rules.
/// Issue #3761: Arbitro Agent Conflict Resolution and Edge Cases.
/// </summary>
internal sealed class RuleConflict : ValueObject
{
    /// <summary>
    /// Type of conflict detected
    /// </summary>
    public ConflictType Type { get; }

    /// <summary>
    /// Pattern identifier for FAQ lookup (e.g., "setup_vs_turn_order")
    /// </summary>
    public string Pattern { get; }

    /// <summary>
    /// IDs of rules involved in the conflict
    /// </summary>
    public IReadOnlyList<string> ConflictingRuleIds { get; }

    /// <summary>
    /// Human-readable description of the conflict
    /// </summary>
    public string Description { get; }

    public RuleConflict(
        ConflictType type,
        string pattern,
        IReadOnlyList<string> conflictingRuleIds,
        string description)
    {
        if (string.IsNullOrWhiteSpace(pattern))
            throw new ArgumentException("Pattern cannot be empty", nameof(pattern));

        if (conflictingRuleIds == null || conflictingRuleIds.Count == 0)
            throw new ArgumentException("At least one conflicting rule required", nameof(conflictingRuleIds));

        if (string.IsNullOrWhiteSpace(description))
            throw new ArgumentException("Description cannot be empty", nameof(description));

        Type = type;
        Pattern = pattern.Trim().ToLowerInvariant();
        ConflictingRuleIds = conflictingRuleIds;
        Description = description.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Type;
        yield return Pattern;
        foreach (var ruleId in ConflictingRuleIds)
        {
            yield return ruleId;
        }
    }

    public override string ToString()
    {
        return $"{Type}: {Description} (Rules: {string.Join(", ", ConflictingRuleIds)})";
    }
}
