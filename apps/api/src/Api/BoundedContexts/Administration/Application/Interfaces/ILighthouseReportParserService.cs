using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Application.Interfaces;

/// <summary>
/// Service interface for parsing Lighthouse CI reports (Issue #2139)
/// </summary>
public interface ILighthouseReportParserService
{
    /// <summary>
    /// Parses latest Lighthouse report and extracts accessibility metrics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>AccessibilityMetrics value object or null if reports not found</returns>
    Task<AccessibilityMetrics?> ParseAccessibilityMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Parses latest Lighthouse report and extracts performance metrics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>PerformanceMetrics value object or null if reports not found</returns>
    Task<PerformanceMetrics?> ParsePerformanceMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if Lighthouse reports are available
    /// </summary>
    Task<bool> ReportsExistAsync(CancellationToken cancellationToken = default);
}
