namespace Api.BoundedContexts.DocumentProcessing.Application.DTOs;

/// <summary>
/// Aggregated processing metrics for admin dashboard.
/// Issue #4212: Historical metrics endpoint for performance monitoring.
/// </summary>
/// <param name="Averages">Average statistics per processing step</param>
/// <param name="Percentiles">Percentile statistics per step</param>
/// <param name="LastUpdated">Timestamp of last metric update</param>
internal record ProcessingMetricsDto(
    Dictionary<string, StepAverages> Averages,
    Dictionary<string, StepPercentiles> Percentiles,
    DateTime LastUpdated
);

/// <summary>
/// Average duration statistics for a processing step.
/// </summary>
/// <param name="Step">Step name</param>
/// <param name="AvgDuration">Average duration in seconds</param>
/// <param name="SampleSize">Number of samples</param>
internal record StepAverages(
    string Step,
    double AvgDuration,
    int SampleSize
);

/// <summary>
/// Percentile statistics for a processing step.
/// </summary>
/// <param name="P50">Median duration (seconds)</param>
/// <param name="P95">95th percentile duration (seconds)</param>
/// <param name="P99">99th percentile duration (seconds)</param>
internal record StepPercentiles(
    double P50,
    double P95,
    double P99
);
