using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.Administration.Application.Queries.Testing;

/// <summary>
/// Query to retrieve accessibility testing metrics (Issue #2139)
/// Returns Lighthouse and axe-core accessibility scores
/// </summary>
public record GetAccessibilityMetricsQuery : IQuery<AccessibilityMetricsDto>;

/// <summary>
/// DTO for accessibility metrics (Issue #2139)
/// </summary>
public record AccessibilityMetricsDto(
    decimal LighthouseScore,
    int AxeViolations,
    IReadOnlyList<string> WcagLevels,
    DateTime LastRunAt,
    string Status,
    bool MeetsQualityStandards)
{
    /// <summary>
    /// Creates DTO from domain value object
    /// </summary>
    public static AccessibilityMetricsDto FromValueObject(AccessibilityMetrics metrics)
    {
        return new AccessibilityMetricsDto(
            metrics.LighthouseScore,
            metrics.AxeViolations,
            metrics.WcagLevels,
            metrics.LastRunAt,
            metrics.Status,
            metrics.MeetsQualityStandards);
    }
}
