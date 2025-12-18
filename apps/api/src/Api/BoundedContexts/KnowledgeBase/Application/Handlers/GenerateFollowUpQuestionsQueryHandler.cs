using System.Diagnostics;
using System.Diagnostics.Metrics;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Configuration;
using Api.Models;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Options;

#pragma warning disable MA0048 // File name must match type name - Contains Handler with related types
namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for GenerateFollowUpQuestionsQuery.
/// Generates AI-powered follow-up questions based on Q&A context.
/// CHAT-02: AI-Generated Follow-Up Questions
/// </summary>
internal sealed class GenerateFollowUpQuestionsQueryHandler
    : IRequestHandler<GenerateFollowUpQuestionsQuery, IReadOnlyList<string>>
{
    private readonly ILlmService _llmService;
    private readonly ILogger<GenerateFollowUpQuestionsQueryHandler> _logger;
    private readonly FollowUpQuestionsConfiguration _config;

    // OpenTelemetry metrics
    private readonly Counter<long> _questionsGeneratedCounter;
    private readonly Counter<long> _errorsCounter;
    private readonly Histogram<double> _generationDurationHistogram;

    public GenerateFollowUpQuestionsQueryHandler(
        ILlmService llmService,
        ILogger<GenerateFollowUpQuestionsQueryHandler> logger,
        IOptions<FollowUpQuestionsConfiguration> config,
        IMeterFactory meterFactory)
    {
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _config = config?.Value ?? throw new ArgumentNullException(nameof(config));

        // Initialize OpenTelemetry metrics
        // CA2000 suppression: Meter lifetime is managed by IMeterFactory (DI singleton).
        // IMeterFactory.Create() returns Meters that are cached and disposed when the factory itself is disposed.
        // Manually disposing here would break metric collection. See: https://learn.microsoft.com/en-us/dotnet/core/diagnostics/metrics-instrumentation#create-a-custom-metric
#pragma warning disable CA2000 // Dispose objects before losing scope - False positive: IMeterFactory manages Meter lifetime
        var meter = meterFactory.Create("MeepleAI.FollowUpQuestions");
#pragma warning restore CA2000 // Dispose objects before losing scope

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

    public async Task<IReadOnlyList<string>> Handle(
        GenerateFollowUpQuestionsQuery request,
        CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var sanitizedGameName = SanitizeGameNameForMetrics(request.GameName);
        var tags = new TagList { { "game_name", sanitizedGameName } };

        try
        {
            // Check for user cancellation before starting
            cancellationToken.ThrowIfCancellationRequested();


            // Apply configuration limits
            var effectiveMaxQuestions = Math.Min(request.MaxQuestions, _config.MaxQuestionsPerResponse);

            // Build prompts
            var systemPrompt = BuildSystemPrompt(effectiveMaxQuestions);
            var userPrompt = BuildUserPrompt(
                request.OriginalQuestion,
                request.GeneratedAnswer,
                request.RagContext,
                request.GameName);

            _logger.LogDebug(
                "Generating up to {MaxQuestions} follow-up questions for game {GameName}",
                effectiveMaxQuestions,
                request.GameName);

            // Generate questions with retry
            var result = await GenerateQuestionsWithRetryAsync(
                systemPrompt, userPrompt, request.GameName, tags, cancellationToken).ConfigureAwait(false);

            if (result == null)
            {
                _logger.LogWarning(
                    "Failed to generate follow-up questions after {MaxRetries} attempts for game {GameName}",
                    _config.MaxRetries,
                    request.GameName);

                _errorsCounter.Add(1, tags);
                return Array.Empty<string>();
            }

            // Validate, filter, and record metrics
            var validQuestions = RecordMetricsAndFilterQuestions(
                result, effectiveMaxQuestions, request.GameName, stopwatch, tags);

            return validQuestions.AsReadOnly();
        }
        catch (OperationCanceledException ex) when (cancellationToken.IsCancellationRequested)
        {
            // User-initiated cancellation - propagate
            _logger.LogInformation(ex,
                "Follow-up question generation cancelled by user for game {GameName}",
                request.GameName);

            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
#pragma warning restore CA1031
        {
            // CQRS handler: Catches all exceptions with configurable failure behavior
            // If FailOnGenerationError=true, re-throws; otherwise gracefully degrades to empty list
            stopwatch.Stop();
            _errorsCounter.Add(1, tags);

            _logger.LogError(
                ex,
                "Error generating follow-up questions for game {GameName} after {DurationMs}ms",
                request.GameName,
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
    /// Generates follow-up questions with retry logic and timeout handling.
    /// Returns FollowUpQuestionsDto or null if all retries failed.
    /// </summary>
    private async Task<FollowUpQuestionsDto?> GenerateQuestionsWithRetryAsync(
        string systemPrompt,
        string userPrompt,
        string gameName,
        TagList tags,
        CancellationToken cancellationToken)
    {
        // Create linked cancellation token with timeout
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
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
                    timeoutCts.Token).ConfigureAwait(false);

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
            catch (OperationCanceledException ex) when (timeoutCts.Token.IsCancellationRequested && !cancellationToken.IsCancellationRequested)
            {
                // Timeout occurred (but not user cancellation)
                _logger.LogWarning(ex,
                    "Follow-up question generation timed out after {TimeoutMs}ms for game {GameName}",
                    _config.GenerationTimeoutMs,
                    gameName);

                _errorsCounter.Add(1, tags);
                return null;
            }
        }

        return result;
    }

    /// <summary>
    /// Validates, filters questions and records success metrics.
    /// </summary>
    private List<string> RecordMetricsAndFilterQuestions(
        FollowUpQuestionsDto result,
        int effectiveMaxQuestions,
        string gameName,
        Stopwatch stopwatch,
        TagList tags)
    {
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

        return validQuestions;
    }

    /// <summary>
    /// Builds the system prompt instructing the LLM to generate follow-up questions.
    /// </summary>
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
    private static string TruncateText(string text, int maxLength)
    {
        if (string.IsNullOrEmpty(text) || text.Length <= maxLength)
        {
            return text;
        }

        return string.Concat(text.AsSpan(0, maxLength - 3), "...");
    }

    /// <summary>
    /// Sanitizes game name for use as a metrics tag to avoid cardinality explosion.
    /// </summary>
    private static string SanitizeGameNameForMetrics(string gameName)
    {
        if (string.IsNullOrWhiteSpace(gameName))
        {
            return "unknown";
        }

        // Keep only alphanumeric characters, spaces, and hyphens
        var sanitized = string.Concat(gameName.Where(c => char.IsLetterOrDigit(c) || c == ' ' || c == '-'));

        // Limit length to 50 chars to avoid cardinality issues
        if (sanitized.Length > 50)
        {
            sanitized = sanitized.Substring(0, 50);
        }

        return string.IsNullOrWhiteSpace(sanitized) ? "unknown" : sanitized;
    }
}

/// <summary>
/// DTO for follow-up questions JSON response from LLM.
/// </summary>
internal record FollowUpQuestionsDto
{
    public List<string> Questions { get; init; } = new();
}
