namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

// Issue #3304: RAG Dashboard DTOs for configuration and metrics.

#region Configuration DTOs

/// <summary>
/// Generation parameters for LLM.
/// </summary>
public sealed record GenerationParamsDto
{
    public required double Temperature { get; init; }
    public required int TopK { get; init; }
    public required double TopP { get; init; }
    public required int MaxTokens { get; init; }
}

/// <summary>
/// Retrieval parameters for document chunks.
/// </summary>
public sealed record RetrievalParamsDto
{
    public required int ChunkSize { get; init; }
    public required int ChunkOverlap { get; init; } // percentage 0-50
    public required int TopResults { get; init; }
    public required double SimilarityThreshold { get; init; }
}

/// <summary>
/// Reranker settings.
/// </summary>
public sealed record RerankerSettingsDto
{
    public required bool Enabled { get; init; }
    public required string Model { get; init; }
    public required int TopN { get; init; }
}

/// <summary>
/// Model selection settings.
/// </summary>
public sealed record ModelSelectionDto
{
    public required string PrimaryModel { get; init; }
    public string? FallbackModel { get; init; }
    public string? EvaluationModel { get; init; } // For CRAG evaluation
}

/// <summary>
/// Strategy-specific settings.
/// </summary>
public sealed record StrategySpecificSettingsDto
{
    public required double HybridAlpha { get; init; } // 0-1, keyword vs vector weight
    public required int ContextWindow { get; init; } // Number of previous messages to include
    public required int MaxHops { get; init; } // Maximum retrieval iterations
}

/// <summary>
/// Complete RAG configuration.
/// </summary>
public sealed record RagConfigDto
{
    public required GenerationParamsDto Generation { get; init; }
    public required RetrievalParamsDto Retrieval { get; init; }
    public required RerankerSettingsDto Reranker { get; init; }
    public required ModelSelectionDto Models { get; init; }
    public required StrategySpecificSettingsDto StrategySpecific { get; init; }
    public required string ActiveStrategy { get; init; }
}

#endregion

#region Metrics DTOs

/// <summary>
/// Performance metrics for a single strategy.
/// </summary>
public sealed record StrategyMetricsDto
{
    public required string StrategyId { get; init; }
    public required string StrategyName { get; init; }
    public required int TotalQueries { get; init; }
    public required double AverageLatencyMs { get; init; }
    public required double P95LatencyMs { get; init; }
    public required double P99LatencyMs { get; init; }
    public required double AverageRelevanceScore { get; init; }
    public required double AverageConfidenceScore { get; init; }
    public required int CacheHits { get; init; }
    public required int CacheMisses { get; init; }
    public required double CacheHitRate { get; init; }
    public required int TotalTokensUsed { get; init; }
    public required decimal TotalCost { get; init; }
    public required double AverageCostPerQuery { get; init; }
    public required int ErrorCount { get; init; }
    public required double ErrorRate { get; init; }
    public required DateTimeOffset LastUpdated { get; init; }
}

/// <summary>
/// Dashboard overview with all strategies.
/// </summary>
public sealed record RagDashboardOverviewDto
{
    public required IReadOnlyList<StrategyMetricsDto> Strategies { get; init; }
    public required StrategyMetricsDto AggregatedMetrics { get; init; }
    public required DateOnly StartDate { get; init; }
    public required DateOnly EndDate { get; init; }
    public required string BestPerformingStrategy { get; init; }
    public required string MostCostEffectiveStrategy { get; init; }
}

/// <summary>
/// Time series data point for metrics charts.
/// </summary>
public sealed record MetricsTimeSeriesPointDto
{
    public required DateTimeOffset Timestamp { get; init; }
    public required double Value { get; init; }
}

/// <summary>
/// Time series metrics for a strategy.
/// </summary>
public sealed record StrategyTimeSeriesMetricsDto
{
    public required string StrategyId { get; init; }
    public required IReadOnlyList<MetricsTimeSeriesPointDto> LatencyTrend { get; init; }
    public required IReadOnlyList<MetricsTimeSeriesPointDto> RelevanceTrend { get; init; }
    public required IReadOnlyList<MetricsTimeSeriesPointDto> QueryCountTrend { get; init; }
    public required IReadOnlyList<MetricsTimeSeriesPointDto> CostTrend { get; init; }
}

/// <summary>
/// Strategy comparison result.
/// </summary>
public sealed record StrategyComparisonDto
{
    public required IReadOnlyList<StrategyMetricsDto> Strategies { get; init; }
    public required IReadOnlyDictionary<string, double> LatencyRanking { get; init; }
    public required IReadOnlyDictionary<string, double> QualityRanking { get; init; }
    public required IReadOnlyDictionary<string, double> CostEfficiencyRanking { get; init; }
    public required string RecommendedStrategy { get; init; }
    public required string RecommendationReason { get; init; }
}

#endregion

#region Available Options DTOs

/// <summary>
/// Available LLM model.
/// </summary>
public sealed record AvailableLlmModelDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Provider { get; init; }
    public bool IsAvailable { get; init; } = true;
}

/// <summary>
/// Available reranker model.
/// </summary>
public sealed record AvailableRerankerModelDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
}

/// <summary>
/// Available retrieval strategy.
/// </summary>
public sealed record AvailableStrategyDto
{
    public required string Id { get; init; }
    public required string Name { get; init; }
    public required string Description { get; init; }
    public required string Category { get; init; }
    public required bool IsAvailable { get; init; }
}

/// <summary>
/// All available RAG dashboard options.
/// </summary>
public sealed record RagDashboardOptionsDto
{
    public required IReadOnlyList<AvailableLlmModelDto> LlmModels { get; init; }
    public required IReadOnlyList<AvailableRerankerModelDto> RerankerModels { get; init; }
    public required IReadOnlyList<AvailableStrategyDto> Strategies { get; init; }
}

#endregion
