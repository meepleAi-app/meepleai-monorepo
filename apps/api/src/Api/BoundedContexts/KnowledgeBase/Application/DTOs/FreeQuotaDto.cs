namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>Daily usage for a single free model.</summary>
public sealed record FreeModelUsageDto(
    string ModelId,
    int RequestsToday,
    int DailyLimit,
    double PercentUsed,
    bool IsExhausted,
    DateTime? NextResetUtc
);

/// <summary>
/// Free tier quota snapshot for the admin usage dashboard.
/// Issue #5082: Admin usage page — free quota indicator.
/// </summary>
public sealed record FreeQuotaDto(
    IReadOnlyList<FreeModelUsageDto> Models,
    int TotalFreeRequestsToday,
    DateTime GeneratedAt
);
