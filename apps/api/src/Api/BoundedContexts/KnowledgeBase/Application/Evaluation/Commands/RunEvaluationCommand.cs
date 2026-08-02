using MediatR;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;

/// <summary>
/// Command to run RAG evaluation on a dataset.
/// </summary>
internal sealed record RunEvaluationCommand : IRequest<EvaluationResult>
{
    /// <summary>
    /// Path or name of the dataset to evaluate.
    /// </summary>
    public required string DatasetPath { get; init; }

    /// <summary>
    /// Configuration name for this evaluation run (e.g., "baseline", "with_reranking").
    /// </summary>
    public string Configuration { get; init; } = "baseline";

    /// <summary>
    /// Maximum number of samples to evaluate (for quick tests). Null = all samples.
    /// </summary>
    public int? MaxSamples { get; init; }

    /// <summary>
    /// Whether to include answer correctness evaluation (requires LLM calls).
    /// </summary>
    public bool EvaluateAnswerCorrectness { get; init; } = true;

    /// <summary>
    /// Number of top results to retrieve per query.
    /// </summary>
    public int TopK { get; init; } = 10;

    /// <summary>
    /// #3390 Slice 4 Step 1: RAG enhancement identifiers to activate on the grounded eval seam
    /// (e.g. <c>["crag-evaluation"]</c>, or <c>["rag.enhancement.raptor-retrieval"]</c>). When
    /// <b>non-null</b> the eval runs through <c>AssemblePromptAsync</c> under
    /// <c>RetrievalPolicy.LiveSessionWith(parsedSet)</c> — an <b>empty</b> list is the grounded
    /// baseline (no enhancements). When <b>null</b>, the legacy <c>AskWithHybridSearchAsync</c> path
    /// runs (preserves the #3477 baseline). Parsed via <c>RagEnhancementExtensions.ParseFlags</c>
    /// (fail-loud on an unknown identifier).
    /// </summary>
    public IReadOnlyList<string>? Enhancements { get; init; }
}
