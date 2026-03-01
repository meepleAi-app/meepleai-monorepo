using System.Diagnostics;
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Application.DTOs;
using Api.BoundedContexts.GameManagement.Domain.Entities;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.Services;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Domain.Services.ContextEngineering;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Arbitro Agent: AI-powered move validation with game state awareness and conflict resolution.
/// Issue #3760: Arbitro Agent Move Validation Logic.
/// Issue #3761: Conflict Resolution and Edge Cases.
/// </summary>
/// <remarks>
/// Orchestrates:
/// 1. Game state context retrieval (via GameStateSource)
/// 2. Applicable rule extraction (via MoveValidationDomainService)
/// 3. Conflict detection in applicable rules
/// 4. FAQ lookup for known conflicts (fast path)
/// 5. AI prompt assembly with conflict awareness
/// 6. LLM inference for intelligent move arbitration
/// 7. Escalation for low-confidence conflicts
/// 8. Response parsing to MoveValidationResultDto
/// </remarks>
internal sealed class ArbitroAgentService : IArbitroAgentService
{
    private readonly GameStateSource _gameStateSource;
    private readonly MoveValidationDomainService _moveValidationService;
    private readonly IRuleConflictFaqRepository _conflictFaqRepository;
    private readonly ILlmService _llmService;
    private readonly ILogger<ArbitroAgentService> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly IUnitOfWork _unitOfWork;

    // Escalation threshold: below this confidence with conflicts → recommend human review
    private const double EscalationThreshold = 0.60;

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
        IRuleConflictFaqRepository conflictFaqRepository,
        ILlmService llmService,
        ILogger<ArbitroAgentService> logger,
        TimeProvider timeProvider,
        IUnitOfWork unitOfWork)
    {
        _gameStateSource = gameStateSource ?? throw new ArgumentNullException(nameof(gameStateSource));
        _moveValidationService = moveValidationService ?? throw new ArgumentNullException(nameof(moveValidationService));
        _conflictFaqRepository = conflictFaqRepository ?? throw new ArgumentNullException(nameof(conflictFaqRepository));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
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
        var validationId = Guid.NewGuid(); // Issue #4328: Correlation ID for feedback tracking

        // Issue #4328: Latency breakdown tracking for beta testing metrics
        long stateRetrievalTime = 0, ruleRetrievalTime = 0, conflictDetectionTime = 0, llmInferenceTime = 0;

        _logger.LogInformation(
            "[Arbitro] Validation started: validationId={ValidationId}, session={SessionId}, game={GameId}, player={Player}, action={Action}",
            validationId,
            session.Id,
            session.GameId,
            move.PlayerName,
            move.Action);

        // STEP 1: Basic session validation (fast fail)
        if (session.Status.IsFinished)
        {
            return CreateInvalidResult(
                validationId: validationId,
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
                validationId: validationId,
                decision: "INVALID",
                confidence: 1.0,
                reasoning: $"Player '{move.PlayerName}' not in session",
                violatedRules: new List<string> { "player_membership" },
                suggestions: new List<string> { $"Valid players: {string.Join(", ", session.Players.Select(p => p.PlayerName))}" },
                applicableRules: new List<ArbitroRuleAtomDto>(),
                elapsedMs: stopwatch.ElapsedMilliseconds);
        }

        // STEP 2: Retrieve game state context
        var stateRetrievalStart = stopwatch.ElapsedMilliseconds;
        var gameStateContext = await RetrieveGameStateAsync(session.GameId, cancellationToken)
            .ConfigureAwait(false);
        stateRetrievalTime = stopwatch.ElapsedMilliseconds - stateRetrievalStart;

        // STEP 3: Get applicable rules from domain service
        var ruleRetrievalStart = stopwatch.ElapsedMilliseconds;
        var applicableRules = await _moveValidationService.GetApplicableRulesAsync(
            session.GameId,
            move,
            ruleSpecVersion: null,
            cancellationToken).ConfigureAwait(false);
        ruleRetrievalTime = stopwatch.ElapsedMilliseconds - ruleRetrievalStart;

        _logger.LogInformation(
            "[Arbitro] Rule retrieval: validationId={ValidationId}, rulesFound={RuleCount}, latency={LatencyMs}ms",
            validationId,
            applicableRules.Count,
            ruleRetrievalTime);

        if (applicableRules.Count == 0)
        {
            _logger.LogWarning("No applicable rules found for move action '{Action}'", move.Action);

            return CreateUncertainResult(
                validationId: validationId,
                confidence: 0.3,
                reasoning: "No specific rules found for this type of move",
                applicableRules: new List<ArbitroRuleAtomDto>(),
                conflicts: null,
                elapsedMs: stopwatch.ElapsedMilliseconds);
        }

        // STEP 3.5: NEW (Issue #3761) - Detect rule conflicts
        var conflictDetectionStart = stopwatch.ElapsedMilliseconds;
        var conflicts = DetectRuleConflicts(applicableRules);
        conflictDetectionTime = stopwatch.ElapsedMilliseconds - conflictDetectionStart;

        if (conflicts.Count > 0)
        {
            _logger.LogInformation(
                "[Arbitro] Conflicts detected: validationId={ValidationId}, conflictCount={ConflictCount}, types={ConflictTypes}, latency={LatencyMs}ms",
                validationId,
                conflicts.Count,
                string.Join(", ", conflicts.Select(c => c.Type)),
                conflictDetectionTime);
        }

        // STEP 3.6: NEW (Issue #3761) - Check FAQ for known conflicts (fast path)
        if (conflicts.Count > 0)
        {
            var faqLookupStart = stopwatch.ElapsedMilliseconds;
            var faqResolution = await _conflictFaqRepository.FindByPatternAsync(
                session.GameId,
                conflicts[0].Pattern,
                cancellationToken).ConfigureAwait(false);
            var faqLookupTime = stopwatch.ElapsedMilliseconds - faqLookupStart;

            if (faqResolution != null)
            {
                _logger.LogInformation(
                    "[Arbitro] FAQ fast-path used: validationId={ValidationId}, pattern={Pattern}, faqId={FaqId}, usageCount={UsageCount}, faqLatency={FaqLatencyMs}ms",
                    validationId,
                    faqResolution.Pattern,
                    faqResolution.Id,
                    faqResolution.UsageCount + 1,
                    faqLookupTime);

                // Record usage and return FAQ resolution immediately
                faqResolution.RecordUsage(_timeProvider);
                await _conflictFaqRepository.UpdateAsync(faqResolution, cancellationToken).ConfigureAwait(false);
                await _unitOfWork.SaveChangesAsync(cancellationToken).ConfigureAwait(false); // Dispatch RuleConflictFAQUsedEvent

                return CreateFaqResolvedResult(validationId, faqResolution, conflicts, applicableRules, stopwatch.ElapsedMilliseconds);
            }

            _logger.LogInformation(
                "[Arbitro] FAQ miss: validationId={ValidationId}, pattern={Pattern}, fallback=LLM",
                validationId,
                conflicts[0].Pattern);
        }

        // STEP 4: Assemble AI prompt (conflict-aware if conflicts detected)
        var prompt = conflicts.Count > 0
            ? AssembleConflictResolutionPrompt(gameStateContext, applicableRules, conflicts, move, session)
            : AssembleValidationPrompt(gameStateContext, applicableRules, move, session);

        // STEP 5: Call LLM for validation
        var llmInferenceStart = stopwatch.ElapsedMilliseconds;
        var llmResult = await _llmService.GenerateCompletionAsync(
            systemPrompt: SystemPrompt,
            userPrompt: prompt,
            source: RequestSource.AgentTask,
            ct: cancellationToken).ConfigureAwait(false);
        llmInferenceTime = stopwatch.ElapsedMilliseconds - llmInferenceStart;

        _logger.LogInformation(
            "[Arbitro] LLM inference: validationId={ValidationId}, success={Success}, latency={LatencyMs}ms, hasConflicts={HasConflicts}",
            validationId,
            llmResult.Success,
            llmInferenceTime,
            conflicts.Count > 0);

        if (!llmResult.Success)
        {
            _logger.LogError("LLM validation failed: {Error}", llmResult.ErrorMessage);

            return CreateUncertainResult(
                validationId: validationId,
                confidence: 0.0,
                reasoning: $"AI validation unavailable: {llmResult.ErrorMessage}",
                applicableRules: MapToRuleAtomDtos(applicableRules),
                conflicts: null,
                elapsedMs: stopwatch.ElapsedMilliseconds);
        }

        // STEP 6: Parse AI response to structured result
        var validationResult = ParseAiResponse(validationId, llmResult.Response, applicableRules, conflicts, stopwatch.ElapsedMilliseconds);

        // STEP 7: NEW (Issue #3761) - Escalation check for low-confidence conflicts
        if (validationResult.Confidence < EscalationThreshold && conflicts.Count > 0)
        {
            _logger.LogWarning(
                "Low confidence ({Confidence:F2}) with conflicts detected, recommending human escalation",
                validationResult.Confidence);

            validationResult = validationResult with
            {
                Decision = "UNCERTAIN",
                Suggestions = validationResult.Suggestions != null
                    ? new List<string>(validationResult.Suggestions) { "Recommend human arbitrator review due to rule conflicts" }
                    : new List<string> { "Recommend human arbitrator review due to rule conflicts" }
            };
        }

        // FINAL LOGGING: Structured metrics for beta testing analysis
        var totalLatency = stopwatch.ElapsedMilliseconds;
        _logger.LogInformation(
            "[Arbitro] Validation complete: " +
            "validationId={ValidationId}, " +
            "decision={Decision}, " +
            "confidence={Confidence:F3}, " +
            "conflicts={ConflictCount}, " +
            "resolution={Resolution}, " +
            "totalLatency={TotalLatencyMs}ms, " +
            "breakdown={{state:{StateMs}ms, rules:{RulesMs}ms, conflicts:{ConflictsMs}ms, llm:{LlmMs}ms}}, " +
            "applicableRules={ApplicableRules}, " +
            "violatedRules={ViolatedRules}",
            validationId,
            validationResult.Decision,
            validationResult.Confidence,
            conflicts.Count,
            conflicts.Count > 0 ? (validationResult.ConflictsResolved?[0].ResolutionStrategy ?? "LLM") : "N/A",
            totalLatency,
            stateRetrievalTime,
            ruleRetrievalTime,
            conflictDetectionTime,
            llmInferenceTime,
            applicableRules.Count,
            validationResult.ViolatedRules.Count);

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
        Guid validationId,
        string aiResponse,
        List<RuleAtom> applicableRules,
        List<RuleConflict> conflicts,
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

            var conflictDtos = conflicts.Select(c => new ConflictResolutionDto
            {
                ConflictType = c.Type.ToString(),
                Pattern = c.Pattern,
                ConflictingRuleIds = c.ConflictingRuleIds.ToList(),
                Description = c.Description,
                ResolutionStrategy = "LLM",
                ResolvedBy = "AI"
            }).ToList();

            return new MoveValidationResultDto
            {
                ValidationId = validationId,
                Decision = decision,
                Confidence = confidence,
                Reasoning = reasoning,
                ViolatedRules = violatedRules,
                Suggestions = suggestions,
                ApplicableRules = MapToRuleAtomDtos(applicableRules),
                TokenUsage = TokenUsageDto.Empty, // Will be enhanced in Phase 2 with actual LLM metrics
                CostBreakdown = CostBreakdownDto.Zero("Ollama"),
                LatencyMs = (int)elapsedMs,
                Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
                ConflictDetected = conflicts.Count > 0,
                ConflictsResolved = conflicts.Count > 0 ? conflictDtos : null
            };
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex, "Failed to parse AI response as JSON: {Response}", aiResponse);

            // Fallback: treat as uncertain with raw response as reasoning
            return CreateUncertainResult(
                validationId: validationId,
                confidence: 0.5,
                reasoning: $"AI response parsing failed: {aiResponse[..Math.Min(100, aiResponse.Length)]}",
                applicableRules: MapToRuleAtomDtos(applicableRules),
                conflicts: null,
                elapsedMs: elapsedMs);
        }
    }

    private MoveValidationResultDto CreateInvalidResult(
        Guid validationId,
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
            ValidationId = validationId,
            Decision = decision,
            Confidence = confidence,
            Reasoning = reasoning,
            ViolatedRules = violatedRules,
            Suggestions = suggestions,
            ApplicableRules = applicableRules,
            TokenUsage = TokenUsageDto.Empty,
            CostBreakdown = CostBreakdownDto.Zero("Local"),
            LatencyMs = (int)elapsedMs,
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
            ConflictDetected = false,
            ConflictsResolved = null
        };
    }

    private MoveValidationResultDto CreateUncertainResult(
        Guid validationId,
        double confidence,
        string reasoning,
        List<ArbitroRuleAtomDto> applicableRules,
        List<ConflictResolutionDto>? conflicts,
        long elapsedMs)
    {
        return new MoveValidationResultDto
        {
            ValidationId = validationId,
            Decision = "UNCERTAIN",
            Confidence = confidence,
            Reasoning = reasoning,
            ViolatedRules = new List<string>(),
            Suggestions = null,
            ApplicableRules = applicableRules,
            TokenUsage = TokenUsageDto.Empty,
            CostBreakdown = CostBreakdownDto.Zero("Local"),
            LatencyMs = (int)elapsedMs,
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
            ConflictDetected = conflicts != null && conflicts.Count > 0,
            ConflictsResolved = conflicts
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

    /// <summary>
    /// Detects conflicts in applicable rules using heuristic analysis.
    /// Issue #3761: Conflict detection logic.
    /// </summary>
    private List<RuleConflict> DetectRuleConflicts(List<RuleAtom> rules)
    {
        var conflicts = new List<RuleConflict>();

        // Pattern 1: Prohibitive vs Permissive rules (contradiction)
        var prohibitiveRules = rules.Where(r =>
            r.text.Contains("cannot", StringComparison.OrdinalIgnoreCase) ||
            r.text.Contains("must not", StringComparison.OrdinalIgnoreCase) ||
            r.text.Contains("forbidden", StringComparison.OrdinalIgnoreCase) ||
            r.text.Contains("illegal", StringComparison.OrdinalIgnoreCase)).ToList();

        var permissiveRules = rules.Where(r =>
            r.text.Contains("may", StringComparison.OrdinalIgnoreCase) ||
            r.text.Contains("can", StringComparison.OrdinalIgnoreCase) ||
            r.text.Contains("allowed", StringComparison.OrdinalIgnoreCase)).ToList();

        if (prohibitiveRules.Count > 0 && permissiveRules.Count > 0)
        {
            conflicts.Add(new RuleConflict(
                type: ConflictType.Contradiction,
                pattern: "prohibitive_vs_permissive",
                conflictingRuleIds: prohibitiveRules.Concat(permissiveRules).Select(r => r.id).ToList(),
                description: $"Found {prohibitiveRules.Count} prohibitive and {permissiveRules.Count} permissive rules"
            ));
        }

        // Pattern 2: Exception clauses (ambiguity)
        var exceptionRules = rules.Where(r =>
            r.text.Contains("except", StringComparison.OrdinalIgnoreCase) ||
            r.text.Contains("unless", StringComparison.OrdinalIgnoreCase) ||
            r.text.Contains("but", StringComparison.OrdinalIgnoreCase) ||
            r.text.Contains("however", StringComparison.OrdinalIgnoreCase)).ToList();

        if (exceptionRules.Count > 0)
        {
            conflicts.Add(new RuleConflict(
                type: ConflictType.ExceptionClause,
                pattern: "exception_clauses",
                conflictingRuleIds: exceptionRules.Select(r => r.id).ToList(),
                description: $"Rules contain {exceptionRules.Count} exception clauses requiring interpretation"
            ));
        }

        return conflicts;
    }

    /// <summary>
    /// Assembles conflict-aware prompt for LLM when conflicts are detected.
    /// Issue #3761: Conflict resolution prompting.
    /// </summary>
    private string AssembleConflictResolutionPrompt(
        string gameState,
        List<RuleAtom> applicableRules,
        List<RuleConflict> conflicts,
        Move move,
        GameSession session)
    {
        var rulesText = string.Join("\n", applicableRules.Select((r, i) =>
            $"[{i + 1}] {r.id} ({r.section ?? "General"}): {r.text}"));

        var conflictsText = string.Join("\n", conflicts.Select(c =>
            $"- {c.Type}: {c.Description} (Rules: {string.Join(", ", c.ConflictingRuleIds)})"));

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

        CONFLICTS DETECTED ({conflicts.Count}):
        {conflictsText}

        Move to Validate:
        - Player: {move.PlayerName}
        - Action: {move.Action}
        - Position: {move.Position ?? "N/A"}
        - Additional Context: {additionalContextText}

        IMPORTANT: Rules conflict. Use careful reasoning to resolve ambiguity.
        If confidence < 0.60, recommend human arbitrator review.

        Analyze and output JSON validation result.
        """;
    }

    /// <summary>
    /// Creates result when FAQ resolution is found (fast path).
    /// Issue #3761: FAQ-based resolution.
    /// </summary>
    private MoveValidationResultDto CreateFaqResolvedResult(
        Guid validationId,
        RuleConflictFAQ faq,
        List<RuleConflict> conflicts,
        List<RuleAtom> applicableRules,
        long elapsedMs)
    {
        var conflictDtos = conflicts.Select(c => new ConflictResolutionDto
        {
            ConflictType = c.Type.ToString(),
            Pattern = c.Pattern,
            ConflictingRuleIds = c.ConflictingRuleIds.ToList(),
            Description = c.Description,
            ResolutionStrategy = "FAQ",
            ResolvedBy = $"FAQ-{faq.Id}"
        }).ToList();

        // Parse FAQ resolution as decision
        var decision = faq.Resolution.Contains("valid", StringComparison.OrdinalIgnoreCase) ? "VALID" : "UNCERTAIN";

        return new MoveValidationResultDto
        {
            ValidationId = validationId,
            Decision = decision,
            Confidence = 0.95, // High confidence for FAQ resolutions
            Reasoning = faq.Resolution[..Math.Min(200, faq.Resolution.Length)],
            ViolatedRules = new List<string>(),
            Suggestions = null,
            ApplicableRules = MapToRuleAtomDtos(applicableRules),
            TokenUsage = TokenUsageDto.Empty, // No LLM call for FAQ
            CostBreakdown = CostBreakdownDto.Zero("FAQ"),
            LatencyMs = (int)elapsedMs,
            Timestamp = _timeProvider.GetUtcNow().UtcDateTime,
            ConflictDetected = true,
            ConflictsResolved = conflictDtos
        };
    }
}
