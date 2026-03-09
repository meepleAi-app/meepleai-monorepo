using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Queries.Queue;

/// <summary>
/// Query to retrieve dashboard metrics with period filtering.
/// Issue #5459: Per-phase timing + metrics dashboard.
/// </summary>
internal sealed record GetDashboardMetricsQuery(string Period = "24h") : IQuery<DashboardMetricsDto>;

internal sealed record DashboardMetricsDto(
    IReadOnlyList<PhaseTimingDto> PhaseTimings,
    int TotalProcessed,
    int TotalFailed,
    double FailureRatePercent,
    double AvgTotalDurationSeconds,
    string Period);

internal sealed record PhaseTimingDto(
    string Phase,
    double AvgDurationSeconds,
    double MinDurationSeconds,
    double MaxDurationSeconds,
    int SampleCount);
