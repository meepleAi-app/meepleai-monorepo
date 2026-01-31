using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.KnowledgeBase.Domain.GridSearch;

/// <summary>
/// ADR-016 Phase 5: Result of a grid search evaluation run.
/// Contains aggregated metrics and comparison data across configurations.
/// </summary>
internal sealed record GridSearchResult
{
    /// <summary>
    /// Unique identifier for this grid search run.
    /// </summary>
    public required string GridSearchId { get; init; }

    /// <summary>
    /// Dataset used for evaluation.
    /// </summary>
    public required string DatasetName { get; init; }

    /// <summary>
    /// Timestamp when grid search started.
    /// </summary>
    public required DateTime StartedAt { get; init; }

    /// <summary>
    /// Timestamp when grid search completed.
    /// </summary>
    public required DateTime CompletedAt { get; init; }

    /// <summary>
    /// Individual configuration results.
    /// </summary>
    public required IReadOnlyList<ConfigurationResult> ConfigurationResults { get; init; }

    /// <summary>
    /// Best performing configuration based on Recall@10 (successful only).
    /// </summary>
    public ConfigurationResult? BestByRecallAt10 =>
        ConfigurationResults.Where(r => r.IsSuccess).MaxBy(r => r.Metrics.RecallAt10);

    /// <summary>
    /// Best performing configuration based on nDCG@10 (successful only).
    /// </summary>
    public ConfigurationResult? BestByNdcg =>
        ConfigurationResults.Where(r => r.IsSuccess).MaxBy(r => r.Metrics.NdcgAt10);

    /// <summary>
    /// Best performing configuration based on P95 latency (lowest, successful only).
    /// </summary>
    public ConfigurationResult? BestByLatency =>
        ConfigurationResults.Where(r => r.IsSuccess).MinBy(r => r.Metrics.P95LatencyMs);

    /// <summary>
    /// Total duration of grid search in milliseconds.
    /// </summary>
    public double TotalDurationMs => (CompletedAt - StartedAt).TotalMilliseconds;

    /// <summary>
    /// Number of configurations evaluated.
    /// </summary>
    public int ConfigurationCount => ConfigurationResults.Count;

    /// <summary>
    /// Number of successful evaluations.
    /// </summary>
    public int SuccessfulCount => ConfigurationResults.Count(r => r.IsSuccess);

    /// <summary>
    /// Whether any configuration meets the Phase 5 target (Recall@10 &gt;= 70%, P95 &lt; 1.5s).
    /// </summary>
    public bool MeetsPhase5Target =>
        ConfigurationResults.Any(r => r.Metrics.MeetsPhase5Target());

    /// <summary>
    /// Creates a grid search result from individual configuration evaluations.
    /// </summary>
    public static GridSearchResult Create(
        string datasetName,
        DateTime startedAt,
        DateTime completedAt,
        IReadOnlyList<ConfigurationResult> configurationResults)
    {
        return new GridSearchResult
        {
            GridSearchId = Guid.NewGuid().ToString(),
            DatasetName = datasetName,
            StartedAt = startedAt,
            CompletedAt = completedAt,
            ConfigurationResults = configurationResults
        };
    }
}

/// <summary>
/// Result for a single configuration evaluation.
/// </summary>
internal sealed record ConfigurationResult
{
    /// <summary>
    /// Configuration that was evaluated.
    /// </summary>
    public required GridSearchConfiguration Configuration { get; init; }

    /// <summary>
    /// Aggregated metrics for this configuration.
    /// </summary>
    public required EvaluationMetrics Metrics { get; init; }

    /// <summary>
    /// Number of samples evaluated.
    /// </summary>
    public required int SampleCount { get; init; }

    /// <summary>
    /// Evaluation duration in milliseconds.
    /// </summary>
    public required double DurationMs { get; init; }

    /// <summary>
    /// Error message if evaluation failed.
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Whether evaluation completed successfully.
    /// </summary>
    public bool IsSuccess => string.IsNullOrEmpty(ErrorMessage);

    /// <summary>
    /// Creates a successful configuration result.
    /// </summary>
    public static ConfigurationResult Success(
        GridSearchConfiguration configuration,
        EvaluationMetrics metrics,
        int sampleCount,
        double durationMs)
    {
        return new ConfigurationResult
        {
            Configuration = configuration,
            Metrics = metrics,
            SampleCount = sampleCount,
            DurationMs = durationMs,
            ErrorMessage = null
        };
    }

    /// <summary>
    /// Creates a failed configuration result.
    /// </summary>
    public static ConfigurationResult Failure(
        GridSearchConfiguration configuration,
        string errorMessage,
        double durationMs)
    {
        return new ConfigurationResult
        {
            Configuration = configuration,
            Metrics = EvaluationMetrics.Empty,
            SampleCount = 0,
            DurationMs = durationMs,
            ErrorMessage = errorMessage
        };
    }
}
