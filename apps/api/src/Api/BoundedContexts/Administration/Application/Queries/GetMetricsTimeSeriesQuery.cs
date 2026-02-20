using MediatR;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Response records
namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Time range for metrics time-series queries.
/// Issue #901: Predefined ranges to avoid exposing raw PromQL.
/// </summary>
internal enum MetricsTimeRange
{
    OneHour,
    SixHours,
    TwentyFourHours,
    SevenDays
}

/// <summary>
/// Query to retrieve time-series data for infrastructure charts.
/// Issue #901: Replaces mock chart data with real Prometheus metrics.
/// </summary>
internal record GetMetricsTimeSeriesQuery : IRequest<MetricsTimeSeriesResponse>
{
    /// <summary>
    /// Time range for the query (determines step resolution).
    /// </summary>
    public required MetricsTimeRange Range { get; init; }
}

/// <summary>
/// Response containing CPU, memory, and request time-series data.
/// </summary>
internal record MetricsTimeSeriesResponse(
    IReadOnlyCollection<MetricsTimeSeriesDataPoint> Cpu,
    IReadOnlyCollection<MetricsTimeSeriesDataPoint> Memory,
    IReadOnlyCollection<MetricsTimeSeriesDataPoint> Requests);

/// <summary>
/// A single data point in a metrics time series.
/// </summary>
internal record MetricsTimeSeriesDataPoint(
    DateTime Timestamp,
    double Value);
