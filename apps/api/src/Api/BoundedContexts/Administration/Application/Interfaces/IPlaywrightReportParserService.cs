using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Application.Interfaces;

/// <summary>
/// Service interface for parsing Playwright test reports (Issue #2139)
/// </summary>
public interface IPlaywrightReportParserService
{
    /// <summary>
    /// Parses latest Playwright test results and extracts E2E metrics
    /// </summary>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>E2EMetrics value object or null if reports not found</returns>
    Task<E2EMetrics?> ParseE2EMetricsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if Playwright test reports are available
    /// </summary>
    Task<bool> ReportsExistAsync(CancellationToken cancellationToken = default);
}
