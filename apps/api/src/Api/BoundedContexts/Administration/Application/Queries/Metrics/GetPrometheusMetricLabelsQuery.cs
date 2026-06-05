using MediatR;

namespace Api.BoundedContexts.Administration.Application.Queries.Metrics;

/// <summary>
/// Returns the list of metric names registered in Prometheus, used by the
/// MetricSelector dropdown in <c>/admin/monitor?tab=alerts</c>
/// (Issue #1840 SP5 F4-C7).
///
/// <para>Result is cached for 60s via HybridCache (key
/// <c>admin:alerts:metric-labels</c>) and falls back to a small hard-coded
/// list of well-known MeepleAI metric names if Prometheus is unreachable —
/// this keeps the rule-creation modal functional during incidents.</para>
/// </summary>
internal sealed record GetPrometheusMetricLabelsQuery : IRequest<MetricLabelsResult>;

/// <summary>
/// Response wrapper exposing both the metric list and a flag the FE uses to
/// render a "Prometheus offline" warning when the result is a fallback.
/// </summary>
internal sealed record MetricLabelsResult(IReadOnlyList<string> Labels, bool IsFallback);
