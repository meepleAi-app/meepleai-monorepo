using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// LLM-based implementation of rulebook analyzer.
/// Uses AI to extract structured information from board game rulebooks.
/// </summary>
internal sealed class LlmRulebookAnalyzer : IRulebookAnalyzer
{
    private readonly ILlmService _llmService;
    private readonly ILogger<LlmRulebookAnalyzer> _logger;

    private const string AnalysisSystemPrompt = """
        You are an expert board game analyst specializing in rulebook analysis and game design.
        Your task is to analyze board game rulebooks and extract structured information.

        Requirements:
        - Extract accurate information from the provided rulebook
        - Identify key game mechanics (e.g., "Worker Placement", "Deck Building", "Area Control")
        - Summarize victory conditions clearly
        - List all game resources and their uses
        - Identify game phases in sequential order
        - Generate common questions players might have
        - Provide confidence scores for your analysis

        Return a JSON object with this exact structure:
        {
            "gameTitle": "Catan",
            "summary": "A competitive resource management game where players build settlements...",
            "keyMechanics": ["Resource Management", "Trading", "Dice Rolling"],
            "victoryConditions": {
                "primary": "First player to reach 10 victory points wins",
                "alternatives": ["Longest road bonus", "Largest army bonus"],
                "isPointBased": true,
                "targetPoints": 10
            },
            "resources": [
                {
                    "name": "Wood",
                    "type": "Building Material",
                    "usage": "Used to build roads and settlements",
                    "isLimited": true
                }
            ],
            "gamePhases": [
                {
                    "name": "Roll Phase",
                    "description": "Roll dice to determine resource production",
                    "order": 1,
                    "isOptional": false
                }
            ],
            "commonQuestions": [
                "Can I trade with any player?",
                "What happens when resources run out?"
            ],
            "confidenceScore": 0.92
        }

        Confidence scoring:
        - 0.9-1.0: Very high confidence, clear information in rulebook
        - 0.7-0.9: Good confidence, most information found
        - 0.5-0.7: Moderate confidence, some ambiguity
        - 0.0-0.5: Low confidence, missing or unclear information
        """;

    public LlmRulebookAnalyzer(ILlmService llmService, ILogger<LlmRulebookAnalyzer> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<RulebookAnalysisResult> AnalyzeAsync(
        string gameName,
        string rulebookContent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Analyzing rulebook for game: {GameName}", gameName);

        // Truncate content if too long (LLM context limits - keep first 15k chars)
        var truncatedContent = rulebookContent.Length > 15000
            ? string.Concat(rulebookContent.AsSpan(0, 15000), "\n\n[Content truncated...]")
            : rulebookContent;

        var userPrompt = $"""
            Game: {gameName}

            Rulebook Content:
            {truncatedContent}

            Please analyze this rulebook and extract all structured information about "{gameName}".
            Focus on accuracy and completeness. If any information is unclear, note it in your confidence score.
            """;

        try
        {
            var result = await _llmService.GenerateJsonAsync<LlmAnalysisResponse>(
                AnalysisSystemPrompt,
                userPrompt,
                RequestSource.RagPipeline,
                cancellationToken).ConfigureAwait(false);

            if (result is null)
            {
                _logger.LogWarning("LLM returned null analysis, using fallback");
                return CreateFallbackAnalysis(gameName);
            }

            // Convert LLM response to domain objects
            var victoryConditions = result.VictoryConditions is not null
                ? VictoryConditions.Create(
                    result.VictoryConditions.Primary,
                    result.VictoryConditions.Alternatives,
                    result.VictoryConditions.IsPointBased,
                    result.VictoryConditions.TargetPoints)
                : VictoryConditions.Empty;

            var resources = result.Resources
                .Select(r => Resource.Create(r.Name, r.Type, r.Usage, r.IsLimited))
                .ToList();

            var gamePhases = result.GamePhases
                .Select(p => GamePhase.Create(p.Name, p.Description, p.Order, p.IsOptional))
                .ToList();

            _logger.LogInformation(
                "Analysis complete for {GameName} with confidence {Confidence}",
                gameName,
                result.ConfidenceScore);

            return new RulebookAnalysisResult(
                GameTitle: result.GameTitle,
                Summary: result.Summary,
                KeyMechanics: result.KeyMechanics,
                VictoryConditions: victoryConditions,
                Resources: resources,
                GamePhases: gamePhases,
                CommonQuestions: result.CommonQuestions,
                ConfidenceScore: result.ConfidenceScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error analyzing rulebook for {GameName}, using fallback", gameName);
            return CreateFallbackAnalysis(gameName);
        }
    }

    public async Task<GameStateSchemaResult> ExtractStateSchemaAsync(
        string gameName,
        string rulebookContent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting game state schema for: {GameName}", gameName);

        var systemPrompt = """
            You are an expert board game analyst. Extract the game state structure from the rulebook.
            Return a JSON schema that defines what state needs to be tracked during gameplay.

            Example output:
            {
                "schemaJson": "{\"type\":\"object\",\"properties\":{\"playerResources\":{\"type\":\"object\"},\"boardState\":{\"type\":\"object\"}}}",
                "confidenceScore": 0.85
            }
            """;

        var truncatedContent = rulebookContent.Length > 15000
            ? string.Concat(rulebookContent.AsSpan(0, 15000), "\n\n[Content truncated...]")
            : rulebookContent;

        var userPrompt = $"""
            Game: {gameName}
            Rulebook: {truncatedContent}

            Extract the game state schema.
            """;

        try
        {
            var result = await _llmService.GenerateJsonAsync<LlmSchemaResponse>(
                systemPrompt,
                userPrompt,
                RequestSource.RagPipeline,
                cancellationToken).ConfigureAwait(false);

            if (result is null || string.IsNullOrWhiteSpace(result.SchemaJson))
            {
                _logger.LogWarning("LLM returned invalid schema, using fallback");
                return new GameStateSchemaResult("{}", 0.5m, new List<string>());
            }

            return new GameStateSchemaResult(result.SchemaJson, result.ConfidenceScore, new List<string>());
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting schema for {GameName}", gameName);
            return new GameStateSchemaResult("{}", 0.5m, new List<string>());
        }
    }

    public async Task<RulebookQuestionsResult> GenerateQuestionsAsync(
        string gameName,
        string rulebookContent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating questions for: {GameName}", gameName);

        var systemPrompt = """
            You are an expert board game teacher. Generate specific questions with answers from the rulebook.

            Return JSON:
            {
                "questions": [
                    {
                        "text": "How do I win?",
                        "answer": "First player to 10 victory points wins",
                        "pageReferences": [5, 12],
                        "confidence": 0.95
                    }
                ],
                "confidenceScore": 0.88
            }
            """;

        var truncatedContent = rulebookContent.Length > 15000
            ? string.Concat(rulebookContent.AsSpan(0, 15000), "\n\n[Content truncated...]")
            : rulebookContent;

        var userPrompt = $"""
            Game: {gameName}
            Rulebook: {truncatedContent}

            Generate 5-8 specific questions with answers.
            """;

        try
        {
            var result = await _llmService.GenerateJsonAsync<LlmQuestionsResponse>(
                systemPrompt,
                userPrompt,
                RequestSource.RagPipeline,
                cancellationToken).ConfigureAwait(false);

            if (result?.Questions is null || result.Questions.Count == 0)
            {
                _logger.LogWarning("LLM returned no questions, using fallback");
                return CreateFallbackQuestions(gameName);
            }

            var questions = result.Questions
                .Select(q => new GeneratedRulebookQuestion(
                    q.Text,
                    q.Answer,
                    q.PageReferences,
                    q.Confidence))
                .ToList();

            return new RulebookQuestionsResult(questions, result.ConfidenceScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating questions for {GameName}", gameName);
            return CreateFallbackQuestions(gameName);
        }
    }

    public async Task<KeyConceptsResult> ExtractKeyConceptsAsync(
        string gameName,
        string rulebookContent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Extracting key concepts for: {GameName}", gameName);

        var systemPrompt = """
            You are an expert board game glossary creator. Extract key terms and concepts from the rulebook.

            Return JSON:
            {
                "concepts": [
                    {
                        "term": "Victory Point",
                        "definition": "Points accumulated to win the game",
                        "category": "Rule"
                    }
                ],
                "confidenceScore": 0.90
            }

            Categories: Mechanic, Component, Rule, Action, Condition
            """;

        var truncatedContent = rulebookContent.Length > 15000
            ? string.Concat(rulebookContent.AsSpan(0, 15000), "\n\n[Content truncated...]")
            : rulebookContent;

        var userPrompt = $"""
            Game: {gameName}
            Rulebook: {truncatedContent}

            Extract 10-15 key concepts with definitions.
            """;

        try
        {
            var result = await _llmService.GenerateJsonAsync<LlmConceptsResponse>(
                systemPrompt,
                userPrompt,
                RequestSource.RagPipeline,
                cancellationToken).ConfigureAwait(false);

            if (result?.Concepts is null || result.Concepts.Count == 0)
            {
                _logger.LogWarning("LLM returned no concepts, using fallback");
                return CreateFallbackConcepts(gameName);
            }

            var concepts = result.Concepts
                .Select(c => new KeyConcept(c.Term, c.Definition, c.Category))
                .ToList();

            return new KeyConceptsResult(concepts, result.ConfidenceScore);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error extracting concepts for {GameName}", gameName);
            return CreateFallbackConcepts(gameName);
        }
    }

    // Fallback methods
    private static RulebookAnalysisResult CreateFallbackAnalysis(string gameName)
    {
        return new RulebookAnalysisResult(
            GameTitle: gameName,
            Summary: "Analysis unavailable. Please try again or provide more detailed rulebook content.",
            KeyMechanics: new List<string> { "Not analyzed" },
            VictoryConditions: VictoryConditions.Empty,
            Resources: new List<Resource>(),
            GamePhases: new List<GamePhase>(),
            CommonQuestions: new List<string> { "How do I play this game?" },
            ConfidenceScore: 0.1m);
    }

    private static RulebookQuestionsResult CreateFallbackQuestions(string gameName)
    {
        var questions = new List<GeneratedRulebookQuestion>
        {
            new("Come si gioca?", "Consultare il regolamento per i dettagli.", new List<int>(), 0.5m),
            new("Come si vince?", "Consultare la sezione vittoria nel regolamento.", new List<int>(), 0.5m),
        };

        return new RulebookQuestionsResult(questions, 0.5m);
    }

    private static KeyConceptsResult CreateFallbackConcepts(string gameName)
    {
        var concepts = new List<KeyConcept>
        {
            new(gameName, "Il gioco da analizzare", "Game"),
        };

        return new KeyConceptsResult(concepts, 0.5m);
    }

    // LLM response DTOs for deserialization
    private sealed record LlmAnalysisResponse(
        string GameTitle,
        string Summary,
        List<string> KeyMechanics,
        LlmVictoryConditions? VictoryConditions,
        List<LlmResource> Resources,
        List<LlmGamePhase> GamePhases,
        List<string> CommonQuestions,
        decimal ConfidenceScore
    );

    private sealed record LlmVictoryConditions(
        string Primary,
        List<string> Alternatives,
        bool IsPointBased,
        int? TargetPoints
    );

    private sealed record LlmResource(
        string Name,
        string Type,
        string? Usage,
        bool IsLimited
    );

    private sealed record LlmGamePhase(
        string Name,
        string Description,
        int Order,
        bool IsOptional
    );

    private sealed record LlmSchemaResponse(
        string SchemaJson,
        decimal ConfidenceScore
    );

    private sealed record LlmQuestionsResponse(
        List<LlmQuestion> Questions,
        decimal ConfidenceScore
    );

    private sealed record LlmQuestion(
        string Text,
        string Answer,
        List<int> PageReferences,
        decimal Confidence
    );

    private sealed record LlmConceptsResponse(
        List<LlmConcept> Concepts,
        decimal ConfidenceScore
    );

    private sealed record LlmConcept(
        string Term,
        string Definition,
        string Category
    );
}
