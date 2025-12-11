

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.Administration.Domain.Services;

/// <summary>
/// Domain service for querying Prometheus historical metrics.
/// Issue #893: Provides access to Prometheus HTTP API for range queries.
/// </summary>
public interface IPrometheusQueryService
{
    /// <summary>
    /// Executes a PromQL range query over a time period.
    /// </summary>
    /// <param name="query">PromQL query string</param>
    /// <param name="start">Start timestamp (RFC3339 or Unix timestamp)</param>
    /// <param name="end">End timestamp (RFC3339 or Unix timestamp)</param>
    /// <param name="step">Query resolution step (e.g., "5m", "1h")</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Prometheus query result</returns>
    Task<PrometheusQueryResult> QueryRangeAsync(
        string query,
        DateTime start,
        DateTime end,
        string step,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Executes a PromQL instant query at a single point in time.
    /// </summary>
    /// <param name="query">PromQL query string</param>
    /// <param name="time">Evaluation timestamp (optional, defaults to now)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Prometheus query result</returns>
    Task<PrometheusQueryResult> QueryInstantAsync(
        string query,
        DateTime? time = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Represents a Prometheus query result.
/// </summary>
public record PrometheusQueryResult(
    string ResultType,
    IReadOnlyCollection<PrometheusTimeSeries> TimeSeries);

/// <summary>
/// Represents a single Prometheus time series.
/// </summary>
public record PrometheusTimeSeries(
    IReadOnlyDictionary<string, string> Metric,
    IReadOnlyCollection<PrometheusDataPoint> Values);

/// <summary>
/// Represents a single data point in a Prometheus time series.
/// </summary>
public record PrometheusDataPoint(
    DateTime Timestamp,
    double Value);
