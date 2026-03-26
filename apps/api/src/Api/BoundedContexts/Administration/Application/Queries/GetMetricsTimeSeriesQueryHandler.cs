using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.Administration.Domain.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for metrics time-series queries.
/// Issue #901: Executes 3 predefined PromQL range queries in parallel
/// and returns formatted time-series data for infrastructure charts.
/// </summary>
internal class GetMetricsTimeSeriesQueryHandler : IRequestHandler<GetMetricsTimeSeriesQuery, MetricsTimeSeriesResponse>
{
    private readonly IPrometheusQueryService _prometheusService;
    private readonly ILogger<GetMetricsTimeSeriesQueryHandler> _logger;
    private readonly TimeProvider _timeProvider;

    // Predefined PromQL queries (not user-configurable for security)
    private const string CpuQuery = "100 - (avg(rate(node_cpu_seconds_total{mode=\"idle\"}[5m])) * 100)";
    private const string MemoryQuery = "(node_memory_MemTotal_bytes - node_memory_MemAvailable_bytes) / 1024 / 1024";
    private const string RequestsQuery = "sum(rate(http_requests_total[5m]))";

    public GetMetricsTimeSeriesQueryHandler(
        IPrometheusQueryService prometheusService,
        ILogger<GetMetricsTimeSeriesQueryHandler> logger,
        TimeProvider timeProvider)
    {
        _prometheusService = prometheusService ?? throw new ArgumentNullException(nameof(prometheusService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<MetricsTimeSeriesResponse> Handle(
        GetMetricsTimeSeriesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var (duration, step) = GetRangeParameters(request.Range);
        var end = _timeProvider.GetUtcNow().UtcDateTime;
        var start = end - duration;

        _logger.LogInformation(
            "Fetching metrics time-series. Range: {Range}, Start: {Start}, End: {End}, Step: {Step}",
            request.Range, start, end, step);

        // Execute all 3 queries in parallel for performance
        var cpuTask = QuerySafeAsync(CpuQuery, start, end, step, "CPU", cancellationToken);
        var memoryTask = QuerySafeAsync(MemoryQuery, start, end, step, "Memory", cancellationToken);
        var requestsTask = QuerySafeAsync(RequestsQuery, start, end, step, "Requests", cancellationToken);

        await Task.WhenAll(cpuTask, memoryTask, requestsTask).ConfigureAwait(false);

        return new MetricsTimeSeriesResponse(
            await cpuTask.ConfigureAwait(false),
            await memoryTask.ConfigureAwait(false),
            await requestsTask.ConfigureAwait(false));
    }

    /// <summary>
    /// Executes a PromQL query with graceful degradation (returns empty on failure).
    /// </summary>
    private async Task<IReadOnlyCollection<MetricsTimeSeriesDataPoint>> QuerySafeAsync(
        string query,
        DateTime start,
        DateTime end,
        string step,
        string metricName,
        CancellationToken cancellationToken)
    {
        try
        {
            var result = await _prometheusService.QueryRangeAsync(
                query, start, end, step, cancellationToken).ConfigureAwait(false);

            // Flatten all time series into a single data point list
            // (aggregated queries typically return a single series)
            var dataPoints = result.TimeSeries
                .SelectMany(ts => ts.Values)
                .OrderBy(dp => dp.Timestamp)
                .Select(dp => new MetricsTimeSeriesDataPoint(dp.Timestamp, dp.Value))
                .ToList();

            _logger.LogDebug("{MetricName} query returned {Count} data points", metricName, dataPoints.Count);
            return dataPoints;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query {MetricName} metrics, returning empty series", metricName);
            return Array.Empty<MetricsTimeSeriesDataPoint>();
        }
    }

    /// <summary>
    /// Maps time range enum to (duration, step) parameters.
    /// </summary>
    private static (TimeSpan Duration, string Step) GetRangeParameters(MetricsTimeRange range) => range switch
    {
        MetricsTimeRange.OneHour => (TimeSpan.FromHours(1), "5m"),
        MetricsTimeRange.SixHours => (TimeSpan.FromHours(6), "15m"),
        MetricsTimeRange.TwentyFourHours => (TimeSpan.FromHours(24), "30m"),
        MetricsTimeRange.SevenDays => (TimeSpan.FromDays(7), "1h"),
        _ => (TimeSpan.FromHours(1), "5m")
    };
}
