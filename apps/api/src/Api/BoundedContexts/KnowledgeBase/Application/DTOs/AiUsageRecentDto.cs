namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Paginated recent AI requests for the current user.
/// Issue #94: C3 Editor Self-Service AI Usage Page
/// </summary>
public record AiUsageRecentDto(
    IReadOnlyList<RecentAiRequestDto> Items,
    int Total,
    int Page,
    int PageSize,
    string Note
);

/// <summary>
/// A single recent AI request (security-filtered: no ErrorMessage, IpAddress, UserAgent, SessionId).
/// </summary>
public record RecentAiRequestDto(
    DateTime RequestedAt,
    string Model,
    string Provider,
    string Operation,
    int PromptTokens,
    int CompletionTokens,
    decimal CostUsd,
    int LatencyMs,
    bool Success
);
