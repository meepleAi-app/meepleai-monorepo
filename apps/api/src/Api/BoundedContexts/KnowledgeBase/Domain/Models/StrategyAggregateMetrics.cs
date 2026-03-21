namespace Api.BoundedContexts.KnowledgeBase.Domain.Models;

public sealed record StrategyAggregateMetrics(
    string Strategy,
    int TotalQueries,
    double AverageLatencyMs,
    double P95LatencyMs,
    double P99LatencyMs,
    double AverageConfidence,
    int CacheHits,
    int CacheMisses,
    int TotalTokensUsed,
    decimal TotalCost,
    decimal AverageCostPerQuery,
    int ErrorCount,
    double ErrorRate,
    int CragCorrect,
    int CragIncorrect,
    int CragAmbiguous,
    DateTimeOffset LastUpdated);
