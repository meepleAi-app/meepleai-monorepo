using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Services;

/// <summary>
/// Service for evaluating RAG pipeline quality using standard metrics.
/// Implements Recall@K, nDCG@K, MRR, and answer correctness evaluation.
/// Named IDatasetEvaluationService to avoid conflict with Api.Services.IRagEvaluationService.
/// </summary>
internal interface IDatasetEvaluationService
{
    /// <summary>
    /// Runs evaluation on a complete dataset.
    /// </summary>
    Task<EvaluationResult> EvaluateDatasetAsync(
        EvaluationDataset dataset,
        EvaluationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Evaluates a single sample.
    /// </summary>
    Task<EvaluationSampleResult> EvaluateSampleAsync(
        EvaluationSample sample,
        EvaluationOptions options,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Computes aggregated metrics from sample results.
    /// </summary>
    EvaluationMetrics ComputeMetrics(IReadOnlyList<EvaluationSampleResult> sampleResults);

    /// <summary>
    /// Calculates Recall@K for a set of retrieved vs relevant chunk IDs.
    /// </summary>
    double CalculateRecallAtK(IReadOnlyList<string> retrievedChunkIds, IReadOnlyList<string> relevantChunkIds, int k);

    /// <summary>
    /// Calculates nDCG@K for retrieved results.
    /// </summary>
    double CalculateNdcgAtK(IReadOnlyList<string> retrievedChunkIds, IReadOnlyList<string> relevantChunkIds, int k);

    /// <summary>
    /// Calculates Mean Reciprocal Rank.
    /// </summary>
    double CalculateMrr(IReadOnlyList<string> retrievedChunkIds, IReadOnlyList<string> relevantChunkIds);
}

/// <summary>
/// Options for evaluation execution.
/// </summary>
internal sealed record EvaluationOptions
{
    /// <summary>
    /// Configuration name for tracking.
    /// </summary>
    public string Configuration { get; init; } = "baseline";

    /// <summary>
    /// Number of top results to retrieve.
    /// </summary>
    public int TopK { get; init; } = 10;

    /// <summary>
    /// Whether to evaluate answer correctness (requires LLM calls).
    /// </summary>
    public bool EvaluateAnswerCorrectness { get; init; } = true;

    /// <summary>
    /// Maximum number of samples to evaluate. Null = all.
    /// </summary>
    public int? MaxSamples { get; init; }

    /// <summary>
    /// Game ID to filter queries by (for MeepleAI-specific evaluation).
    /// </summary>
    public string? GameIdFilter { get; init; }

    /// <summary>
    /// #3390 Slice 4 Step 1: when non-null, the eval exercises the SAME grounded retrieval seam the
    /// in-session live path uses (<c>AssemblePromptAsync</c>) under a <c>RetrievalPolicy</c> carrying
    /// this explicit enhancement set — so flipping an enhancement actually changes retrieval and the
    /// eval can measure it. <c>None</c> = grounded baseline (no enhancements). When <b>null</b>, the
    /// legacy <c>AskWithHybridSearchAsync</c> path is used (backward-compatible; preserves the #3477
    /// baseline's reproducibility). Note: the legacy path never touches the enhancement gate, so
    /// per-enhancement measurement REQUIRES this to be non-null (#3390 R1 / #3475-class gap).
    /// </summary>
    public Api.BoundedContexts.KnowledgeBase.Domain.Enums.RagEnhancement? GroundedEnhancements { get; init; }

    /// <summary>
    /// Default options for baseline evaluation.
    /// </summary>
    public static EvaluationOptions Default => new();
}
