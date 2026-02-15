using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Multi-model ensemble evaluator with consensus scoring.
/// Issue #3771: GPT-4 + Claude-3.5 parallel evaluation for expert decisions.
/// </summary>
internal sealed class MultiModelEvaluator : IMultiModelEvaluator
{
    private readonly ILlmService _llmService;
    private readonly IHybridCacheService _cacheService;
    private readonly ILogger<MultiModelEvaluator> _logger;

    private const string SystemPrompt = """
        You are an expert chess strategist. Evaluate the move and provide JSON:
        {"score": 0.85, "reasoning": "...", "pros": [...], "cons": [...], "expectedOutcome": "..."}
        """;

    public MultiModelEvaluator(
        ILlmService llmService,
        IHybridCacheService cacheService,
        ILogger<MultiModelEvaluator> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ConsensusResult> EvaluateWithEnsembleAsync(
        CandidateMove move,
        ParsedGameState state,
        string playerColor,
        CancellationToken cancellationToken = default)
    {
        // Issue #4332: Position caching for cost optimization
        // Cache key: hash(FEN + player + move notation)
        var cacheKey = GeneratePositionCacheKey(state, playerColor, move);

        return await _cacheService.GetOrCreateAsync(
            cacheKey,
            factory: async ct => await EvaluateUncachedAsync(move, state, playerColor, ct).ConfigureAwait(false),
            tags: ["multi-model", "decisore", $"player:{playerColor}"],
            expiration: TimeSpan.FromHours(24),  // Chess positions are deterministic
            cancellationToken).ConfigureAwait(false);
    }

    private async Task<ConsensusResult> EvaluateUncachedAsync(
        CandidateMove move,
        ParsedGameState state,
        string playerColor,
        CancellationToken cancellationToken)
    {
        var prompt = BuildPrompt(move, state, playerColor);

        // Issue #4332: Real multi-model evaluation with GPT-4 + Claude + DeepSeek
        // OpenRouter model IDs: https://openrouter.ai/docs#models
        var evaluationTasks = new[]
        {
            EvaluateWithModelAsync("openai/gpt-4", prompt, cancellationToken),
            EvaluateWithModelAsync("anthropic/claude-3.5-sonnet", prompt, cancellationToken),
            EvaluateWithModelAsync("deepseek/deepseek-chat", prompt, cancellationToken)
        };

        var evaluations = await Task.WhenAll(evaluationTasks).ConfigureAwait(false);

        // Calculate consensus with weighted voting
        // Issue #4332: Model-specific weights based on capabilities
        var modelWeights = new Dictionary<string, double>(StringComparer.Ordinal)
        {
            ["openai/gpt-4"] = 0.40,           // Highest weight for strategic analysis
            ["anthropic/claude-3.5-sonnet"] = 0.35,  // Strong reasoning
            ["deepseek/deepseek-chat"] = 0.25         // Economical baseline
        };

        var weightedScores = new List<(double score, double weight, string model)>();
        for (int i = 0; i < evaluations.Length; i++)
        {
            var eval = evaluations[i];
            if (eval != null)
            {
                var modelId = i == 0 ? "openai/gpt-4" : i == 1 ? "anthropic/claude-3.5-sonnet" : "deepseek/deepseek-chat";
                var weight = modelWeights.GetValueOrDefault(modelId, 1.0 / evaluations.Length);
                weightedScores.Add((eval.Score, weight, modelId));
            }
        }

        if (weightedScores.Count == 0)
        {
            _logger.LogWarning("All ensemble evaluations failed");
            return CreateFallbackResult(move);
        }

        // Weighted consensus score
        var totalWeight = weightedScores.Sum(x => x.weight);
        var weightedMeanScore = weightedScores.Sum(x => x.score * x.weight) / totalWeight;

        // Variance for confidence calculation (unweighted for agreement measure)
        var scores = weightedScores.Select(x => x.score).ToList();
        var variance = CalculateVariance(scores);
        var agreement = ClassifyAgreement(variance);
        var confidence = CalculateConfidence(variance);

        _logger.LogInformation(
            "Multi-model consensus: score={Score:F2}, variance={Variance:F4}, agreement={Agreement}, models={Count}",
            weightedMeanScore, variance, agreement, weightedScores.Count);

        // Use first successful evaluation for reasoning
        var primaryEval = evaluations.FirstOrDefault(e => e != null);

        return new ConsensusResult
        {
            Score = weightedMeanScore,  // Issue #4332: Weighted consensus score
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
            // Issue #4332: Call specific model via GenerateCompletionWithModelAsync
            var result = await _llmService.GenerateCompletionWithModelAsync(
                modelName,  // Explicit model ID (e.g., "openai/gpt-4")
                SystemPrompt,
                prompt,
                cancellationToken).ConfigureAwait(false);

            if (!result.Success || string.IsNullOrWhiteSpace(result.Response))
            {
                _logger.LogWarning("Model {Model} returned no response", modelName);
                return null;
            }

            var evaluation = JsonSerializer.Deserialize<ModelEvaluation>(result.Response);
            if (evaluation == null)
            {
                _logger.LogWarning("Failed to parse JSON from model {Model}", modelName);
                return null;
            }

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

    /// <summary>
    /// Issue #4332: Generate deterministic cache key for position evaluation.
    /// </summary>
    private static string GeneratePositionCacheKey(ParsedGameState state, string playerColor, CandidateMove move)
    {
        var input = $"{state.GetFen()}|{playerColor}|{move.ToAlgebraicNotation()}";
        var hash = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input)));
        return $"multimodel:eval:{hash[..16]}";  // Use first 16 hex chars (64 bits)
    }
}
