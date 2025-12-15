using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Testing;

/// <summary>
/// Query to retrieve E2E testing metrics (Issue #2139)
/// Returns Playwright test execution results
/// </summary>
internal record GetE2EMetricsQuery : IQuery<E2EMetricsDto>;

/// <summary>
/// DTO for E2E metrics (Issue #2139)
/// </summary>
internal record E2EMetricsDto(
    decimal Coverage,
    decimal PassRate,
    decimal FlakyRate,
    decimal ExecutionTime,
    int TotalTests,
    int PassedTests,
    int FailedTests,
    int SkippedTests,
    int FlakyTests,
    DateTime LastRunAt,
    string Status,
    bool MeetsQualityStandards)
{
    /// <summary>
    /// Creates DTO from domain value object
    /// </summary>
    public static E2EMetricsDto FromValueObject(E2EMetrics metrics)
    {
        return new E2EMetricsDto(
            metrics.Coverage,
            metrics.PassRate,
            metrics.FlakyRate,
            metrics.ExecutionTime,
            metrics.TotalTests,
            metrics.PassedTests,
            metrics.FailedTests,
            metrics.SkippedTests,
            metrics.FlakyTests,
            metrics.LastRunAt,
            metrics.Status,
            metrics.MeetsQualityStandards);
    }
}
