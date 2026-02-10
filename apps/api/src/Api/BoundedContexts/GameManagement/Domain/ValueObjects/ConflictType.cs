namespace Api.BoundedContexts.GameManagement.Domain.ValueObjects;

/// <summary>
/// Types of rule conflicts that can occur during move validation.
/// Issue #3761: Arbitro Agent Conflict Resolution and Edge Cases.
/// </summary>
public enum ConflictType
{
    /// <summary>
    /// Multiple rules directly contradict each other (e.g., "can move" vs "cannot move")
    /// </summary>
    Contradiction = 1,

    /// <summary>
    /// Rules contain ambiguous or unclear language requiring interpretation
    /// </summary>
    Ambiguity = 2,

    /// <summary>
    /// No specific rule found for the move, unclear if allowed or forbidden
    /// </summary>
    MissingRule = 3,

    /// <summary>
    /// Rules contain exception clauses that create conditional logic
    /// </summary>
    ExceptionClause = 4
}
