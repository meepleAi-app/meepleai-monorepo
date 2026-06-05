namespace Api.BoundedContexts.Administration.Infrastructure.External;

/// <summary>
/// Thin HTTP client that exposes Prometheus' <c>/api/v1/label/__name__/values</c>
/// endpoint (the list of every metric name registered in the TSDB).
///
/// <para>Distinct from <see cref="PrometheusHttpClient"/> (which runs PromQL queries)
/// because the labels endpoint has a different return shape and uses different
/// cache semantics — see Issue #1840 SP5 F4-C7 MetricSelector dropdown.</para>
/// </summary>
internal interface IPrometheusLabelsClient
{
    /// <summary>
    /// Returns the alphabetic list of metric names currently known to Prometheus,
    /// or <c>null</c> if Prometheus is unreachable (caller falls back to a static list).
    /// </summary>
    Task<IReadOnlyList<string>?> GetMetricNamesAsync(CancellationToken cancellationToken);
}
