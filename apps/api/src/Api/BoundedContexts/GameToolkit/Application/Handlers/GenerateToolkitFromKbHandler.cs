using Api.BoundedContexts.GameToolkit.Application.Commands;
using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameToolkit.Application.Handlers;

/// <summary>
/// Generates an AI-powered toolkit suggestion by querying KB vector chunks
/// for game rules and asking the LLM to extract mechanical components.
/// Returns the suggestion DTO (not persisted — caller reviews and applies).
/// </summary>
internal class GenerateToolkitFromKbHandler
    : IRequestHandler<GenerateToolkitFromKbCommand, AiToolkitSuggestionDto>
{
    private readonly ILlmService _llmService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<GenerateToolkitFromKbHandler> _logger;

    private const int SearchLimit = 30;

    public GenerateToolkitFromKbHandler(
        ILlmService llmService,
        IEmbeddingService embeddingService,
        IQdrantService qdrantService,
        ILogger<GenerateToolkitFromKbHandler> logger)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<AiToolkitSuggestionDto> Handle(
        GenerateToolkitFromKbCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // 1. Generate embedding for a broad game rules query
        var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(
            "game rules components resources scoring turns dice cards timer setup",
            cancellationToken).ConfigureAwait(false);

        if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
        {
            _logger.LogWarning(
                "Embedding generation failed for toolkit AI generation, game {GameId}",
                request.GameId);
            return CreateDefaultSuggestion("Embedding generation failed. Provided generic defaults.");
        }

        // 2. Search Qdrant for game rule chunks
        var searchResult = await _qdrantService.SearchAsync(
            request.GameId.ToString(),
            embeddingResult.Embeddings[0],
            SearchLimit,
            documentIds: null,
            cancellationToken).ConfigureAwait(false);

        if (!searchResult.Success || searchResult.Results.Count == 0)
        {
            _logger.LogWarning(
                "No KB chunks found for game {GameId}, returning minimal suggestion",
                request.GameId);
            return CreateDefaultSuggestion("No game rules found in knowledge base. Provided generic defaults.");
        }

        // 3. Build context from chunks
        var rulesContext = string.Join("\n\n---\n\n",
            searchResult.Results.Select(c => c.Text));

        // 4. System prompt instructing LLM to analyze game rules
        const string systemPrompt = """
            You are a board game toolkit configurator. Given game rules text, extract the
            mechanical components needed for a digital game session toolkit.

            Analyze the rules and return a JSON object with these fields:
            - toolkitName: A short name for the toolkit (e.g., "Catan Toolkit")
            - diceTools: Array of dice configs. Each has: name, diceType (D4|D6|D8|D10|D12|D20|Custom),
              quantity (int), customFaces (string[] if Custom, null otherwise), isInteractive (bool),
              color (hex string or null)
            - counterTools: Array of resource counters. Each has: name, minValue (int), maxValue (int),
              defaultValue (int), isPerPlayer (true if each player tracks independently),
              icon (emoji string or null), color (hex string or null)
            - timerTools: Array of timers. Each has: name, durationSeconds (int),
              timerType (CountDown|CountUp|Chess), autoStart (bool), color (hex string or null),
              isPerPlayer (bool), warningThresholdSeconds (int or null)
            - scoringTemplate: { dimensions: string[] (scoring categories), defaultUnit: string,
              scoreType: "Points" or "Ranking" } or null
            - turnTemplate: { turnOrderType: "RoundRobin" or "Custom" or "Free",
              phases: string[] (phase names, empty array if none) } or null
            - overrides: { overridesTurnOrder: bool, overridesScoreboard: bool, overridesDiceSet: bool }
              Set to true if you provide a corresponding custom tool/template
            - reasoning: Brief explanation of why you chose these components

            Rules:
            - Only include components explicitly mentioned or strongly implied by the rules
            - If the game uses standard dice, include them. If not, omit diceTools.
            - For resources, identify all trackable quantities (money, wood, ore, cards, etc.)
            - For scoring, identify all ways to earn points
            - Keep names short and descriptive
            - Return ONLY valid JSON, no markdown
            """;

        var userPrompt = $"""
            Analyze these game rules and generate the toolkit configuration:

            {rulesContext}
            """;

        // 5. Call LLM for structured JSON
        var suggestion = await _llmService.GenerateJsonAsync<AiToolkitSuggestionDto>(
            systemPrompt,
            userPrompt,
            RequestSource.AgentTask,
            cancellationToken).ConfigureAwait(false);

        if (suggestion is null)
        {
            _logger.LogWarning("LLM returned null for toolkit generation, game {GameId}", request.GameId);
            return CreateDefaultSuggestion("AI generation failed. Provided generic defaults.");
        }

        _logger.LogInformation(
            "Generated toolkit suggestion for game {GameId}: {Name} with {Dice} dice, {Counters} counters, {Timers} timers",
            request.GameId,
            suggestion.ToolkitName,
            suggestion.DiceTools.Count,
            suggestion.CounterTools.Count,
            suggestion.TimerTools.Count);

        return suggestion;
    }

    private static AiToolkitSuggestionDto CreateDefaultSuggestion(string reasoning) => new(
        ToolkitName: "Game Toolkit",
        DiceTools: [new("Standard Dice", DiceType.D6, 2, null, false, null)],
        CounterTools: [],
        TimerTools: [],
        ScoringTemplate: new(["Points"], "points", ScoreType.Points),
        TurnTemplate: new(TurnOrderType.RoundRobin, []),
        Overrides: new(false, false, false),
        Reasoning: reasoning
    );
}
