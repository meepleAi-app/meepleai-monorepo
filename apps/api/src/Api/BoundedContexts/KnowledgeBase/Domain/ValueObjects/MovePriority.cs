namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Enum representing move priority levels for strategic ranking.
/// Issue #3770: Used by Move Generator to classify move importance.
/// </summary>
public enum MovePriority
{
    /// <summary>
    /// Critical moves (winning captures: Queen takes Pawn).
    /// </summary>
    Critical = 1,

    /// <summary>
    /// High priority (checks, equal captures, threats on high-value pieces).
    /// </summary>
    High = 2,

    /// <summary>
    /// Medium priority (development, positional improvements).
    /// </summary>
    Medium = 3,

    /// <summary>
    /// Low priority (quiet moves, defensive positioning).
    /// </summary>
    Low = 4,

    /// <summary>
    /// Very low priority (retreats, passive moves).
    /// </summary>
    VeryLow = 5
}

/// <summary>
/// Extension methods for MovePriority classification.
/// </summary>
public static class MovePriorityExtensions
{
    /// <summary>
    /// Classifies a move based on its score components.
    /// </summary>
    public static MovePriority Classify(MoveScore score, bool isCapture, bool isCheck)
    {
        // Winning captures (material > 2)
        if (isCapture && score.Material >= 2.0)
            return MovePriority.Critical;

        // Checks or high-value threats
        if (isCheck || score.Tactical >= 0.4)
            return MovePriority.High;

        // Positive evaluation (development, position)
        if (score.Overall >= 0.3)
            return MovePriority.Medium;

        // Neutral or slightly negative
        if (score.Overall >= -0.2)
            return MovePriority.Low;

        // Clearly bad moves
        return MovePriority.VeryLow;
    }
}
