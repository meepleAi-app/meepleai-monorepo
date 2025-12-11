using Api.Models;

namespace Api.Services;

/// <summary>
/// Service for managing system alerts from Prometheus AlertManager.
/// OPS-07: Alerting system for critical errors and anomalies.
/// </summary>
public interface IAlertingService
{
    /// <summary>
    /// Send an alert through configured channels (Email, Slack, PagerDuty).
    /// Implements throttling (1 alert per hour max for same alert type).
    /// </summary>
    Task<AlertDto> SendAlertAsync(
        string alertType,
        string severity,
        string message,
        IDictionary<string, object>? metadata = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Resolve an active alert by alert type.
    /// Marks the alert as inactive and sets resolved timestamp.
    /// </summary>
    Task<bool> ResolveAlertAsync(
        string alertType,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get all currently active alerts.
    /// </summary>
    Task<List<AlertDto>> GetActiveAlertsAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Get alert history for a specific date range.
    /// </summary>
    Task<List<AlertDto>> GetAlertHistoryAsync(
        DateTime fromDate,
        DateTime toDate,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Check if an alert type is currently throttled (sent within the last hour).
    /// </summary>
    Task<bool> IsThrottledAsync(
        string alertType,
        CancellationToken cancellationToken = default);
}
