using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Multi-model ensemble evaluator for expert-level strategic decisions.
/// Issue #3771: Enhances Decisore with GPT-4 + Claude consensus.
/// </summary>
public interface IMultiModelEvaluator
{
    /// <summary>
    /// Evaluates a move using multiple LLMs and calculates consensus.
    /// </summary>
    Task<ConsensusResult> EvaluateWithEnsembleAsync(
        CandidateMove move,
        ParsedGameState state,
        string playerColor,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Result of multi-model consensus evaluation.
/// </summary>
public sealed record ConsensusResult
{
    public required double Score { get; init; }  // Consensus score 0-1
    public required double Confidence { get; init; }  // Based on agreement 0-1
    public required string Agreement { get; init; }  // "high", "medium", "low"
    public required string Reasoning { get; init; }  // Primary model reasoning
    public required List<string> Pros { get; init; }
    public required List<string> Cons { get; init; }
    public required string ExpectedOutcome { get; init; }
    public required double Variance { get; init; }  // Score variance across models
}
