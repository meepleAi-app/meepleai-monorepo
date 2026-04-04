namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Overview analytics for admin dashboard: high-level entity counts.
/// </summary>
internal record OverviewAnalyticsDto(
    int TotalUsers,
    int TotalGames,
    int TotalDocuments,
    int TotalChats,
    int TodayNewUsers,
    int TodayNewChats);

/// <summary>
/// Chat analytics: thread/message counts and 7-day message histogram.
/// </summary>
internal record ChatAnalyticsDto(
    int TotalThreads,
    int TotalMessages,
    double AvgMessagesPerThread,
    IReadOnlyList<DailyCountDto> MessagesLast7Days);

/// <summary>
/// Single day count bucket used in time-series responses.
/// </summary>
internal record DailyCountDto(DateOnly Date, int Count);

/// <summary>
/// PDF processing analytics: totals, success rate, average timing.
/// </summary>
internal record PdfAnalyticsDto(
    int TotalDocuments,
    int TotalProcessed,
    double SuccessRate,
    double AvgProcessingTimeMs);

/// <summary>
/// Per-model performance metrics aggregated from AgentTestResult.
/// </summary>
internal record ModelPerformanceDto(
    IReadOnlyList<ModelPerformanceItemDto> Models);

/// <summary>
/// Single model performance row.
/// </summary>
internal record ModelPerformanceItemDto(
    string ModelName,
    int Invocations,
    double AvgLatencyMs,
    decimal TotalCost,
    double AvgConfidence);

/// <summary>
/// Monthly/daily active user counts and retention estimate.
/// </summary>
internal record MauDto(
    int MonthlyActiveUsers,
    int DailyActiveUsers,
    double RetentionRate);
