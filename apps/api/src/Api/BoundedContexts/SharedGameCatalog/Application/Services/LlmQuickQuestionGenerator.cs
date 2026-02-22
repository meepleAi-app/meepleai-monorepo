using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// LLM-based implementation of quick question generator.
/// Uses AI to analyze rulebook content and generate game-specific questions.
/// </summary>
internal sealed class LlmQuickQuestionGenerator : IQuickQuestionGenerator
{
    private readonly ILlmService _llmService;
    private readonly ILogger<LlmQuickQuestionGenerator> _logger;

    private const string SystemPrompt = """
        You are an expert board game teacher and rules analyst. Your task is to analyze board game rulebooks
        and generate 5-8 relevant, game-specific quick questions that players might ask when learning the game.

        Requirements:
        - Generate questions that are SPECIFIC to this game, not generic
        - Cover different aspects: gameplay, rules, winning, setup, strategy, clarifications
        - Keep questions concise (under 150 characters)
        - Prioritize questions beginners would ask
        - Avoid yes/no questions; prefer "how", "what", "when" questions
        - Each question should have a clear category

        Categories:
        - Gameplay: Questions about core gameplay mechanics and turn structure
        - Rules: Questions about specific rules and mechanics
        - Winning: Questions about winning conditions and scoring
        - Setup: Questions about game preparation
        - Strategy: Questions about tactics and strategies
        - Clarifications: Questions about edge cases and specific situations

        Return a JSON object with this exact structure:
        {
            "questions": [
                {
                    "text": "Come funziona il sistema di combattimento?",
                    "category": "Rules",
                    "confidence": 0.92
                },
                ...
            ],
            "overallConfidence": 0.88
        }

        Confidence scores:
        - Per-question: How confident you are this question is relevant (0-1)
        - Overall: How confident you are in the entire set of questions (0-1)
        """;

    public LlmQuickQuestionGenerator(ILlmService llmService, ILogger<LlmQuickQuestionGenerator> logger)
    {
        _llmService = llmService;
        _logger = logger;
    }

    public async Task<QuickQuestionGenerationResult> GenerateQuestionsAsync(
        string gameName,
        string rulebookContent,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating quick questions for game: {GameName}", gameName);

        // Truncate content if too long (LLM context limits - keep first 15k chars)
        var truncatedContent = rulebookContent.Length > 15000
            ? string.Concat(rulebookContent.AsSpan(0, 15000), "\n\n[Content truncated...]")
            : rulebookContent;

        var userPrompt = $"""
            Game: {gameName}

            Rulebook Content:
            {truncatedContent}

            Please analyze this rulebook and generate 5-8 specific quick questions that players
            would commonly ask when learning "{gameName}".
            """;

        try
        {
            var result = await _llmService.GenerateJsonAsync<LlmQuestionResponse>(
                SystemPrompt,
                userPrompt,
                RequestSource.RagPipeline,
                cancellationToken).ConfigureAwait(false);

            if (result?.Questions == null || result.Questions.Count == 0)
            {
                _logger.LogWarning("LLM returned no questions, using fallback");
                return CreateFallbackQuestions(gameName);
            }

            // Convert LLM response to domain objects
            var questions = result.Questions
                .Select(q => new GeneratedQuestion(
                    Text: q.Text,
                    Category: ParseCategory(q.Category),
                    Confidence: q.Confidence))
                .ToList();

            _logger.LogInformation(
                "Generated {Count} questions with overall confidence {Confidence}",
                questions.Count,
                result.OverallConfidence);

            return new QuickQuestionGenerationResult(
                Questions: questions,
                ConfidenceScore: result.OverallConfidence);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating questions for {GameName}, using fallback", gameName);
            return CreateFallbackQuestions(gameName);
        }
    }

    /// <summary>
    /// Creates fallback generic questions when AI generation fails.
    /// </summary>
    private static QuickQuestionGenerationResult CreateFallbackQuestions(string gameName)
    {
        var fallbackQuestions = new List<GeneratedQuestion>
        {
            new("Come si gioca?", QuestionCategory.Gameplay, 0.5m),
            new("Quali sono le regole principali?", QuestionCategory.Rules, 0.5m),
            new("Come si vince?", QuestionCategory.Winning, 0.5m),
            new("Come si prepara il gioco?", QuestionCategory.Setup, 0.5m),
            new("Quanto dura una partita?", QuestionCategory.Gameplay, 0.5m),
        };

        return new QuickQuestionGenerationResult(fallbackQuestions, ConfidenceScore: 0.5m);
    }

    /// <summary>
    /// Parses category string from LLM response to enum.
    /// </summary>
    private QuestionCategory ParseCategory(string categoryStr)
    {
        var category = categoryStr.ToLowerInvariant() switch
        {
            "gameplay" => (QuestionCategory?)QuestionCategory.Gameplay,
            "rules" => (QuestionCategory?)QuestionCategory.Rules,
            "winning" => (QuestionCategory?)QuestionCategory.Winning,
            "setup" => (QuestionCategory?)QuestionCategory.Setup,
            "strategy" => (QuestionCategory?)QuestionCategory.Strategy,
            "clarifications" => (QuestionCategory?)QuestionCategory.Clarifications,
            _ => null
        };

        if (category is null)
        {
            _logger.LogWarning("Unknown category from LLM: {Category}, defaulting to Gameplay", categoryStr);
            return QuestionCategory.Gameplay;
        }

        return category.Value;
    }

    /// <summary>
    /// LLM response schema for deserialization.
    /// </summary>
    private sealed record LlmQuestionResponse(
        List<LlmQuestion> Questions,
        decimal OverallConfidence
    );

    private sealed record LlmQuestion(
        string Text,
        string Category,
        decimal Confidence
    );
}