namespace Api.BoundedContexts.GameToolkit.Domain.Enums;

/// <summary>
/// v2 (B19-3b, 2026-05-31): how a single scoring category is computed at game end.
/// Used by AiScoringCategorySuggestion to drive polymorphic UI rendering.
/// </summary>
public enum ScoringComputation
{
    /// <summary>
    /// Sum of a count of items (e.g., "Eggs: 1 point each", "Tucked cards: 1 each").
    /// </summary>
    Count = 0,

    /// <summary>
    /// Sum of variable values per item (e.g., "Birds: variable points printed on each card").
    /// </summary>
    Sum = 1,

    /// <summary>
    /// Position-dependent points based on rank (e.g., Wingspan end-of-round goals: 5/2/1 per round).
    /// </summary>
    RankBased = 2,

    /// <summary>
    /// Custom computation rule too game-specific to encode generically; UI falls back to manual entry.
    /// </summary>
    Custom = 99
}
