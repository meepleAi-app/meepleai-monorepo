namespace Api.BoundedContexts.Administration.Domain.Models;

/// <summary>
/// Issue #894: Prometheus metrics summary for infrastructure monitoring.
/// Contains key operational metrics from the last 24 hours.
/// </summary>
/// <param name="ApiRequestsLast24h">Total API requests in last 24 hours</param>
/// <param name="AvgLatencyMs">Average API latency in milliseconds (last 1 hour)</param>
/// <param name="ErrorRate">API error rate percentage (last 1 hour, 0-1 range)</param>
/// <param name="LlmCostLast24h">Total LLM cost in USD for last 24 hours</param>
public record PrometheusMetricsSummary(
    long ApiRequestsLast24h,
    double AvgLatencyMs,
    double ErrorRate,
    double LlmCostLast24h
);
