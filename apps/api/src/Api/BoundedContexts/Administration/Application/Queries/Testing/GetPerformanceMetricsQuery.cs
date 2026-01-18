using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Enums;

namespace Api.BoundedContexts.Administration.Application.Queries.Testing;

/// <summary>
/// Query to retrieve performance testing metrics (Issue #2139)
/// Returns Core Web Vitals and performance budgets
/// </summary>
internal record GetPerformanceMetricsQuery : IQuery<PerformanceMetricsDto>;

/// <summary>
/// DTO for performance metrics (Issue #2139)
/// </summary>
internal record PerformanceMetricsDto(
    decimal Lcp,
    decimal Fid,
    decimal Cls,
    decimal Fcp,
    decimal Tti,
    decimal Tbt,
    decimal SpeedIndex,
    decimal PerformanceScore,
    PerformanceBudgetStatus BudgetStatus,
    DateTime LastRunAt,
    bool MeetsCoreWebVitals)
{
    /// <summary>
    /// Creates DTO from domain value object
    /// </summary>
    public static PerformanceMetricsDto FromValueObject(PerformanceMetrics metrics)
    {
        return new PerformanceMetricsDto(
            metrics.Lcp,
            metrics.Fid,
            metrics.Cls,
            metrics.Fcp,
            metrics.Tti,
            metrics.Tbt,
            metrics.SpeedIndex,
            metrics.PerformanceScore,
            metrics.BudgetStatus,
            metrics.LastRunAt,
            metrics.MeetsCoreWebVitals);
    }
}
