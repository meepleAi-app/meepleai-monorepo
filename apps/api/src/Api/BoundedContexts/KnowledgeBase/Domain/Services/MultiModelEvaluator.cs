using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Multi-model ensemble evaluator with consensus scoring.
/// Issue #3771: GPT-4 + Claude-3.5 parallel evaluation for expert decisions.
/// </summary>
internal sealed class MultiModelEvaluator : IMultiModelEvaluator
{
    private readonly HybridLlmService _llmService;
    private readonly ILogger<MultiModelEvaluator> _logger;

    private const string SystemPrompt = """
        You are an expert chess strategist. Evaluate the move and provide JSON:
        {"score": 0.85, "reasoning": "...", "pros": [...], "cons": [...], "expectedOutcome": "..."}
        """;

    public MultiModelEvaluator(HybridLlmService llmService, ILogger<MultiModelEvaluator> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ConsensusResult> EvaluateWithEnsembleAsync(
        CandidateMove move,
        ParsedGameState state,
        string playerColor,
        CancellationToken cancellationToken = default)
    {
        var prompt = BuildPrompt(move, state, playerColor);

        // Parallel LLM calls (simulated - actual multi-model requires multiple API keys)
        // For MVP: Call same LLM 3 times to validate parallel execution pattern
        var evaluationTasks = new[]
        {
            EvaluateWithModelAsync("gpt-4", prompt, cancellationToken),
            EvaluateWithModelAsync("claude-3.5", prompt, cancellationToken),
            EvaluateWithModelAsync("deepseek", prompt, cancellationToken)
        };

        var evaluations = await Task.WhenAll(evaluationTasks).ConfigureAwait(false);

        // Calculate consensus
        var scores = evaluations.Where(e => e != null).Select(e => e!.Score).ToList();

        if (scores.Count == 0)
        {
            _logger.LogWarning("All ensemble evaluations failed");
            return CreateFallbackResult(move);
        }

        var meanScore = scores.Average();
        var variance = CalculateVariance(scores);
        var agreement = ClassifyAgreement(variance);
        var confidence = CalculateConfidence(variance);

        // Use first successful evaluation for reasoning
        var primaryEval = evaluations.FirstOrDefault(e => e != null);

        return new ConsensusResult
        {
            Score = meanScore,
            Confidence = confidence,
            Agreement = agreement,
            Reasoning = primaryEval?.Reasoning ?? "Consensus evaluation",
            Pros = primaryEval?.Pros ?? new List<string>(),
            Cons = primaryEval?.Cons ?? new List<string>(),
            ExpectedOutcome = primaryEval?.ExpectedOutcome ?? "Multiple outcomes possible",
            Variance = variance
        };
    }

    private async Task<ModelEvaluation?> EvaluateWithModelAsync(
        string modelName,
        string prompt,
        CancellationToken cancellationToken)
    {
        try
        {
            // MVP: Use same LLM service (will be extended to actual multi-model in Phase 2)
            var result = await _llmService.GenerateCompletionAsync(
                SystemPrompt,
                prompt,
                cancellationToken).ConfigureAwait(false);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Response))
                return null;

            var evaluation = JsonSerializer.Deserialize<ModelEvaluation>(result.Response);
            return evaluation;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Model {Model} evaluation failed", modelName);
            return null;
        }
    }

    private static string BuildPrompt(CandidateMove move, ParsedGameState state, string playerColor)
    {
        return $"""
            Position: {state.GetFen()}
            Player: {playerColor}
            Move: {move.ToAlgebraicNotation()}
            Heuristic: {move.Score.Overall:F2}

            Evaluate strategically.
            """;
    }

    private static double CalculateVariance(List<double> scores)
    {
        if (scores.Count < 2)
            return 0.0;

        var mean = scores.Average();
        var sumSquaredDiff = scores.Sum(s => Math.Pow(s - mean, 2));
        return sumSquaredDiff / scores.Count;
    }

    private static string ClassifyAgreement(double variance)
    {
        return variance < 0.01 ? "high" : variance < 0.04 ? "medium" : "low";
    }

    private static double CalculateConfidence(double variance)
    {
        // High agreement = high confidence
        return variance < 0.01 ? 0.95 : variance < 0.04 ? 0.75 : 0.50;
    }

    private static ConsensusResult CreateFallbackResult(CandidateMove move)
    {
        return new ConsensusResult
        {
            Score = Math.Clamp((move.Score.Overall + 5) / 10, 0.0, 1.0),
            Confidence = 0.3,
            Agreement = "none",
            Reasoning = "Ensemble unavailable, using heuristic",
            Pros = new List<string>(),
            Cons = new List<string>(),
            ExpectedOutcome = "Uncertain",
            Variance = 0.0
        };
    }

    private sealed record ModelEvaluation(
        double Score,
        string Reasoning,
        List<string> Pros,
        List<string> Cons,
        string ExpectedOutcome);
}
