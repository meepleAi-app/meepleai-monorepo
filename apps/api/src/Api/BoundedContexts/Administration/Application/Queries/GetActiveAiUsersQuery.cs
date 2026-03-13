using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to get Monthly Active AI Users data for monitoring dashboard.
/// Issue #113: MAU Monitoring Dashboard.
/// </summary>
/// <param name="PeriodDays">Analysis period in days (7, 30, 90)</param>
internal record GetActiveAiUsersQuery(
    int PeriodDays = 30
) : IQuery<ActiveAiUsersResult>;

/// <summary>
/// Result containing MAU data with feature breakdown.
/// </summary>
internal record ActiveAiUsersResult(
    int TotalActiveUsers,
    int AiChatUsers,
    int PdfUploadUsers,
    int AgentUsers,
    int PeriodDays,
    DateTime PeriodStart,
    DateTime PeriodEnd,
    IReadOnlyList<DailyActiveUsersDto> DailyBreakdown
);

/// <summary>
/// Daily active user count for trend analysis.
/// </summary>
internal record DailyActiveUsersDto(
    DateTime Date,
    int ActiveUsers,
    int AiChatUsers,
    int PdfUploadUsers
);
