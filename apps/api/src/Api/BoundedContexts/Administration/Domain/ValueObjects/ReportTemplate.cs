namespace Api.BoundedContexts.Administration.Domain.ValueObjects;

/// <summary>
/// Value object representing report template types
/// ISSUE-916: 4 templates - SystemHealth, UserActivity, AIUsage, ContentMetrics
/// </summary>
public enum ReportTemplate
{
    /// <summary>System health metrics report</summary>
    SystemHealth = 1,

    /// <summary>User activity and engagement report</summary>
    UserActivity = 2,

    /// <summary>AI/LLM usage and cost report</summary>
    AIUsage = 3,

    /// <summary>Content and document metrics report</summary>
    ContentMetrics = 4
}
