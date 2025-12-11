using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Result record
namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Query to retrieve historical metrics from Prometheus.
/// Issue #893: Prometheus client integration for range queries.
/// </summary>
public record GetPrometheusMetricsQuery : IRequest<PrometheusMetricsResponse>
{
    /// <summary>
    /// PromQL query string (e.g., "sum(rate(http_requests_total[5m]))")
    /// </summary>
    public required string Query { get; init; }

    /// <summary>
    /// Start timestamp for range query
    /// </summary>
    public required DateTime Start { get; init; }

    /// <summary>
    /// End timestamp for range query
    /// </summary>
    public required DateTime End { get; init; }

    /// <summary>
    /// Query resolution step (e.g., "5m", "1h", "1d")
    /// </summary>
    public required string Step { get; init; }
}

/// <summary>
/// Response DTO for Prometheus metrics query.
/// </summary>
public record PrometheusMetricsResponse(
    string ResultType,
    IReadOnlyCollection<PrometheusTimeSeriesDto> TimeSeries);

/// <summary>
/// DTO for a single Prometheus time series.
/// </summary>
public record PrometheusTimeSeriesDto(
    IReadOnlyDictionary<string, string> Metric,
    IReadOnlyCollection<PrometheusDataPointDto> Values);

/// <summary>
/// DTO for a single data point.
/// </summary>
public record PrometheusDataPointDto(
    DateTime Timestamp,
    double Value);
