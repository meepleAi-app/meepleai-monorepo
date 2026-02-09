using System.Diagnostics;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Arbitro Agent: AI-powered move validation with game state awareness.
/// Issue #3760: Arbitro Agent Move Validation Logic.
/// </summary>
/// <remarks>
/// Orchestrates:
/// 1. Game state context retrieval (via GameStateSource)
/// 2. Applicable rule extraction (via MoveValidationDomainService)
/// 3. AI prompt assembly with structured output
/// 4. LLM inference for intelligent move arbitration
/// 5. Response parsing to MoveValidationResultDto
/// </remarks>
internal sealed class ArbitroAgentService : IArbitroAgentService
{
    private readonly GameStateSource _gameStateSource;
    private readonly MoveValidationDomainService _moveValidationService;
    private readonly ILlmService _llmService;
    private readonly ILogger<ArbitroAgentService> _logger;
    private readonly TimeProvider _timeProvider;

    private const string SystemPrompt = """
        You are an expert board game rules arbitrator. Your task is to validate player moves strictly according to game rules.

        Analyze the provided game state, applicable rules, and the proposed move.
        Determine if the move is VALID, INVALID, or UNCERTAIN.

        Output ONLY a valid JSON object with this exact structure (no markdown, no explanation outside JSON):
        {
          "decision": "VALID" | "INVALID" | "UNCERTAIN",
          "confidence": 0.85,
          "reasoning": "Brief explanation (max 200 chars)",
          "violatedRules": ["rule_key_1", "rule_key_2"],
          "suggestions": ["Alternative action if invalid"]
        }
        """;

    public ArbitroAgentService(
        GameStateSource gameStateSource,
        MoveValidationDomainService moveValidationService,
        ILlmService llmService,
        ILogger<ArbitroAgentService> logger,
        TimeProvider timeProvider)
    {
        _gameStateSource = gameStateSource ?? throw new ArgumentNullException(nameof(gameStateSource));
        _moveValidationService = moveValidationService ?? throw new ArgumentNullException(nameof(moveValidationService));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    /// <summary>
    /// Validates a player move using AI-powered arbitration.
    /// </summary>
    /// <param name="session">Active game session.</param>
    /// <param name="move">Move to validate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Validation result with AI reasoning.</returns>
    public async Task<MoveValidationResultDto> ValidateMoveAsync(
        GameSession session,
        Move move,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(move);

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Arbitro validating move: session={SessionId}, player={Player}, action={Action}",
            session.Id,
            move.PlayerName,
            move.Action);

        // STEP 1: Basic session validation (fast fail)
        if (session.Status.IsFinished)
        {
            return CreateInvalidResult(
                decision: "INVALID",
                confidence: 1.0,
                reasoning: $"Session is {session.Status}, cannot validate moves",
                violatedRules: new List<string> { "session_state" },
                suggestions: new List<string> { "Session must be active to validate moves" },
                applicableRules: new List<ArbitroRuleAtomDto>(),
                elapsedMs: stopwatch.ElapsedMilliseconds);
        }

        if (!session.HasPlayer(move.PlayerName))
        {
            return CreateInvalidResult(
                decision: "INVALID",
                confidence: 1.0,
                reasoning: $"Player '{move.PlayerName}' not in session",
                violatedRules: new List<string> { "player_membership" },
                suggestions: new List<string> { $"Valid players: {string.Join(", ", session.Players.Select(p => p.PlayerName))}" },
                applicableRules: new List<ArbitroRuleAtomDto>(),
                elapsedMs: stopwatch.ElapsedMilliseconds);
        }

        // STEP 2: Retrieve game state context
        var gameStateContext = await RetrieveGameStateAsync(session.GameId, cancellationToken)
            .ConfigureAwait(false);

        // STEP 3: Get applicable rules from domain service
        var applicableRules = await _moveValidationService.GetApplicableRulesAsync(
            session.GameId,
            move,
            ruleSpecVersion: null,
            cancellationToken).ConfigureAwait(false);

        if (applicableRules.Count == 0)
        {
            _logger.LogWarning("No applicable rules found for move action '{Action}'", move.Action);

            return CreateUncertainResult(
                confidence: 0.3,
                reasoning: "No specific rules found for this type of move",
                applicableRules: new List<ArbitroRuleAtomDto>(),
                elapsedMs: stopwatch.ElapsedMilliseconds);
        }

        // STEP 4: Assemble AI prompt
        var prompt = AssembleValidationPrompt(gameStateContext, applicableRules, move, session);

        // STEP 5: Call LLM for validation
        var llmResult = await _llmService.GenerateCompletionAsync(
            systemPrompt: SystemPrompt,
            userPrompt: prompt,
            cancellationToken).ConfigureAwait(false);

        if (!llmResult.Success)
        {
            _logger.LogError("LLM validation failed: {Error}", llmResult.ErrorMessage);

            return CreateUncertainResult(
                confidence: 0.0,
                reasoning: $"AI validation unavailable: {llmResult.ErrorMessage}",
                applicableRules: MapToRuleAtomDtos(applicableRules),
                elapsedMs: stopwatch.ElapsedMilliseconds);
        }

        // STEP 6: Parse AI response to structured result
        var validationResult = ParseAiResponse(llmResult.Response, applicableRules, stopwatch.ElapsedMilliseconds);

        _logger.LogInformation(
            "Arbitro validation complete: decision={Decision}, confidence={Confidence:F2}, latency={Latency}ms",
            validationResult.Decision,
            validationResult.Confidence,
            stopwatch.ElapsedMilliseconds);

        return validationResult;
    }

    private async Task<string> RetrieveGameStateAsync(Guid gameId, CancellationToken cancellationToken)
    {
        var contextRequest = new ContextRetrievalRequest
        {
            Query = "current game state", // Dummy query for state retrieval
            GameId = gameId,
            MaxTokens = 2000,
            MaxItems = 1 // Only latest snapshot
        };

        var result = await _gameStateSource.RetrieveAsync(contextRequest, cancellationToken)
            .ConfigureAwait(false);

        if (!result.IsSuccess || result.Items.Count == 0)
        {
            _logger.LogWarning("No game state found for game {GameId}", gameId);
            return "[No current game state available]";
        }

        // Extract board state JSON from the latest snapshot
        var latestState = result.Items.OrderByDescending(i => i.Relevance).First();
        return latestState.Content;
    }

    private string AssembleValidationPrompt(
        string gameState,
        List<RuleAtom> applicableRules,
        Move move,
        GameSession session)
    {
        var rulesText = string.Join("\n", applicableRules.Select((r, i) =>
            $"[{i + 1}] {r.id} ({r.section ?? "General"}): {r.text}"));

        var additionalContextText = move.AdditionalContext != null && move.AdditionalContext.Count > 0
            ? string.Join(", ", move.AdditionalContext.Select(kvp => $"{kvp.Key}: {kvp.Value}"))
            : "None";

        return $"""
        Game: {session.GameId}
        Session Status: {session.Status}
        Player Count: {session.PlayerCount}

        {gameState}

        Applicable Rules ({applicableRules.Count} found):
        {rulesText}

        Move to Validate:
        - Player: {move.PlayerName}
        - Action: {move.Action}
        - Position: {move.Position ?? "N/A"}
        - Additional Context: {additionalContextText}

        Analyze and output JSON validation result.
        """;
    }

    private MoveValidationResultDto ParseAiResponse(
        string aiResponse,
        List<RuleAtom> applicableRules,
        long elapsedMs)
    {
        try
        {
            // Try to parse JSON response
            var json = JsonDocument.Parse(aiResponse);
            var root = json.RootElement;

            var decision = root.GetProperty("decision").GetString() ?? "UNCERTAIN";
            var confidence = root.GetProperty("confidence").GetDouble();
            var reasoning = root.GetProperty("reasoning").GetString() ?? "No reasoning provided";

            var violatedRules = new List<string>();
            if (root.TryGetProperty("violatedRules", out var violatedRulesElement))
            {
                violatedRules = violatedRulesElement.EnumerateArray()
                    .Select(e => e.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();
            }

            List<string>? suggestions = null;
            if (root.TryGetProperty("suggestions", out var suggestionsElement))
            {
                suggestions = suggestionsElement.EnumerateArray()
                    .Select(e => e.GetString() ?? string.Empty)
                    .Where(s => !string.IsNullOrEmpty(s))
                    .ToList();

                if (suggestions.Count == 0)
                    suggestions = null;
            }

            return new MoveValidationResultDto
            {
                Decision = decision,
                Confidence = confidence,
                Reasoning = reasoning,
                ViolatedRules = violatedRules,
                Suggestions = suggestions,
                ApplicableRules = MapToRuleAtomDtos(applicableRules),
                TokenUsage = TokenUsageDto.Empty, // Will be enhanced in Phase 2 with actual LLM metrics
                CostBreakdown = CostBreakdownDto.Zero("Ollama"),
                LatencyMs = (int)elapsedMs,
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse AI response as JSON: {Response}", aiResponse);

            // Fallback: treat as uncertain with raw response as reasoning
            return CreateUncertainResult(
                confidence: 0.5,
                reasoning: $"AI response parsing failed: {aiResponse[..Math.Min(100, aiResponse.Length)]}",
                applicableRules: MapToRuleAtomDtos(applicableRules),
                elapsedMs: elapsedMs);
        }
    }

    private MoveValidationResultDto CreateInvalidResult(
        string decision,
        double confidence,
        string reasoning,
        List<string> violatedRules,
        List<string> suggestions,
        List<ArbitroRuleAtomDto> applicableRules,
        long elapsedMs)
    {
        return new MoveValidationResultDto
        {
            Decision = decision,
            Confidence = confidence,
            Reasoning = reasoning,
            ViolatedRules = violatedRules,
            Suggestions = suggestions,
            ApplicableRules = applicableRules,
            TokenUsage = TokenUsageDto.Empty,
            CostBreakdown = CostBreakdownDto.Zero("Local"),
            LatencyMs = (int)elapsedMs,
            Timestamp = DateTime.UtcNow
        };
    }

    private MoveValidationResultDto CreateUncertainResult(
        double confidence,
        string reasoning,
        List<ArbitroRuleAtomDto> applicableRules,
        long elapsedMs)
    {
        return new MoveValidationResultDto
        {
            Decision = "UNCERTAIN",
            Confidence = confidence,
            Reasoning = reasoning,
            ViolatedRules = new List<string>(),
            Suggestions = null,
            ApplicableRules = applicableRules,
            TokenUsage = TokenUsageDto.Empty,
            CostBreakdown = CostBreakdownDto.Zero("Local"),
            LatencyMs = (int)elapsedMs,
            Timestamp = DateTime.UtcNow
        };
    }

    private static List<ArbitroRuleAtomDto> MapToRuleAtomDtos(List<RuleAtom> rules)
    {
        return rules.Select(r => new ArbitroRuleAtomDto(
            Id: r.id,
            Text: r.text,
            Section: r.section,
            Page: r.page,
            Line: r.line
        )).ToList();
    }
}
