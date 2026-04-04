// OPS-02: RAG evaluation and grid search metrics (ADR-016 Phase 5)
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>
    /// Counter for total evaluation runs.
    /// </summary>
    public static readonly Counter<long> EvaluationRunsTotal = Meter.CreateCounter<long>(
        name: "meepleai.evaluation.runs.total",
        unit: "runs",
        description: "Total number of RAG evaluation runs");

    /// <summary>
    /// Histogram for Recall@5 scores from evaluation.
    /// </summary>
    public static readonly Histogram<double> EvaluationRecallAt5 = Meter.CreateHistogram<double>(
        name: "meepleai.evaluation.recall_at_5",
        unit: "score",
        description: "Recall@5 scores from RAG evaluation");

    /// <summary>
    /// Histogram for Recall@10 scores from evaluation.
    /// </summary>
    public static readonly Histogram<double> EvaluationRecallAt10 = Meter.CreateHistogram<double>(
        name: "meepleai.evaluation.recall_at_10",
        unit: "score",
        description: "Recall@10 scores from RAG evaluation");

    /// <summary>
    /// Histogram for nDCG@10 scores from evaluation.
    /// </summary>
    public static readonly Histogram<double> EvaluationNdcgAt10 = Meter.CreateHistogram<double>(
        name: "meepleai.evaluation.ndcg_at_10",
        unit: "score",
        description: "nDCG@10 scores from RAG evaluation");

    /// <summary>
    /// Histogram for MRR (Mean Reciprocal Rank) scores from evaluation.
    /// </summary>
    public static readonly Histogram<double> EvaluationMrr = Meter.CreateHistogram<double>(
        name: "meepleai.evaluation.mrr",
        unit: "score",
        description: "Mean Reciprocal Rank scores from RAG evaluation");

    /// <summary>
    /// Histogram for P95 latency from evaluation runs.
    /// </summary>
    public static readonly Histogram<double> EvaluationP95Latency = Meter.CreateHistogram<double>(
        name: "meepleai.evaluation.p95_latency",
        unit: "ms",
        description: "P95 latency from RAG evaluation runs");

    /// <summary>
    /// Counter for configurations meeting Phase 5 target.
    /// </summary>
    public static readonly Counter<long> EvaluationTargetMet = Meter.CreateCounter<long>(
        name: "meepleai.evaluation.target_met.total",
        unit: "configurations",
        description: "Configurations meeting Phase 5 targets (Recall@10 >= 70%, P95 < 1.5s)");

    /// <summary>
    /// Counter for evaluation sample results by outcome.
    /// </summary>
    public static readonly Counter<long> EvaluationSamplesTotal = Meter.CreateCounter<long>(
        name: "meepleai.evaluation.samples.total",
        unit: "samples",
        description: "Total evaluation samples by outcome (hit/miss)");

    /// <summary>
    /// Histogram for grid search total duration.
    /// </summary>
    public static readonly Histogram<double> GridSearchDuration = Meter.CreateHistogram<double>(
        name: "meepleai.gridsearch.duration",
        unit: "ms",
        description: "Total duration of grid search runs");

    /// <summary>
    /// Counter for grid search configurations evaluated.
    /// </summary>
    public static readonly Counter<long> GridSearchConfigsEvaluated = Meter.CreateCounter<long>(
        name: "meepleai.gridsearch.configs_evaluated.total",
        unit: "configs",
        description: "Total configurations evaluated in grid search");

    /// <summary>
    /// Records a RAG evaluation run with all metrics.
    /// ADR-016 Phase 5: Captures comprehensive evaluation metrics for observability.
    /// </summary>
    /// <param name="recallAt5">Recall@5 score (0.0 to 1.0)</param>
    /// <param name="recallAt10">Recall@10 score (0.0 to 1.0)</param>
    /// <param name="ndcgAt10">nDCG@10 score (0.0 to 1.0)</param>
    /// <param name="mrr">Mean Reciprocal Rank (0.0 to 1.0)</param>
    /// <param name="p95LatencyMs">P95 latency in milliseconds</param>
    /// <param name="configurationId">Optional configuration identifier</param>
    /// <param name="datasetName">Optional dataset name</param>
    /// <param name="meetsTarget">Whether configuration meets Phase 5 targets</param>
    public static void RecordEvaluationRun(
        double recallAt5,
        double recallAt10,
        double ndcgAt10,
        double mrr,
        double p95LatencyMs,
        string? configurationId = null,
        string? datasetName = null,
        bool meetsTarget = false)
    {
        var tags = new TagList();

        if (!string.IsNullOrEmpty(configurationId))
        {
            tags.Add("configuration", configurationId);
        }

        if (!string.IsNullOrEmpty(datasetName))
        {
            tags.Add("dataset", datasetName);
        }

        EvaluationRunsTotal.Add(1, tags);
        EvaluationRecallAt5.Record(recallAt5, tags);
        EvaluationRecallAt10.Record(recallAt10, tags);
        EvaluationNdcgAt10.Record(ndcgAt10, tags);
        EvaluationMrr.Record(mrr, tags);
        EvaluationP95Latency.Record(p95LatencyMs, tags);

        if (meetsTarget)
        {
            EvaluationTargetMet.Add(1, tags);
        }
    }

    /// <summary>
    /// Records grid search completion with summary metrics.
    /// ADR-016 Phase 5: Tracks grid search runs for performance monitoring.
    /// </summary>
    /// <param name="totalDurationMs">Total grid search duration in milliseconds</param>
    /// <param name="configsEvaluated">Number of configurations evaluated</param>
    /// <param name="successfulConfigs">Number of successful evaluations</param>
    /// <param name="configsMeetingTarget">Number of configurations meeting Phase 5 target</param>
    /// <param name="datasetName">Dataset used for evaluation</param>
    public static void RecordGridSearchCompletion(
        double totalDurationMs,
        int configsEvaluated,
        int successfulConfigs,
        int configsMeetingTarget,
        string? datasetName = null)
    {
        var tags = new TagList
        {
            { "successful_count", successfulConfigs },
            { "target_met_count", configsMeetingTarget }
        };

        if (!string.IsNullOrEmpty(datasetName))
        {
            tags.Add("dataset", datasetName);
        }

        GridSearchDuration.Record(totalDurationMs, tags);
        GridSearchConfigsEvaluated.Add(configsEvaluated, tags);
    }

    /// <summary>
    /// Records individual evaluation sample outcome.
    /// ADR-016 Phase 5: Granular tracking of sample-level results.
    /// </summary>
    /// <param name="isHit">Whether the sample was a hit (relevant document in top-K)</param>
    /// <param name="rank">Rank of first relevant document (0 if miss)</param>
    /// <param name="configurationId">Configuration identifier</param>
    public static void RecordEvaluationSample(
        bool isHit,
        int rank = 0,
        string? configurationId = null)
    {
        var tags = new TagList
        {
            { "outcome", isHit ? "hit" : "miss" }
        };

        if (!string.IsNullOrEmpty(configurationId))
        {
            tags.Add("configuration", configurationId);
        }

        if (isHit && rank > 0)
        {
            tags.Add("rank", rank);
        }

        EvaluationSamplesTotal.Add(1, tags);
    }
}
