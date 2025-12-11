using Api.BoundedContexts.Administration.Domain.Models;
using Api.BoundedContexts.Administration.Domain.Services;

namespace Api.BoundedContexts.Administration.Infrastructure.External;

/// <summary>
/// Issue #894: Infrastructure service orchestrating health checks and Prometheus metrics.
/// Executes parallel queries for optimal performance.
/// </summary>
public class InfrastructureDetailsService : IInfrastructureDetailsService
{
    private readonly IInfrastructureHealthService _healthService;
    private readonly IPrometheusQueryService _prometheusService;
    private readonly ILogger<InfrastructureDetailsService> _logger;

    public InfrastructureDetailsService(
        IInfrastructureHealthService healthService,
        IPrometheusQueryService prometheusService,
        ILogger<InfrastructureDetailsService> logger)
    {
        _healthService = healthService;
        _prometheusService = prometheusService;
        _logger = logger;
    }

    public async Task<InfrastructureDetails> GetDetailsAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Fetching comprehensive infrastructure details");

        try
        {
            var now = DateTime.UtcNow;
            var yesterday = now.AddDays(-1);
            var oneHourAgo = now.AddHours(-1);

            // Parallel execution: health + 4 Prometheus queries
            var healthTask = _healthService.GetOverallHealthAsync(cancellationToken);
            var servicesTask = _healthService.GetAllServicesHealthAsync(cancellationToken);
            var apiRequestsTask = QueryApiRequests24h(yesterday, now, cancellationToken);
            var avgLatencyTask = QueryAvgLatency(oneHourAgo, now, cancellationToken);
            var errorRateTask = QueryErrorRate(oneHourAgo, now, cancellationToken);
            var llmCostTask = QueryLlmCost24h(yesterday, now, cancellationToken);

            await Task.WhenAll(healthTask, servicesTask, apiRequestsTask, avgLatencyTask, errorRateTask, llmCostTask).ConfigureAwait(false);

            var overallHealth = await healthTask.ConfigureAwait(false);
            var services = await servicesTask.ConfigureAwait(false);
            var apiRequests24h = await apiRequestsTask.ConfigureAwait(false);
            var avgLatency = await avgLatencyTask.ConfigureAwait(false);
            var errorRate = await errorRateTask.ConfigureAwait(false);
            var llmCost24h = await llmCostTask.ConfigureAwait(false);

            var metricsSummary = new PrometheusMetricsSummary(
                apiRequests24h,
                avgLatency,
                errorRate,
                llmCost24h
            );

            var details = new InfrastructureDetails(overallHealth, services, metricsSummary);

            _logger.LogInformation(
                "Infrastructure details fetched successfully. Overall: {State}, Metrics: {Requests} requests, {Latency}ms avg latency",
                overallHealth.State,
                apiRequests24h,
                avgLatency);

            return details;
        }
#pragma warning disable S2139 // Exceptions should be either logged or rethrown but not both
        // INFRASTRUCTURE LOGGING PATTERN: Log exceptions at the infrastructure boundary for debugging.
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch infrastructure details");
            throw;
        }
#pragma warning restore S2139
    }

    private async Task<long> QueryApiRequests24h(DateTime start, DateTime end, CancellationToken ct)
    {
        try
        {
            var query = "sum(increase(http_requests_total[24h]))";
            var result = await _prometheusService.QueryRangeAsync(query, start, end, "1h", ct).ConfigureAwait(false);

            // Extract single value from time series (last data point)
            var lastValue = result.TimeSeries
                .SelectMany(ts => ts.Values)
                .OrderByDescending(v => v.Timestamp)
                .FirstOrDefault();

            return lastValue != null ? (long)lastValue.Value : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query API requests (24h), returning 0");
            return 0;
        }
    }

    private async Task<double> QueryAvgLatency(DateTime start, DateTime end, CancellationToken ct)
    {
        try
        {
            var query = "avg(rate(http_request_duration_seconds_sum[1h]) / rate(http_request_duration_seconds_count[1h]))";
            var result = await _prometheusService.QueryRangeAsync(query, start, end, "5m", ct).ConfigureAwait(false);

            var lastValue = result.TimeSeries
                .SelectMany(ts => ts.Values)
                .OrderByDescending(v => v.Timestamp)
                .FirstOrDefault();

            // Convert seconds to milliseconds
            return lastValue != null ? lastValue.Value * 1000 : 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query avg latency, returning 0");
            return 0;
        }
    }

    private async Task<double> QueryErrorRate(DateTime start, DateTime end, CancellationToken ct)
    {
        try
        {
            var query = "sum(rate(http_requests_total{status=~\"5..\"}[1h])) / sum(rate(http_requests_total[1h]))";
            var result = await _prometheusService.QueryRangeAsync(query, start, end, "5m", ct).ConfigureAwait(false);

            var lastValue = result.TimeSeries
                .SelectMany(ts => ts.Values)
                .OrderByDescending(v => v.Timestamp)
                .FirstOrDefault();

            return lastValue?.Value ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query error rate, returning 0");
            return 0;
        }
    }

    private async Task<double> QueryLlmCost24h(DateTime start, DateTime end, CancellationToken ct)
    {
        try
        {
            var query = "sum(increase(meepleai_llm_cost_usd[24h]))";
            var result = await _prometheusService.QueryRangeAsync(query, start, end, "1h", ct).ConfigureAwait(false);

            var lastValue = result.TimeSeries
                .SelectMany(ts => ts.Values)
                .OrderByDescending(v => v.Timestamp)
                .FirstOrDefault();

            return lastValue?.Value ?? 0;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to query LLM cost (24h), returning 0");
            return 0;
        }
    }
}
