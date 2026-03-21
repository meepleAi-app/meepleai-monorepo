namespace Api.BoundedContexts.KnowledgeBase.Domain.Models;

public sealed record TimeSeriesPoint(
    DateTimeOffset Bucket,
    int QueryCount,
    double AverageLatencyMs,
    double AverageConfidence,
    decimal TotalCost);

public enum TimeSeriesGranularity { Hour, Day, Week }
