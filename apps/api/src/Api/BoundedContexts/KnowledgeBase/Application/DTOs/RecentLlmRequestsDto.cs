namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>Summary row for the recent-requests table.</summary>
public sealed record LlmRequestSummaryDto(
    Guid Id,
    DateTime RequestedAt,
    string ModelId,
    string Provider,
    string Source,
    Guid? UserId,
    string? UserRole,
    int PromptTokens,
    int CompletionTokens,
    int TotalTokens,
    decimal CostUsd,
    int LatencyMs,
    bool Success,
    string? ErrorMessage,
    bool IsStreaming,
    bool IsFreeModel
);

/// <summary>
/// Paginated list of recent LLM requests for the admin usage dashboard.
/// Issue #5083: Admin usage page — recent requests table.
/// </summary>
public sealed record RecentLlmRequestsDto(
    IReadOnlyList<LlmRequestSummaryDto> Items,
    int Total,
    int Page,
    int PageSize,
    int TotalPages
);
