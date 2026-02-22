using System.Diagnostics;
using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs.Decisore;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Decisore Agent domain service for strategic game analysis.
/// Issue #3769: Orchestrates move generation, evaluation, and LLM refinement.
/// </summary>
internal sealed class DecisoreAgentService : IDecisoreAgentService
{
    private readonly IMoveGeneratorService _moveGenerator;
    private readonly HybridLlmService _llmService;
    private readonly IMultiModelEvaluator? _ensembleEvaluator;
    private readonly ILogger<DecisoreAgentService> _logger;

    private const string SystemPrompt = """
        You are an expert chess strategist analyzing board positions.

        Evaluate the provided move and provide strategic reasoning.
        Output ONLY valid JSON (no markdown):
        {
          "score": 0.85,
          "reasoning": "Brief strategic explanation (max 200 chars)",
          "pros": ["Advantage 1", "Advantage 2"],
          "cons": ["Risk 1"],
          "expectedOutcome": "Brief outcome description"
        }
        """;

    public DecisoreAgentService(
        IMoveGeneratorService moveGenerator,
        HybridLlmService llmService,
        ILogger<DecisoreAgentService> logger,
        IMultiModelEvaluator? ensembleEvaluator = null)
    {
        _moveGenerator = moveGenerator ?? throw new ArgumentNullException(nameof(moveGenerator));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _ensembleEvaluator = ensembleEvaluator;  // Optional for ensemble mode
    }

    public async Task<StrategicAnalysisResultDto> AnalyzePositionAsync(
        ParsedGameState state,
        string playerColor,
        int maxSuggestions = 3,
        bool useEnsemble = false,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(state);

        var stopwatch = Stopwatch.StartNew();
        var suggestionId = Guid.NewGuid(); // Issue #4335: Correlation ID for feedback tracking

        _logger.LogInformation(
            "Decisore analyzing position: player={Player}, turn={Turn}, maxSuggestions={Max}",
            playerColor,
            state.TurnNumber,
            maxSuggestions);

        // STEP 1: Generate candidate moves (uses #3770)
        var candidates = await _moveGenerator.GenerateCandidatesAsync(
            state,
            playerColor,
            maxCandidates: Math.Min(maxSuggestions * 3, 20),  // Generate more for LLM filtering
            cancellationToken).ConfigureAwait(false);

        if (candidates.Count == 0)
        {
            _logger.LogWarning("No legal moves available (stalemate or checkmate)");
            return CreateEmptyResult(stopwatch.ElapsedMilliseconds);
        }

        // STEP 2: Refine top N candidates with LLM (or ensemble if enabled)
        var topCandidates = candidates.Take(maxSuggestions).ToList();
        var suggestions = new List<MoveSuggestionDto>();

        foreach (var candidate in topCandidates)
        {
            MoveSuggestionDto suggestion;

            if (useEnsemble && _ensembleEvaluator != null)
            {
                suggestion = await RefineCandidateWithEnsembleAsync(
                    candidate,
                    state,
                    playerColor,
                    cancellationToken).ConfigureAwait(false);
            }
            else
            {
                suggestion = await RefineCandidateWithLLMAsync(
                    candidate,
                    state,
                    playerColor,
                    cancellationToken).ConfigureAwait(false);
            }

            suggestions.Add(suggestion);
        }

        // STEP 3: Evaluate overall position strength
        var positionStrength = EvaluatePositionStrength(state, playerColor, candidates);

        // STEP 4: Identify victory paths (if winning)
        var victoryPaths = positionStrength > 0.3
            ? IdentifyVictoryPaths(state, playerColor, suggestions)
            : new List<string>();

        // STEP 5: Calculate risk level
        var riskLevel = CalculateRiskLevel(positionStrength, suggestions);

        // STEP 6: Calculate confidence
        var confidence = CalculateConfidence(suggestions, candidates.Count);

        stopwatch.Stop();

        // Issue #3774: Performance metrics logging
        _logger.LogInformation(
            "Decisore analysis complete: suggestions={Count}, strength={Strength:F2}, time={Time}ms, " +
            "candidates={Candidates}, riskLevel={Risk}, confidence={Confidence:F2}",
            suggestions.Count,
            positionStrength,
            stopwatch.ElapsedMilliseconds,
            candidates.Count,
            riskLevel,
            confidence);

        // Performance warning if exceeds target
        if (stopwatch.ElapsedMilliseconds > 5000)
        {
            _logger.LogWarning(
                "Decisore analysis exceeded 5s target: {Time}ms (candidates={Candidates}, depth={Depth})",
                stopwatch.ElapsedMilliseconds,
                candidates.Count,
                useEnsemble ? "deep" : "standard");
        }

        return new StrategicAnalysisResultDto
        {
            SuggestionId = suggestionId,
            Suggestions = suggestions,
            PositionStrength = positionStrength,
            RiskLevel = riskLevel,
            VictoryPaths = victoryPaths,
            Confidence = confidence,
            ExecutionTimeMs = (int)stopwatch.ElapsedMilliseconds,
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<MoveSuggestionDto> RefineCandidateWithLLMAsync(
        CandidateMove candidate,
        ParsedGameState state,
        string playerColor,
        CancellationToken cancellationToken)
    {
        try
        {
            // Build prompt with move context
            var prompt = $"""
                Chess Position (FEN): {state.GetFen()}
                Player: {playerColor}
                Candidate Move: {candidate.ToAlgebraicNotation()}
                Heuristic Score: {candidate.Score.Overall:F2}
                Move Description: {candidate.GetDescription()}

                Analyze this move strategically and provide evaluation.
                """;

            var llmResult = await _llmService.GenerateCompletionAsync(
                systemPrompt: SystemPrompt,
                userPrompt: prompt,
                source: RequestSource.AgentTask,
                ct: cancellationToken).ConfigureAwait(false);

            if (!llmResult.Success || string.IsNullOrWhiteSpace(llmResult.Response))
            {
                _logger.LogWarning("LLM refinement failed, using heuristic only");
                return CreateFallbackSuggestion(candidate);
            }

            // Parse LLM JSON response
            var evaluation = JsonSerializer.Deserialize<LlmEvaluation>(llmResult.Response);
            if (evaluation == null)
            {
                _logger.LogWarning("Failed to parse LLM response, using fallback");
                return CreateFallbackSuggestion(candidate);
            }

            // Combine heuristic + LLM scores
            var combinedScore = (0.4 * candidate.Score.Overall) + (0.6 * evaluation.Score);

            return new MoveSuggestionDto
            {
                Move = candidate.ToAlgebraicNotation(),
                Position = candidate.To.Notation,
                Score = Math.Clamp(combinedScore, 0.0, 1.0),
                Reasoning = evaluation.Reasoning,
                Pros = evaluation.Pros ?? new List<string>(),
                Cons = evaluation.Cons ?? new List<string>(),
                ExpectedOutcome = evaluation.ExpectedOutcome ?? "Continuation unclear"
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during LLM refinement for move {Move}", candidate.ToAlgebraicNotation());
            return CreateFallbackSuggestion(candidate);
        }
    }

    private static MoveSuggestionDto CreateFallbackSuggestion(CandidateMove candidate)
    {
        return new MoveSuggestionDto
        {
            Move = candidate.ToAlgebraicNotation(),
            Position = candidate.To.Notation,
            Score = Math.Clamp((candidate.Score.Overall + 5) / 10, 0.0, 1.0),  // Normalize to 0-1
            Reasoning = $"Heuristic evaluation: {candidate.Score}",
            Pros = new List<string> { $"Material: {candidate.Score.Material:F1}", $"Position: {candidate.Score.Positional:F2}" },
            Cons = new List<string>(),
            ExpectedOutcome = candidate.IsCapture ? "Material advantage" : "Positional improvement"
        };
    }

    private static double EvaluatePositionStrength(
        ParsedGameState state,
        string playerColor,
        List<CandidateMove> candidates)
    {
        if (candidates.Count == 0)
            return -1.0;  // No moves = losing

        // Material advantage
        var opponentColor = string.Equals(playerColor, "White", StringComparison.Ordinal) ? "Black" : "White";
        var playerMaterial = state.ChessBoard!.GetMaterialCount(playerColor);
        var opponentMaterial = state.ChessBoard.GetMaterialCount(opponentColor);
        var materialAdvantage = (playerMaterial - opponentMaterial) / 39.0;  // Normalize by max material (Q+2R+2B+2N+8P = 39)

        // Best move quality
        var bestMoveScore = candidates[0].Score.Overall;

        // Mobility (number of legal moves)
        var mobility = Math.Min(candidates.Count / 40.0, 1.0);  // Normalize by typical middlegame moves

        // Weighted combination
        var strength = (0.5 * materialAdvantage) + (0.3 * bestMoveScore / 10.0) + (0.2 * mobility);

        return Math.Clamp(strength, -1.0, 1.0);
    }

    private static List<string> IdentifyVictoryPaths(
        ParsedGameState state,
        string playerColor,
        List<MoveSuggestionDto> suggestions)
    {
        var paths = new List<string>();

        // Simple heuristic-based victory path identification
        var topMove = suggestions.Count > 0 ? suggestions[0] : null;
        if (topMove == null)
            return paths;

        if (topMove.Score > 0.8)
        {
            paths.Add("Maintain material advantage → convert to winning endgame");
        }
        else if (topMove.Reasoning.Contains("center", StringComparison.OrdinalIgnoreCase))
        {
            paths.Add("Control center → limit opponent mobility → positional advantage");
        }
        else if (topMove.Move.Contains('x'))  // Capture
        {
            paths.Add("Material gain → simplify position → technical endgame");
        }
        else
        {
            paths.Add("Improve position → create threats → tactical advantage");
        }

        return paths;
    }

    private static string CalculateRiskLevel(double positionStrength, List<MoveSuggestionDto> suggestions)
    {
        // Low risk if winning and good moves available
        if (positionStrength > 0.5 && suggestions.Any(s => s.Score > 0.7))
            return "low";

        // High risk if losing or forced moves
        if (positionStrength < -0.3 || suggestions.Count < 3)
            return "high";

        return "medium";
    }

    private static double CalculateConfidence(List<MoveSuggestionDto> suggestions, int totalCandidates)
    {
        if (suggestions.Count == 0)
            return 0.0;

        // Higher confidence if top moves are clearly better
        var topScore = suggestions[0].Score;
        var avgScore = suggestions.Average(s => s.Score);
        var scoreDivergence = topScore - avgScore;

        // Many good moves = lower confidence (position complex)
        var moveQuality = totalCandidates > 20 ? 0.7 : 0.9;

        return Math.Clamp((scoreDivergence + 0.5) * moveQuality, 0.3, 0.95);
    }

    private static StrategicAnalysisResultDto CreateEmptyResult(long elapsedMs)
    {
        return new StrategicAnalysisResultDto
        {
            SuggestionId = Guid.NewGuid(),
            Suggestions = new List<MoveSuggestionDto>(),
            PositionStrength = -1.0,
            RiskLevel = "critical",
            VictoryPaths = new List<string>(),
            Confidence = 1.0,  // Certain there are no moves
            ExecutionTimeMs = (int)elapsedMs,
            Timestamp = DateTime.UtcNow
        };
    }

    private async Task<MoveSuggestionDto> RefineCandidateWithEnsembleAsync(
        CandidateMove candidate,
        ParsedGameState state,
        string playerColor,
        CancellationToken cancellationToken)
    {
        try
        {
            var consensus = await _ensembleEvaluator!.EvaluateWithEnsembleAsync(
                candidate,
                state,
                playerColor,
                cancellationToken).ConfigureAwait(false);

            return new MoveSuggestionDto
            {
                Move = candidate.ToAlgebraicNotation(),
                Position = candidate.To.Notation,
                Score = consensus.Score,
                Reasoning = $"{consensus.Reasoning} (Consensus: {consensus.Agreement}, models agree)",
                Pros = consensus.Pros,
                Cons = consensus.Cons,
                ExpectedOutcome = consensus.ExpectedOutcome
            };
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ensemble evaluation failed, falling back to single model");
            return await RefineCandidateWithLLMAsync(candidate, state, playerColor, cancellationToken).ConfigureAwait(false);
        }
    }

    private sealed record LlmEvaluation(
        double Score,
        string Reasoning,
        List<string>? Pros,
        List<string>? Cons,
        string? ExpectedOutcome);
}
