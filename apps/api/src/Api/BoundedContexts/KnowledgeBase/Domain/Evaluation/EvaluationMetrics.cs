namespace Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

/// <summary>
/// Standard RAG evaluation metrics for retrieval quality assessment.
/// Implements Recall@K, nDCG@K, and MRR as defined in ADR-016.
/// </summary>
internal sealed record EvaluationMetrics
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
    /// Answer correctness score from deterministic keyword / word-overlap matching (NOT an LLM-as-judge;
    /// kept keyword-based for CI stability). Range: 0.0 to 1.0.
    /// </summary>
    public double AnswerCorrectness { get; init; }

    /// <summary>
    /// Number of samples evaluated.
    /// </summary>
    public int SampleCount { get; init; }

    /// <summary>
    /// Number of samples with at least one relevant chunk id (contribute to recall@k/nDCG@10/MRR).
    /// </summary>
    public int LabeledSampleCount { get; init; }

    /// <summary>
    /// Number of samples with no relevant chunk ids (excluded from recall@k/nDCG@10/MRR).
    /// </summary>
    public int UnlabeledSampleCount { get; init; }

    /// <summary>
    /// Citation accuracy (0..1): among citation-graded samples (those with <c>ExpectedCitations</c>),
    /// the fraction whose actual cited pages satisfy the expected pages per the sample's match policy.
    /// Page-level, so stable across corpus re-index (#3427).
    /// </summary>
    public double CitationAccuracy { get; init; }

    /// <summary>
    /// Structural validity (0..1): mean fraction of well-formed citations (PDF:guid + existing document +
    /// page in range) across successful samples. A trust floor separate from page-accuracy.
    /// </summary>
    public double CitationStructuralValidity { get; init; }

    /// <summary>
    /// Number of citation-graded samples (with <c>ExpectedCitations</c>) contributing to <see cref="CitationAccuracy"/>.
    /// </summary>
    public int CitedSampleCount { get; init; }

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
        SampleCount = 0,
        LabeledSampleCount = 0,
        UnlabeledSampleCount = 0,
        CitationAccuracy = 0.0,
        CitationStructuralValidity = 0.0,
        CitedSampleCount = 0
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
        int sampleCount,
        int labeledSampleCount = 0,
        int unlabeledSampleCount = 0,
        double citationAccuracy = 0.0,
        double citationStructuralValidity = 0.0,
        int citedSampleCount = 0)
    {
        return new EvaluationMetrics
        {
            RecallAt5 = Math.Clamp(recallAt5, 0.0, 1.0),
            RecallAt10 = Math.Clamp(recallAt10, 0.0, 1.0),
            NdcgAt10 = Math.Clamp(ndcgAt10, 0.0, 1.0),
            Mrr = Math.Clamp(mrr, 0.0, 1.0),
            P95LatencyMs = Math.Max(0.0, p95LatencyMs),
            AnswerCorrectness = Math.Clamp(answerCorrectness, 0.0, 1.0),
            SampleCount = Math.Max(0, sampleCount),
            LabeledSampleCount = Math.Max(0, labeledSampleCount),
            UnlabeledSampleCount = Math.Max(0, unlabeledSampleCount),
            CitationAccuracy = Math.Clamp(citationAccuracy, 0.0, 1.0),
            CitationStructuralValidity = Math.Clamp(citationStructuralValidity, 0.0, 1.0),
            CitedSampleCount = Math.Max(0, citedSampleCount)
        };
    }

    /// <summary>
    /// Computes aggregated metrics from sample results. Only successful samples (<see cref="EvaluationSampleResult.IsSuccess"/>)
    /// contribute to the aggregates. Retrieval metrics (recall@5/@10, nDCG@10, MRR) are computed only over "labeled" samples
    /// (<see cref="EvaluationSampleResult.RelevantChunkIds"/> non-empty) so unlabeled samples cannot silently pollute them;
    /// answer correctness and P95 latency are still computed over all successful samples.
    /// </summary>
    public static EvaluationMetrics Compute(IReadOnlyList<EvaluationSampleResult> sampleResults)
    {
        ArgumentNullException.ThrowIfNull(sampleResults);

        var successful = sampleResults.Where(r => r.IsSuccess).ToList();

        if (successful.Count == 0)
        {
            return Empty;
        }

        var labeled = successful.Where(r => r.RelevantChunkIds.Count > 0).ToList();

        var recallAt5 = labeled.Count > 0 ? labeled.Average(r => r.HitAt5 ? 1.0 : 0.0) : 0.0;
        var recallAt10 = labeled.Count > 0 ? labeled.Average(r => r.HitAt10 ? 1.0 : 0.0) : 0.0;
        var ndcgAt10 = labeled.Count > 0 ? labeled.Average(r => r.NdcgAt10) : 0.0;
        var mrr = labeled.Count > 0 ? labeled.Average(r => r.ReciprocalRank) : 0.0;
        var answerCorrectness = successful.Average(r => r.AnswerCorrectness);

        // Citation accuracy is computed only over "citation-graded" samples (those with ExpectedCitations,
        // i.e. CitationMatched != null) so ungraded samples cannot silently pollute it — mirroring how
        // retrieval metrics exclude unlabeled samples. Structural validity is a trust floor over ALL
        // successful samples (a sample that cited nothing is vacuously valid, CitationStructuralValidity = 1.0).
        var citationGraded = successful.Where(r => r.CitationMatched.HasValue).ToList();
        var citationAccuracy = citationGraded.Count > 0
            ? citationGraded.Average(r => r.CitationMatched!.Value ? 1.0 : 0.0)
            : 0.0;
        var citationStructuralValidity = successful.Average(r => r.CitationStructuralValidity);

        // Calculate P95 latency over all successful samples
        var latencies = successful.Select(r => r.LatencyMs).OrderBy(l => l).ToList();
        var p95Index = (int)Math.Ceiling(latencies.Count * 0.95) - 1;
        var p95Latency = latencies.Count > 0 ? latencies[Math.Max(0, p95Index)] : 0.0;

        return Create(
            recallAt5: recallAt5,
            recallAt10: recallAt10,
            ndcgAt10: ndcgAt10,
            mrr: mrr,
            p95LatencyMs: p95Latency,
            answerCorrectness: answerCorrectness,
            sampleCount: successful.Count,
            labeledSampleCount: labeled.Count,
            unlabeledSampleCount: successful.Count - labeled.Count,
            citationAccuracy: citationAccuracy,
            citationStructuralValidity: citationStructuralValidity,
            citedSampleCount: citationGraded.Count
        );
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
    /// Checks if metrics meet Phase 5 target (Recall@10 &gt;= 70%, P95 &lt; 1.5s).
    /// </summary>
    public bool MeetsPhase5Target() =>
        RecallAt10 >= 0.70 && P95LatencyMs < 1500.0;

    /// <summary>
    /// The citation-accuracy gate for the in-session live path (#3467): no enhancement wired in #3390
    /// may regress citation accuracy below this page-level threshold.
    /// </summary>
    public const double CitationAccuracyTarget = 0.80;

    /// <summary>
    /// Checks if metrics meet the citation-accuracy target (<see cref="CitationAccuracyTarget"/>).
    /// This is the per-enhancement gate #3390 must satisfy, alongside <see cref="MeetsPhase5Target"/>.
    /// </summary>
    public bool MeetsCitationAccuracyTarget() =>
        CitationAccuracy >= CitationAccuracyTarget;
}
