#pragma warning disable MA0002 // Dictionary without StringComparer
using System.Text.Json;
using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services.AgentModes;

/// <summary>
/// Player Mode handler that suggests optimal moves during gameplay.
/// Analyzes current game state and rules to provide strategic recommendations.
/// Issue #2404 - Player Mode move suggestions
/// </summary>
internal sealed class PlayerModeHandler : IAgentModeHandler
{
    private readonly IGameSessionStateRepository _sessionStateRepository;
    private readonly ILlmService _llmService;
    private readonly ILogger<PlayerModeHandler> _logger;

    public PlayerModeHandler(
        IGameSessionStateRepository sessionStateRepository,
        ILlmService llmService,
        ILogger<PlayerModeHandler> logger)
    {
        _sessionStateRepository = sessionStateRepository;
        _llmService = llmService;
        _logger = logger;
    }

    public AgentMode SupportedMode => AgentMode.Player;

    public async Task<AgentModeResult> HandleAsync(
        AgentModeContext context,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(context);

        // Validate Player mode requirements
        if (context.Configuration.SelectedDocumentIds.Count == 0)
        {
            throw new InvalidOperationException(
                "Player mode requires at least one rulebook document selected");
        }

        _logger.LogInformation(
            "PlayerModeHandler invoked for Agent {AgentId} with query: {Query}",
            context.Agent.Id,
            context.Query);

        // Get current game state if GameId provided
        string? gameStateJson = null;
        if (context.GameId.HasValue)
        {
            var sessionState = await _sessionStateRepository
                .GetBySessionIdAsync(context.GameId.Value, cancellationToken)
                .ConfigureAwait(false);

            if (sessionState != null)
            {
                gameStateJson = sessionState.GetStateAsString();
                _logger.LogDebug(
                    "Retrieved game state for session {SessionId}: {StateLength} chars",
                    context.GameId.Value,
                    gameStateJson.Length);
            }
        }

        // Generate move suggestions using LLM
        var suggestions = await GenerateMoveSuggestionsAsync(
            context,
            gameStateJson,
            cancellationToken)
            .ConfigureAwait(false);

        // Calculate overall confidence from search results
        var confidence = context.SearchResults.Any()
            ? context.SearchResults.Average(r => r.RelevanceScore.Value)
            : 0.5;

        // Format response content
        var content = FormatMoveSuggestions(suggestions);

        return new AgentModeResult
        {
            Mode = AgentMode.Player,
            Content = content,
            Confidence = confidence,
            Metadata = new Dictionary<string, object>
            {
                { "suggestionCount", suggestions.Count },
                { "hasGameState", gameStateJson != null },
                { "avgRiskLevel", CalculateAverageRisk(suggestions) }
            }
        };
    }

    /// <summary>
    /// Generates move suggestions using LLM with game state and rules context
    /// </summary>
    private async Task<List<MoveSuggestion>> GenerateMoveSuggestionsAsync(
        AgentModeContext context,
        string? gameStateJson,
        CancellationToken cancellationToken)
    {
        // Build system prompt for move suggestion
        var systemPrompt = BuildSystemPrompt(context.SearchResults, gameStateJson);

        // Build user prompt with query
        var userPrompt = $"""
            Player question: {context.Query}

            Provide 1-3 strategic move suggestions based on:
            1. Current game state (if available)
            2. Game rules and valid moves
            3. Strategic positioning and win conditions

            For each suggestion, explain:
            - What action to take
            - Why this move is strategically sound
            - Risk level (low/medium/high)
            - Expected outcome
            """;

        try
        {
            // Use GenerateJsonAsync for structured output
            var suggestionsResponse = await _llmService.GenerateJsonAsync<MoveSuggestionsResponse>(
                systemPrompt,
                userPrompt,
                cancellationToken)
                .ConfigureAwait(false);

            if (suggestionsResponse?.Suggestions == null || suggestionsResponse.Suggestions.Count == 0)
            {
                _logger.LogWarning("LLM returned no move suggestions, returning fallback");
                return CreateFallbackSuggestions();
            }

            // Convert to domain MoveSuggestion objects
            return suggestionsResponse.Suggestions
                .Select(s => MoveSuggestion.Create(
                    action: s.Action,
                    reasoning: s.Reasoning,
                    risk: ParseRiskLevel(s.Risk),
                    confidenceScore: s.ConfidenceScore,
                    stateChange: s.StateChange))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to generate move suggestions from LLM");
            return CreateFallbackSuggestions();
        }
    }

    /// <summary>
    /// Builds system prompt with rules context and game state
    /// </summary>
    private static string BuildSystemPrompt(
        IReadOnlyList<Api.BoundedContexts.KnowledgeBase.Domain.Entities.SearchResult> searchResults,
        string? gameStateJson)
    {
        var rulesContext = string.Join("\n\n", searchResults
            .OrderByDescending(r => r.RelevanceScore.Value)
            .Take(5)
            .Select((r, i) => $"[Rule {i + 1}] {r.TextContent}"));

        var systemPrompt = $"""
            You are an expert board game strategist helping players make optimal moves.

            Your role:
            - Analyze the current game state and rules
            - Suggest 1-3 strategic moves with clear reasoning
            - Consider risk vs reward for each suggestion
            - Explain expected outcomes

            Game Rules Context:
            {rulesContext}
            """;

        if (!string.IsNullOrWhiteSpace(gameStateJson))
        {
            systemPrompt += $"""


                Current Game State:
                {gameStateJson}
                """;
        }

        return systemPrompt;
    }

    /// <summary>
    /// Formats move suggestions for chat display
    /// </summary>
    private static string FormatMoveSuggestions(List<MoveSuggestion> suggestions)
    {
        if (suggestions.Count == 0)
        {
            return "🎮 **No specific move suggestions available**\n\nPlease provide more context about the current game state.";
        }

        var formattedSuggestions = suggestions
            .Select((s, i) => $"""
                {i + 1}. **{s.Action}** (Rischio: {GetRiskLevelText(s.Risk)})
                   → {s.Reasoning}
                """)
            .ToList();

        return $"""
            🎮 **Suggerimenti per il tuo turno:**

            {string.Join("\n\n", formattedSuggestions)}
            """;
    }

    /// <summary>
    /// Creates fallback suggestions when LLM fails
    /// </summary>
    private static List<MoveSuggestion> CreateFallbackSuggestions()
    {
        return new List<MoveSuggestion>
        {
            MoveSuggestion.Create(
                action: "Analizza le tue opzioni disponibili",
                reasoning: "Valuta tutte le mosse possibili prima di decidere",
                risk: RiskLevel.Low,
                confidenceScore: 0.6f)
        };
    }

    /// <summary>
    /// Parses risk level string to enum
    /// </summary>
    private static RiskLevel ParseRiskLevel(string risk)
    {
        return risk?.ToLowerInvariant() switch
        {
            "low" or "basso" => RiskLevel.Low,
            "medium" or "medio" => RiskLevel.Medium,
            "high" or "alto" => RiskLevel.High,
            _ => RiskLevel.Medium
        };
    }

    /// <summary>
    /// Gets Italian text for risk level
    /// </summary>
    private static string GetRiskLevelText(RiskLevel risk)
    {
        return risk switch
        {
            RiskLevel.Low => "Basso",
            RiskLevel.Medium => "Medio",
            RiskLevel.High => "Alto",
            _ => "Medio"
        };
    }

    /// <summary>
    /// Calculates average risk level across suggestions
    /// </summary>
    private static double CalculateAverageRisk(List<MoveSuggestion> suggestions)
    {
        if (suggestions.Count == 0) return 0.0;
        return suggestions.Average(s => (int)s.Risk) / 2.0; // Normalize to 0-1 range
    }
}

/// <summary>
/// Response model for LLM JSON output
/// </summary>
internal sealed record MoveSuggestionsResponse
{
    public List<MoveSuggestionDto> Suggestions { get; init; } = new();
}

/// <summary>
/// DTO for individual move suggestion from LLM
/// </summary>
internal sealed record MoveSuggestionDto
{
    public string Action { get; init; } = string.Empty;
    public string Reasoning { get; init; } = string.Empty;
    public string Risk { get; init; } = "medium";
    public float ConfidenceScore { get; init; } = 0.5f;
    public Dictionary<string, object> StateChange { get; init; } = new();
}
