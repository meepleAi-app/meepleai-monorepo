namespace Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

/// <summary>
/// Standard RAG evaluation metrics for retrieval quality assessment.
/// Implements Recall@K, nDCG@K, and MRR as defined in ADR-016.
/// </summary>
public sealed record EvaluationMetrics
{
    /// <summary>
    /// Recall at K - proportion of relevant documents retrieved in top K results.
    /// </summary>
    public double RecallAt5 { get; init; }

    /// <summary>
    /// Recall at K - proportion of relevant documents retrieved in top K results.
    /// </summary>
    public double RecallAt10 { get; init; }

    /// <summary>
    /// Normalized Discounted Cumulative Gain at K - measures ranking quality.
    /// </summary>
    public double NdcgAt10 { get; init; }

    /// <summary>
    /// Mean Reciprocal Rank - average of reciprocal ranks of first relevant result.
    /// </summary>
    public double Mrr { get; init; }

    /// <summary>
    /// P95 latency in milliseconds for end-to-end query processing.
    /// </summary>
    public double P95LatencyMs { get; init; }

    /// <summary>
    /// Answer correctness score from LLM-as-judge evaluation.
    /// Range: 0.0 to 1.0
    /// </summary>
    public double AnswerCorrectness { get; init; }

    /// <summary>
    /// Number of samples evaluated.
    /// </summary>
    public int SampleCount { get; init; }

    /// <summary>
    /// Empty metrics instance for fallback scenarios.
    /// </summary>
    public static EvaluationMetrics Empty => new()
    {
        RecallAt5 = 0.0,
        RecallAt10 = 0.0,
        NdcgAt10 = 0.0,
        Mrr = 0.0,
        P95LatencyMs = 0.0,
        AnswerCorrectness = 0.0,
        SampleCount = 0
    };

    /// <summary>
    /// Creates metrics from computed values.
    /// </summary>
    public static EvaluationMetrics Create(
        double recallAt5,
        double recallAt10,
        double ndcgAt10,
        double mrr,
        double p95LatencyMs,
        double answerCorrectness,
        int sampleCount)
    {
        return new EvaluationMetrics
        {
            RecallAt5 = Math.Clamp(recallAt5, 0.0, 1.0),
            RecallAt10 = Math.Clamp(recallAt10, 0.0, 1.0),
            NdcgAt10 = Math.Clamp(ndcgAt10, 0.0, 1.0),
            Mrr = Math.Clamp(mrr, 0.0, 1.0),
            P95LatencyMs = Math.Max(0.0, p95LatencyMs),
            AnswerCorrectness = Math.Clamp(answerCorrectness, 0.0, 1.0),
            SampleCount = Math.Max(0, sampleCount)
        };
    }

    /// <summary>
    /// Checks if metrics meet the Phase 0 baseline requirements.
    /// </summary>
    public bool MeetsBaselineRequirements() =>
        RecallAt10 >= 0.0 && SampleCount >= 30;

    /// <summary>
    /// Checks if metrics meet Phase 4 target (Recall@10 >= 60%).
    /// </summary>
    public bool MeetsPhase4Target() => RecallAt10 >= 0.60;

    /// <summary>
    /// Checks if metrics meet Phase 5 target (Recall@10 >= 70%, P95 < 1.5s).
    /// </summary>
    public bool MeetsPhase5Target() =>
        RecallAt10 >= 0.70 && P95LatencyMs < 1500.0;
}
