namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Statistical metrics for a processing step duration.
/// Issue #4212: Historical performance statistics for ETA calculation.
/// </summary>
/// <param name="Step">Processing step name</param>
/// <param name="AverageDurationSeconds">Mean duration in seconds</param>
/// <param name="MedianDurationSeconds">Median duration in seconds (P50)</param>
/// <param name="P95DurationSeconds">95th percentile duration in seconds</param>
/// <param name="P99DurationSeconds">99th percentile duration in seconds</param>
/// <param name="SampleSize">Number of samples used for calculation</param>
public record StepDurationStats(
    string Step,
    double AverageDurationSeconds,
    double MedianDurationSeconds,
    double P95DurationSeconds,
    double P99DurationSeconds,
    int SampleSize
);
