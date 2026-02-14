namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Represents the type of AI-generated user insight.
/// </summary>
public enum InsightType
{
    /// <summary>
    /// Alert about games in library not played for extended period (30+ days).
    /// </summary>
    BacklogAlert = 0,

    /// <summary>
    /// Reminder about saved rulebook PDFs not reviewed recently (7+ days).
    /// </summary>
    RulesReminder = 1,

    /// <summary>
    /// RAG-based recommendation for similar games based on user preferences.
    /// </summary>
    Recommendation = 2,

    /// <summary>
    /// Nudge to maintain or recover activity streak.
    /// </summary>
    StreakNudge = 3
}
