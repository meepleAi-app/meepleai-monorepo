namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// Aggregated snapshot of OpenRouter account status and rate-limit utilization.
/// Issue #5077: Admin usage page — KPI cards data source.
/// Properties: BalanceUsd (USD balance), DailySpendUsd (UTC-day spend),
/// TodayRequestCount (LLM calls today), CurrentRpm / LimitRpm (sliding-window RPM),
/// UtilizationPercent ([0,1] fraction), IsThrottled, IsFreeTier,
/// RateLimitInterval (e.g. "minute"), LastUpdated (last poll time; null if never).
/// </summary>
public sealed record OpenRouterStatusDto(
    decimal BalanceUsd,
    decimal DailySpendUsd,
    int TodayRequestCount,
    int CurrentRpm,
    int LimitRpm,
    double UtilizationPercent,
    bool IsThrottled,
    bool IsFreeTier,
    string RateLimitInterval,
    DateTime? LastUpdated
);
