using Api.Models;

namespace Api.Services;

/// <summary>
/// ADMIN-02: Service for retrieving analytics dashboard statistics and metrics.
/// Provides aggregated data from database and OpenTelemetry metrics.
/// </summary>
internal interface IAdminStatsService
{
    /// <summary>
    /// Get comprehensive dashboard statistics including metrics and time-series trends.
    /// </summary>
    /// <param name="queryParams">Query parameters for filtering and date range</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Dashboard statistics with metrics and trends</returns>
    Task<DashboardStatsDto> GetDashboardStatsAsync(
        AnalyticsQueryParams queryParams,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Export dashboard data in CSV or JSON format.
    /// </summary>
    /// <param name="request">Export request with format and filters</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Exported data as string (CSV or JSON)</returns>
    Task<string> ExportDashboardDataAsync(
        ExportDataRequest request,
        CancellationToken cancellationToken = default);
}
