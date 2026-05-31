namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Per-bucket AI metrics datapoint (#1729). Bucket boundaries are
/// inclusive-lower / exclusive-upper. Empty buckets surface with
/// <c>RequestCount = 0</c> and zeroed percentiles to keep the FE chart
/// series continuous (no gaps).
/// </summary>
internal record AiMetricsDatapoint
{
    public required DateTime Timestamp { get; init; }
    public required int RequestCount { get; init; }
    public required int AvgLatencyMs { get; init; }
    public required int P50LatencyMs { get; init; }
    public required int P95LatencyMs { get; init; }
    public required double ErrorRate { get; init; }
}

/// <summary>
/// Trend response: a continuous series of <see cref="AiMetricsDatapoint"/>
/// covering the requested range, with metadata about the bucket size
/// used (so the FE can label/space axes correctly).
/// </summary>
internal record AiMetricsTrendResult
{
    public required string Range { get; init; }
    public required string BucketSize { get; init; }
    public required IReadOnlyList<AiMetricsDatapoint> Datapoints { get; init; }
}
