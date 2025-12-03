using MediatR;
using Api.BoundedContexts.KnowledgeBase.Domain.Evaluation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Evaluation.Commands;

/// <summary>
/// Command to run RAG evaluation on a dataset.
/// </summary>
public sealed record RunEvaluationCommand : IRequest<EvaluationResult>
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
}
