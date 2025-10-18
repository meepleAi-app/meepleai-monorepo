using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Diagnostics.Metrics;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Api.Configuration;
using Api.Models;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Api.Services.Chat;

/// <summary>
/// Service for generating AI-powered follow-up questions based on Q&A context.
/// CHAT-02: AI-Generated Follow-Up Questions
/// </summary>
public class FollowUpQuestionService : IFollowUpQuestionService
{
    private readonly ILlmService _llmService;
    private readonly ILogger<FollowUpQuestionService> _logger;
    private readonly FollowUpQuestionsConfiguration _config;

    // OpenTelemetry metrics
    private readonly Counter<long> _questionsGeneratedCounter;
    private readonly Counter<long> _errorsCounter;
    private readonly Histogram<double> _generationDurationHistogram;

    /// <summary>
    /// Initializes a new instance of the <see cref="FollowUpQuestionService"/> class.
    /// </summary>
    /// <param name="llmService">The LLM service for generating questions</param>
    /// <param name="logger">Logger instance</param>
    /// <param name="config">Follow-up questions configuration</param>
    /// <param name="meterFactory">Meter factory for OpenTelemetry metrics</param>
    public FollowUpQuestionService(
        ILlmService llmService,
        ILogger<FollowUpQuestionService> logger,
        IOptions<FollowUpQuestionsConfiguration> config,
        IMeterFactory meterFactory)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));

        // Initialize OpenTelemetry metrics
        var meter = meterFactory.Create("MeepleAI.FollowUpQuestions");

        _questionsGeneratedCounter = meter.CreateCounter<long>(
            "meepleai.followups.generated.total",
            description: "Total number of follow-up questions successfully generated");

        _errorsCounter = meter.CreateCounter<long>(
            "meepleai.followups.errors.total",
            description: "Total number of follow-up question generation errors");

        _generationDurationHistogram = meter.CreateHistogram<double>(
            "meepleai.followups.generation.duration",
            unit: "ms",
            description: "Duration of follow-up question generation");
    }

    /// <summary>
    /// Generates follow-up questions based on the original question, answer, and RAG context.
    /// </summary>
    /// <param name="originalQuestion">The user's original question</param>
    /// <param name="generatedAnswer">The AI-generated answer</param>
    /// <param name="ragContext">RAG context snippets used to generate the answer</param>
    /// <param name="gameName">Name of the game for context-specific questions</param>
    /// <param name="maxQuestions">Maximum number of questions to generate (limited by configuration)</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>List of follow-up questions (empty list if generation fails)</returns>
    public async Task<IReadOnlyList<string>> GenerateQuestionsAsync(
        string originalQuestion,
        string generatedAnswer,
        IReadOnlyList<Snippet> ragContext,
        string gameName,
        int maxQuestions = 5,
        CancellationToken ct = default)
    {
        var stopwatch = Stopwatch.StartNew();
        var sanitizedGameName = SanitizeGameNameForMetrics(gameName);
        var tags = new TagList { { "game_name", sanitizedGameName } };

        try
        {
            // Check for user cancellation before starting
            if (ct.IsCancellationRequested)
            {
                throw new OperationCanceledException(ct);
            }

            // Apply configuration limits (take minimum of requested and configured max)
            var effectiveMaxQuestions = Math.Min(maxQuestions, _config.MaxQuestionsPerResponse);

            // Build prompts
            var systemPrompt = BuildSystemPrompt(effectiveMaxQuestions);
            var userPrompt = BuildUserPrompt(originalQuestion, generatedAnswer, ragContext, gameName);

            _logger.LogDebug(
                "Generating up to {MaxQuestions} follow-up questions for game {GameName}",
                effectiveMaxQuestions,
                gameName);

            // Create linked cancellation token with timeout
            using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(ct);
            timeoutCts.CancelAfter(TimeSpan.FromMilliseconds(_config.GenerationTimeoutMs));

            FollowUpQuestionsDto? result = null;
            int attemptCount = 0;

            // Retry loop (up to MaxRetries)
            while (attemptCount < _config.MaxRetries && result == null)
            {
                attemptCount++;

                try
                {
                    // Call LLM service to generate structured JSON response
                    result = await _llmService.GenerateJsonAsync<FollowUpQuestionsDto>(
                        systemPrompt,
                        userPrompt,
                        timeoutCts.Token);

                    if (result == null)
                    {
                        _logger.LogWarning(
                            "LLM returned null for follow-up questions (attempt {Attempt}/{MaxRetries})",
                            attemptCount,
                            _config.MaxRetries);

                        // Continue to retry if we haven't reached max retries
                        if (attemptCount < _config.MaxRetries)
                        {
                            continue;
                        }
                    }
                }
                catch (OperationCanceledException) when (timeoutCts.Token.IsCancellationRequested && !ct.IsCancellationRequested)
                {
                    // Timeout occurred (but not user cancellation)
                    _logger.LogWarning(
                        "Follow-up question generation timed out after {TimeoutMs}ms for game {GameName}",
                        _config.GenerationTimeoutMs,
                        gameName);

                    _errorsCounter.Add(1, tags);
                    return Array.Empty<string>();
                }
            }

            // If we exhausted retries without success, return empty list
            if (result == null)
            {
                _logger.LogWarning(
                    "Failed to generate follow-up questions after {Attempts} attempts for game {GameName}",
                    attemptCount,
                    gameName);

                _errorsCounter.Add(1, tags);
                return Array.Empty<string>();
            }

            // Validate and filter questions
            var validQuestions = result.Questions
                .Where(q => !string.IsNullOrWhiteSpace(q))
                .Take(effectiveMaxQuestions)
                .ToList();

            stopwatch.Stop();

            // Record success metrics
            _questionsGeneratedCounter.Add(validQuestions.Count, tags);
            _generationDurationHistogram.Record(stopwatch.Elapsed.TotalMilliseconds, tags);

            _logger.LogInformation(
                "Generated {QuestionCount} follow-up questions for game {GameName} in {DurationMs}ms",
                validQuestions.Count,
                gameName,
                stopwatch.Elapsed.TotalMilliseconds);

            return validQuestions.AsReadOnly();
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            // User-initiated cancellation - propagate
            _logger.LogInformation(
                "Follow-up question generation cancelled by user for game {GameName}",
                gameName);

            throw;
        }
        catch (Exception ex)
        {
            stopwatch.Stop();
            _errorsCounter.Add(1, tags);

            _logger.LogError(
                ex,
                "Error generating follow-up questions for game {GameName} after {DurationMs}ms",
                gameName,
                stopwatch.Elapsed.TotalMilliseconds);

            // Check if we should fail or degrade gracefully
            if (_config.FailOnGenerationError)
            {
                throw;
            }

            // Graceful degradation - return empty list
            return Array.Empty<string>();
        }
    }

    /// <summary>
    /// Builds the system prompt instructing the LLM to generate follow-up questions.
    /// </summary>
    /// <param name="maxQuestions">Maximum number of questions to generate</param>
    /// <returns>System prompt string</returns>
    private static string BuildSystemPrompt(int maxQuestions)
    {
        return $@"You are an expert board game assistant generating follow-up questions.

Your task: Generate {maxQuestions} concise, relevant follow-up questions that:
1. Help users explore related rules, strategies, or edge cases
2. Are directly answerable using the game's rulebook
3. Build naturally on the current conversation context
4. Vary in focus (clarifications, extensions, related scenarios)

Guidelines:
- Questions should be 5-15 words each
- Avoid yes/no questions - prefer ""how"", ""what"", ""when""
- Each question should explore a different aspect
- Maintain the user's experience level from the original question

Return ONLY a valid JSON object with this structure:
{{
  ""questions"": [""question 1"", ""question 2"", ...]
}}";
    }

    /// <summary>
    /// Builds the user prompt with game context, question, answer, and RAG snippets.
    /// </summary>
    /// <param name="originalQuestion">The user's original question</param>
    /// <param name="generatedAnswer">The AI-generated answer</param>
    /// <param name="ragContext">RAG context snippets (top 3 used)</param>
    /// <param name="gameName">Name of the game</param>
    /// <returns>User prompt string</returns>
    private static string BuildUserPrompt(
        string originalQuestion,
        string generatedAnswer,
        IReadOnlyList<Snippet> ragContext,
        string gameName)
    {
        // Take top 3 RAG snippets and truncate to 150 chars each
        var contextSnippets = ragContext
            .Take(3)
            .Select(s => TruncateText(s.text, 150))
            .ToList();

        var contextSection = contextSnippets.Count > 0
            ? string.Join("\n", contextSnippets.Select((text, i) => $"{i + 1}. {text}"))
            : "No specific rule context available";

        return $@"Game: {gameName}

User's Question: {originalQuestion}

Generated Answer: {generatedAnswer}

Relevant Rule Context:
{contextSection}

Generate follow-up questions that help the user explore this topic further.";
    }

    /// <summary>
    /// Truncates text to a maximum length, adding ellipsis if truncated.
    /// </summary>
    /// <param name="text">Text to truncate</param>
    /// <param name="maxLength">Maximum length</param>
    /// <returns>Truncated text</returns>
    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }

        return text.Substring(0, maxLength - 3) + "...";
    }

    /// <summary>
    /// Sanitizes game name for use as a metrics tag to avoid cardinality explosion.
    /// </summary>
    /// <param name="gameName">Original game name</param>
    /// <returns>Sanitized game name (alphanumeric, max 50 chars)</returns>
    private static string SanitizeGameNameForMetrics(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            return "unknown";
        }

        // Keep only alphanumeric characters, spaces, and hyphens
        var sanitized = new string(gameName
            .Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-')
            .ToArray());

        // Limit length to 50 chars to avoid cardinality issues
        if (sanitized.Length > 50)
        {
            sanitized = sanitized.Substring(0, 50);
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}
