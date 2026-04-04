using System.Text.Json;
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Models;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Helpers;
using Api.Models;
using Api.Services;
using MediatR;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands;

/// <summary>
/// Handler for SuggestPlayerMoveCommand.
/// Issue #2473: Production AI Implementation for Player Mode Move Suggestions.
///
/// Integrates RAG search and LLM-powered move generation for optimal player suggestions.
/// Pipeline: GameState → Parse → RAG Search → LLM Generation → Confidence Scoring → Cache
/// </summary>
internal sealed class SuggestPlayerMoveCommandHandler
    : IRequestHandler<SuggestPlayerMoveCommand, PlayerModeSuggestionResponse>
{
    private readonly IAiResponseCacheService _cache;
    private readonly ILogger<SuggestPlayerMoveCommandHandler> _logger;
    private readonly IGameStateParser _gameStateParser;
    private readonly IMediator _mediator;
    private readonly ILlmService _llmService;

    // Confidence scoring weights
    private const double RagConfidenceWeight = 0.4;
    private const double LlmConfidenceWeight = 0.4;
    private const double StateCompletenessWeight = 0.2;

    // System prompt template for LLM move generation
    private const string SystemPromptTemplate = """
        You are a board game AI assistant analyzing player moves.
        Based on rules and game state, suggest optimal moves with:
        - Primary suggestion (highest confidence)
        - 2-3 alternative moves
        - Strategic context
        - Confidence scores (0.0-1.0)

        Output MUST be valid JSON matching this schema:
        {
          "primarySuggestion": {
            "action": "Specific action description",
            "rationale": "Why this is optimal",
            "expectedOutcome": "What happens after",
            "confidence": 0.85
          },
          "alternatives": [
            {
              "action": "Alternative action",
              "rationale": "Why this is viable",
              "expectedOutcome": "Expected result",
              "confidence": 0.72
            }
          ],
          "strategicContext": "High-level strategic advice"
        }
        """;

    public SuggestPlayerMoveCommandHandler(
        IAiResponseCacheService cache,
        ILogger<SuggestPlayerMoveCommandHandler> logger,
        IGameStateParser gameStateParser,
        IMediator mediator,
        ILlmService llmService)
    {
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _gameStateParser = gameStateParser ?? throw new ArgumentNullException(nameof(gameStateParser));
        _mediator = mediator ?? throw new ArgumentNullException(nameof(mediator));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
    }

    public async Task<PlayerModeSuggestionResponse> Handle(
        SuggestPlayerMoveCommand request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // Validate query if provided
        if (!string.IsNullOrWhiteSpace(request.Query))
        {
            var queryError = QueryValidator.ValidateQuery(request.Query);
            if (queryError != null)
            {
                return CreateErrorResponse(queryError);
            }
        }

        try
        {
            // Check cache first
            var cacheKey = GenerateCacheKey(request.GameId, request.GameState, request.Query);
            var cachedResponse = await _cache.GetAsync<PlayerModeSuggestionResponse>(
                cacheKey,
                cancellationToken).ConfigureAwait(false);

            if (cachedResponse != null)
            {
                _logger.LogInformation("Returning cached player move suggestion for game {GameId}", request.GameId);
                return cachedResponse;
            }

            var startTime = DateTime.UtcNow;

            // Step 1: Parse game state
            var parsedState = _gameStateParser.Parse(request.GameState);
            if (parsedState == null)
            {
                _logger.LogWarning("Failed to parse game state for game {GameId}", request.GameId);
                return CreateFallbackResponse("Unable to parse game state. Please provide player information.");
            }

            // Step 2: RAG search for strategic rules and context
            var (ragSnippets, ragConfidence) = await PerformRagSearchAsync(
                request.GameId,
                request.Query,
                parsedState,
                cancellationToken
            ).ConfigureAwait(false);

            // Step 3: Generate LLM-powered move suggestions
            var (llmOutput, llmConfidence, promptTokens, completionTokens) = await GenerateMoveSuggestionsAsync(
                request.Query,
                parsedState,
                ragSnippets,
                cancellationToken
            ).ConfigureAwait(false);

            // Step 4: Calculate overall confidence
            var overallConfidence = CalculateOverallConfidence(
                ragConfidence,
                llmConfidence,
                parsedState.CompletenessScore
            );

            // Step 5: Map to response model
            var processingTimeMs = (int)(DateTime.UtcNow - startTime).TotalMilliseconds;
            var response = MapToResponse(
                llmOutput,
                ragSnippets,
                overallConfidence,
                promptTokens,
                completionTokens,
                processingTimeMs
            );

            // Cache the response
            await _cache.SetAsync(cacheKey, response, 3600, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Generated player move suggestion for game {GameId} with confidence {Confidence:F2} in {ProcessingTimeMs}ms",
                request.GameId,
                overallConfidence,
                processingTimeMs
            );

            return response;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Error during player move suggestion for game {GameId}", request.GameId);
            return CreateFallbackResponse("An error occurred while analyzing your game state. Please try again.");
        }
    }

    /// <summary>
    /// Generates collision-resistant cache key for player move suggestion.
    /// Uses SHA256 to prevent hash collisions from different game states or queries.
    /// </summary>
    private static string GenerateCacheKey(
        string gameId,
        IReadOnlyDictionary<string, object> gameState,
        string? query)
    {
        // Serialize game state to JSON for consistent cache key
        var stateJson = JsonSerializer.Serialize(gameState);
        var queryPart = query ?? "no-query";
        var combined = $"{gameId}|{stateJson}|{queryPart}";

        // Use SHA256 for collision-resistant hashing (CA1850: prefer static HashData)
        var hashBytes = System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes(combined));
        var hashString = Convert.ToHexString(hashBytes);

        return $"player-move:{gameId}:{hashString}";
    }

    /// <summary>
    /// Performs RAG search for strategic rules and game context.
    /// Returns relevant snippets and search confidence score.
    /// </summary>
    private async Task<(List<Snippet> snippets, double confidence)> PerformRagSearchAsync(
        string gameId,
        string? query,
        ParsedGameState parsedState,
        CancellationToken cancellationToken)
    {
        try
        {
            // Build search query combining user query + game state context
            var searchQuery = BuildRagSearchQuery(query, parsedState);

            var searchCommand = new SearchQuery(
                GameId: Guid.Parse(gameId),
                Query: searchQuery,
                TopK: 10,
                MinScore: 0.55,
                SearchMode: "hybrid",
                Language: "en",
                DocumentIds: null
            );

            var searchResults = await _mediator.Send(searchCommand, cancellationToken).ConfigureAwait(false);

            if (searchResults == null || searchResults.Count == 0)
            {
                _logger.LogWarning("RAG search returned no results for game {GameId}", gameId);
                return (new List<Snippet>(), 0.0);
            }

            // Map to Snippet format and calculate confidence
            var snippets = searchResults
                .Take(10)
                .Select(r => new Snippet(
                    text: r.TextContent,
                    source: r.VectorDocumentId,
                    page: r.PageNumber,
                    line: 0,
                    score: (float)r.RelevanceScore
                ))
                .ToList();

            // Calculate search confidence as weighted average of top results
            var confidence = CalculateSearchConfidence(searchResults);

            _logger.LogInformation(
                "RAG search completed: {SnippetCount} snippets, confidence={Confidence:F2}",
                snippets.Count,
                confidence
            );

            return (snippets, confidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "RAG search failed for game {GameId}", gameId);
            return (new List<Snippet>(), 0.0);
        }
    }

    /// <summary>
    /// Builds RAG search query from user query and game state.
    /// </summary>
    private static string BuildRagSearchQuery(string? query, ParsedGameState parsedState)
    {
        var queryParts = new List<string>();

        if (!string.IsNullOrWhiteSpace(query))
        {
            queryParts.Add($"Player query: {query}");
        }

        queryParts.Add($"Game state: {parsedState.ToSummary()}");
        queryParts.Add("What are the strategic options and recommended moves?");

        return string.Join("\n", queryParts);
    }

    /// <summary>
    /// Calculates search confidence from result relevance scores.
    /// Weighted average of top 5 results by rank.
    /// </summary>
    private static double CalculateSearchConfidence(List<SearchResultDto> results)
    {
        if (results.Count == 0)
        {
            return 0.0;
        }

        var top5 = results.Take(5).ToList();
        var weightedSum = 0.0;
        var totalWeight = 0.0;

        foreach (var result in top5)
        {
            var weight = 1.0 / result.Rank;
            weightedSum += result.RelevanceScore * weight;
            totalWeight += weight;
        }

        return totalWeight > 0 ? weightedSum / totalWeight : 0.0;
    }

    /// <summary>
    /// Generates move suggestions using LLM with structured JSON output.
    /// Returns LLM output, confidence score, and token counts.
    /// </summary>
    private async Task<(LlmMoveGenerationOutput output, double confidence, int promptTokens, int completionTokens)>
        GenerateMoveSuggestionsAsync(
            string? query,
            ParsedGameState parsedState,
            List<Snippet> ragSnippets,
            CancellationToken cancellationToken)
    {
        try
        {
            var systemPrompt = SystemPromptTemplate;
            var userPrompt = BuildUserPrompt(query, parsedState, ragSnippets);

            var llmOutput = await _llmService.GenerateJsonAsync<LlmMoveGenerationOutput>(
                systemPrompt,
                userPrompt,
                RequestSource.Manual,
                cancellationToken
            ).ConfigureAwait(false);

            if (llmOutput == null)
            {
                _logger.LogWarning("LLM returned null output, using fallback");
                return (CreateFallbackLlmOutput(), 0.5, 0, 0);
            }

            // Estimate token count (rough approximation: 1 token ≈ 4 chars)
            var promptTokens = (systemPrompt.Length + userPrompt.Length) / 4;
            var completionTokens = EstimateTokensFromLlmOutput(llmOutput);

            // Calculate LLM confidence based on primary suggestion confidence
            var llmConfidence = llmOutput.PrimarySuggestion.Confidence;

            _logger.LogInformation(
                "LLM generation completed: primary confidence={Confidence:F2}, tokens={PromptTokens}/{CompletionTokens}",
                llmConfidence,
                promptTokens,
                completionTokens
            );

            return (llmOutput, llmConfidence, promptTokens, completionTokens);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "LLM generation failed");
            return (CreateFallbackLlmOutput(), 0.5, 0, 0);
        }
    }

    /// <summary>
    /// Builds user prompt with game context and RAG snippets.
    /// </summary>
    private static string BuildUserPrompt(
        string? query,
        ParsedGameState parsedState,
        List<Snippet> ragSnippets)
    {
        var promptParts = new List<string>();

        // Add RAG context
        if (ragSnippets.Count > 0)
        {
            promptParts.Add("CONTEXT FROM RULES:");
            foreach (var snippet in ragSnippets.Take(5))
            {
                promptParts.Add($"- {snippet.text}");
            }
            promptParts.Add(string.Empty);
        }

        // Add game state
        promptParts.Add("GAME STATE:");
        promptParts.Add(parsedState.ToSummary());
        promptParts.Add(string.Empty);

        // Add user query
        if (!string.IsNullOrWhiteSpace(query))
        {
            promptParts.Add("USER QUERY:");
            promptParts.Add(query);
        }
        else
        {
            promptParts.Add("Suggest optimal moves based on current game state.");
        }

        return string.Join("\n", promptParts);
    }

    /// <summary>
    /// Calculates overall confidence from RAG, LLM, and state completeness.
    /// Formula: RAG * 0.4 + LLM * 0.4 + State * 0.2
    /// </summary>
    private static double CalculateOverallConfidence(
        double ragConfidence,
        double llmConfidence,
        double stateCompleteness)
    {
        var confidence = (ragConfidence * RagConfidenceWeight)
                       + (llmConfidence * LlmConfidenceWeight)
                       + (stateCompleteness * StateCompletenessWeight);

        return Math.Clamp(confidence, 0.0, 1.0);
    }

    /// <summary>
    /// Maps LLM output to PlayerModeSuggestionResponse.
    /// </summary>
    private static PlayerModeSuggestionResponse MapToResponse(
        LlmMoveGenerationOutput llmOutput,
        List<Snippet> snippets,
        double overallConfidence,
        int promptTokens,
        int completionTokens,
        int processingTimeMs)
    {
        var primary = new SuggestedMove(
            action: llmOutput.PrimarySuggestion.Action,
            rationale: llmOutput.PrimarySuggestion.Rationale,
            expectedOutcome: llmOutput.PrimarySuggestion.ExpectedOutcome,
            confidence: llmOutput.PrimarySuggestion.Confidence
        );

        var alternatives = llmOutput.Alternatives
            .Select(alt => new SuggestedMove(
                action: alt.Action,
                rationale: alt.Rationale,
                expectedOutcome: alt.ExpectedOutcome,
                confidence: alt.Confidence
            ))
            .ToList();

        return new PlayerModeSuggestionResponse(
            primarySuggestion: primary,
            alternativeMoves: alternatives,
            overallConfidence: overallConfidence,
            strategicContext: llmOutput.StrategicContext,
            sources: snippets,
            promptTokens: promptTokens,
            completionTokens: completionTokens,
            totalTokens: promptTokens + completionTokens,
            processingTimeMs: processingTimeMs,
            metadata: new Dictionary<string, object>(StringComparer.Ordinal)
            {
                { "version", "2.0-production" },
                { "implementation", "rag+llm" }
            }
        );
    }

    /// <summary>
    /// Creates fallback LLM output when generation fails.
    /// </summary>
    private static LlmMoveGenerationOutput CreateFallbackLlmOutput()
    {
        return new LlmMoveGenerationOutput
        {
            PrimarySuggestion = new PrimarySuggestion
            {
                Action = "Analyze current position and resources",
                Rationale = "Unable to generate specific suggestion due to processing error",
                ExpectedOutcome = "Review game state manually for optimal move",
                Confidence = 0.5
            },
            Alternatives = new List<AlternativeMove>(),
            StrategicContext = "Consider available resources and current game objectives."
        };
    }

    /// <summary>
    /// Creates fallback response for error scenarios.
    /// </summary>
    private static PlayerModeSuggestionResponse CreateFallbackResponse(string errorMessage)
    {
        return new PlayerModeSuggestionResponse(
            primarySuggestion: new SuggestedMove(
                action: "No suggestion available",
                rationale: errorMessage,
                expectedOutcome: null,
                confidence: 0.0
            ),
            alternativeMoves: new List<SuggestedMove>(),
            overallConfidence: 0.0,
            strategicContext: null,
            sources: new List<Snippet>(),
            promptTokens: 0,
            completionTokens: 0,
            totalTokens: 0,
            processingTimeMs: 0,
            metadata: new Dictionary<string, object>(StringComparer.Ordinal) { { "error", errorMessage } }
        );
    }

    /// <summary>
    /// Estimates token count from LLM output (rough approximation).
    /// </summary>
    private static int EstimateTokensFromLlmOutput(LlmMoveGenerationOutput output)
    {
        var sb = new System.Text.StringBuilder();
        sb.Append(output.PrimarySuggestion.Action)
          .Append(' ')
          .Append(output.PrimarySuggestion.Rationale)
          .Append(' ')
          .Append(output.PrimarySuggestion.ExpectedOutcome)
          .Append(' ')
          .Append(output.StrategicContext);

        foreach (var alt in output.Alternatives)
        {
            sb.Append(' ')
              .Append(alt.Action)
              .Append(' ')
              .Append(alt.Rationale)
              .Append(' ')
              .Append(alt.ExpectedOutcome);
        }

        return sb.Length / 4; // Rough approximation: 1 token ≈ 4 chars
    }

    /// <summary>
    /// Creates error response when processing fails.
    /// </summary>
    private static PlayerModeSuggestionResponse CreateErrorResponse(string errorMessage)
    {
        return new PlayerModeSuggestionResponse(
            primarySuggestion: new SuggestedMove(
                action: "No suggestion available",
                rationale: errorMessage,
                expectedOutcome: null,
                confidence: 0.0
            ),
            alternativeMoves: new List<SuggestedMove>(),
            overallConfidence: 0.0,
            strategicContext: null,
            sources: new List<Snippet>(),
            promptTokens: 0,
            completionTokens: 0,
            totalTokens: 0,
            processingTimeMs: 0,
            metadata: new Dictionary<string, object>(StringComparer.Ordinal) { { "error", errorMessage } }
        );
    }
}
