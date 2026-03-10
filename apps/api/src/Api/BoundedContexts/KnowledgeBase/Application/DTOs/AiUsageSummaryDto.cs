namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Multi-period AI usage summary (today, 7-day, 30-day).
/// Issue #94: C3 Editor Self-Service AI Usage Page
/// </summary>
public record AiUsageSummaryDto(
    AiUsagePeriodSummaryDto Today,
    AiUsagePeriodSummaryDto Last7Days,
    AiUsagePeriodSummaryDto Last30Days
);

/// <summary>
/// Usage summary for a single period.
/// </summary>
public record AiUsagePeriodSummaryDto(
    int RequestCount,
    long PromptTokens,
    long CompletionTokens,
    long TotalTokens,
    decimal CostUsd,
    int AverageLatencyMs
);
