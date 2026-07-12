namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Operational KPIs for the Mechanic Extractor admin metrics dashboard (#532 ME-M2.3): cost, review
/// velocity, approval rate, and the rejection-reasons breakdown. Computed over non-suppressed analyses.
/// </summary>
internal sealed record MechanicMetricsSummaryDto(
    decimal TotalCostUsd,
    int TotalAnalyses,
    int PublishedCount,
    int RejectedCount,
    int InReviewCount,
    decimal AverageCostUsd,
    double? AverageReviewTimeHours,
    double ApprovalRatePct,
    IReadOnlyList<RejectionReasonCountDto> RejectionBreakdown);

/// <summary>One rejection reason + how many analyses were rejected with it (#532).</summary>
internal sealed record RejectionReasonCountDto(string Reason, int Count);

/// <summary>Daily cost bucket for the time-series chart (#532). Gap-filled: days with no analyses report 0.</summary>
internal sealed record MechanicCostByDayDto(DateOnly Date, decimal CostUsd, int AnalysisCount);

/// <summary>One row of the recent-analyses table (#532).</summary>
internal sealed record MechanicRecentAnalysisRowDto(
    Guid Id,
    Guid SharedGameId,
    string GameName,
    int Status,
    Guid? ReviewedBy,
    string? ReviewerName,
    DateTime CreatedAt,
    DateTime? ReviewedAt,
    decimal EstimatedCostUsd);

/// <summary>Paginated recent-analyses result (#532).</summary>
internal sealed record MechanicRecentAnalysesResult(
    IReadOnlyList<MechanicRecentAnalysisRowDto> Items,
    int TotalCount);

/// <summary>CSV export payload for the recent-analyses table (#532).</summary>
internal sealed record ExportMechanicAnalysesResult(byte[] Content, string ContentType, string FileName);

/// <summary>One selectable option (game or reviewer) for the dashboard filter dropdowns (#2837).</summary>
internal sealed record MechanicFilterOptionDto(Guid Id, string Name);

/// <summary>
/// DISTINCT game + reviewer options for the metrics dashboard filter dropdowns (#2837), computed over
/// ALL non-suppressed analyses (no recency cap, unlike the earlier recent(200)-derived options).
/// </summary>
internal sealed record MechanicMetricsFilterOptionsDto(
    IReadOnlyList<MechanicFilterOptionDto> Games,
    IReadOnlyList<MechanicFilterOptionDto> Reviewers);
