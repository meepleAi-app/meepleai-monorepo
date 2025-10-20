using Api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using System.Text;

namespace Api.Services;

/// <summary>
/// AI-07.1: Service for managing RAG prompt templates with few-shot learning support
/// Implements LangChain-style prompt engineering with configurable templates and examples
/// </summary>
public class PromptTemplateService : IPromptTemplateService
{
    private readonly RagPromptsConfiguration _config;
    private readonly ILogger<PromptTemplateService> _logger;

    // Fallback hardcoded prompts for backward compatibility
    private static readonly PromptTemplate FallbackTemplate = new()
    {
        SystemPrompt = @"You are a board game rules assistant. Your job is to answer questions about board game rules based ONLY on the provided context from the rulebook.

CRITICAL INSTRUCTIONS:
- If the answer to the question is clearly found in the provided context, answer it concisely and accurately.
- If the answer is NOT in the provided context or you're uncertain, respond with EXACTLY: ""Not specified""
- Do NOT make assumptions or use external knowledge about the game.
- Do NOT hallucinate or invent information.
- Keep your answers brief and to the point (2-3 sentences maximum).
- Reference page numbers when relevant.",
        UserPromptTemplate = @"CONTEXT FROM RULEBOOK:
{context}

QUESTION:
{query}

ANSWER:",
        FewShotExamples = new List<FewShotExample>()
    };

    public PromptTemplateService(
        IOptions<RagPromptsConfiguration> config,
        ILogger<PromptTemplateService> logger)
    {
        _config = config?.Value ?? new RagPromptsConfiguration();
        _logger = logger;
    }

    /// <summary>
    /// Gets the appropriate prompt template for a game and question type
    /// Priority: Game-specific > Question-type-specific > Default > Fallback
    /// </summary>
    public Task<PromptTemplate> GetTemplateAsync(Guid? gameId, QuestionType questionType)
    {
        try
        {
            // Priority 1: Game-specific template
            if (gameId.HasValue && _config.GameTemplates.TryGetValue(gameId.Value.ToString(), out var gameTemplates))
            {
                var questionTypeKey = questionType.ToString();
                if (gameTemplates.TryGetValue(questionTypeKey, out var gameTemplate))
                {
                    _logger.LogDebug(
                        "Using game-specific template for game {GameId}, question type {QuestionType}",
                        gameId, questionType);
                    return Task.FromResult(ConvertToPromptTemplate(gameTemplate, gameId, questionType));
                }
            }

            // Priority 2: Question-type-specific template
            var questionTypeKeyStr = questionType.ToString();
            if (_config.Templates.TryGetValue(questionTypeKeyStr, out var typeTemplate))
            {
                _logger.LogDebug(
                    "Using question-type-specific template for type {QuestionType}",
                    questionType);
                return Task.FromResult(ConvertToPromptTemplate(typeTemplate, null, questionType));
            }

            // Priority 3: Default template
            if (_config.Default != null)
            {
                _logger.LogDebug("Using default template");
                return Task.FromResult(ConvertToPromptTemplate(_config.Default, null, QuestionType.General));
            }

            // Priority 4: Fallback hardcoded template
            _logger.LogWarning(
                "No configuration found for game {GameId}, question type {QuestionType}. Using fallback template.",
                gameId, questionType);
            return Task.FromResult(FallbackTemplate);
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Error loading prompt template for game {GameId}, question type {QuestionType}. Using fallback.",
                gameId, questionType);
            return Task.FromResult(FallbackTemplate);
        }
    }

    /// <summary>
    /// Renders system prompt with few-shot examples in LangChain format
    /// </summary>
    public string RenderSystemPrompt(PromptTemplate template)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        if (template.FewShotExamples == null || template.FewShotExamples.Count == 0)
        {
            return template.SystemPrompt;
        }

        var sb = new StringBuilder();
        sb.AppendLine(template.SystemPrompt);
        sb.AppendLine();
        sb.AppendLine("EXAMPLES:");

        foreach (var example in template.FewShotExamples)
        {
            sb.AppendLine($"Q: {example.Question}");
            sb.AppendLine($"A: {example.Answer}");
            sb.AppendLine();
        }

        sb.AppendLine("INSTRUCTIONS:");
        sb.AppendLine("- Be precise and cite page numbers when available");
        sb.AppendLine("- If the answer is not in the provided context, respond with \"Not specified\"");
        sb.AppendLine("- State confidence level if uncertain");
        sb.AppendLine("- For ambiguous questions, ask for clarification");

        return sb.ToString().TrimEnd();
    }

    /// <summary>
    /// Renders user prompt with context and query placeholders replaced
    /// </summary>
    public string RenderUserPrompt(PromptTemplate template, string context, string query)
    {
        if (template == null)
        {
            throw new ArgumentNullException(nameof(template));
        }

        if (string.IsNullOrEmpty(template.UserPromptTemplate))
        {
            throw new ArgumentException("UserPromptTemplate cannot be null or empty", nameof(template));
        }

        return template.UserPromptTemplate
            .Replace("{context}", context ?? string.Empty)
            .Replace("{query}", query ?? string.Empty);
    }

    /// <summary>
    /// Classifies question based on keyword matching
    /// Keywords are case-insensitive and checked in priority order
    /// </summary>
    public QuestionType ClassifyQuestion(string query)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return QuestionType.General;
        }

        var queryLower = query.ToLowerInvariant();

        // Check keywords in priority order (most specific first)

        // EdgeCases keywords (check first as they're most specific)
        if (ContainsAny(queryLower, new[]
            { "en passant", "castling", "castle", "stalemate", "special", "exception", "edge case" }))
        {
            return QuestionType.EdgeCases;
        }

        // WinningConditions keywords
        if (ContainsAny(queryLower, new[]
            { "win", "victory", "lose", "checkmate", "three in a row", "winning", "defeat" }))
        {
            return QuestionType.WinningConditions;
        }

        // Setup keywords
        if (ContainsAny(queryLower, new[]
            { "setup", "set up", "start", "begin", "prepare", "place pieces", "initial", "arrange" }))
        {
            return QuestionType.Setup;
        }

        // Gameplay keywords (most common, check last)
        if (ContainsAny(queryLower, new[]
            { "move", "turn", "action", "can i", "allowed", "play", "rules", "how does" }))
        {
            return QuestionType.Gameplay;
        }

        // Default to General if no keywords match
        return QuestionType.General;
    }

    #region Private Helper Methods

    private bool ContainsAny(string text, string[] keywords)
    {
        return keywords.Any(keyword => text.Contains(keyword, StringComparison.OrdinalIgnoreCase));
    }

    private PromptTemplate ConvertToPromptTemplate(
        PromptTemplateConfig config,
        Guid? gameId,
        QuestionType questionType)
    {
        if (config == null)
        {
            throw new ArgumentNullException(nameof(config));
        }

        var fewShotExamples = config.FewShotExamples?
            .Select(e => new FewShotExample
            {
                Question = e.Question,
                Answer = e.Answer,
                Category = e.Category
            })
            .ToList() ?? new List<FewShotExample>();

        return new PromptTemplate
        {
            SystemPrompt = config.SystemPrompt,
            UserPromptTemplate = config.UserPromptTemplate,
            FewShotExamples = fewShotExamples,
            GameId = gameId,
            QuestionType = questionType
        };
    }

    #endregion
}
