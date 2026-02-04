namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Response DTO for AI-powered dashboard insights (Issue #3319).
/// Contains personalized insights based on user's library, play history, and RAG recommendations.
/// </summary>
public record DashboardInsightsResponseDto(
    IReadOnlyList<DashboardInsightDto> Insights,
    DateTime GeneratedAt,
    DateTime NextRefresh
);

/// <summary>
/// Individual insight item (Issue #3319).
/// Maps to AiInsight type in frontend.
/// </summary>
public record DashboardInsightDto(
    string Id,
    InsightType Type,
    string Icon,
    string Title,
    string Description,
    string ActionUrl,
    string ActionLabel,
    int Priority,
    Dictionary<string, object>? Metadata = null
);

/// <summary>
/// Insight types (Issue #3319).
/// </summary>
public enum InsightType
{
    /// <summary>Games not played for 30+ days</summary>
    Backlog,

    /// <summary>Recently saved chat rules</summary>
    RulesReminder,

    /// <summary>RAG-powered game recommendations</summary>
    Recommendation,

    /// <summary>Streak nudge for engagement</summary>
    Streak,

    /// <summary>Achievement progress highlight</summary>
    Achievement
}
