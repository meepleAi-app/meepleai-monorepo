using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Models;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;

namespace Api.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Handler for StreamSetupGuideQuery.
/// Implements streaming setup guide generation with progressive step delivery.
/// AI-03: RAG-based setup guide generation with streaming delivery
/// </summary>
public class StreamSetupGuideQueryHandler : IStreamingQueryHandler<StreamSetupGuideQuery, RagStreamingEvent>
{
    private readonly IEmbeddingService _embeddingService;
    private readonly IQdrantService _qdrantService;
    private readonly ILlmService _llmService;
    private readonly IPromptTemplateService _promptTemplateService;
    private readonly IConfiguration _configuration;
    private readonly ILogger<StreamSetupGuideQueryHandler> _logger;
    private readonly TimeProvider _timeProvider;
    public StreamSetupGuideQueryHandler(
        IEmbeddingService embeddingService,
        IQdrantService qdrantService,
        ILlmService llmService,
        IPromptTemplateService promptTemplateService,
        IConfiguration configuration,
        ILogger<StreamSetupGuideQueryHandler> logger,
        TimeProvider? timeProvider = null)
    {
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _qdrantService = qdrantService ?? throw new ArgumentNullException(nameof(qdrantService));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _promptTemplateService = promptTemplateService ?? throw new ArgumentNullException(nameof(promptTemplateService));
        _configuration = configuration ?? throw new ArgumentNullException(nameof(configuration));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }
    public async IAsyncEnumerable<RagStreamingEvent> Handle(
        StreamSetupGuideQuery query,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(query.GameId))
        {
            yield return CreateEvent(StreamingEventType.Error,
                new StreamingError("Game ID is required.", "EMPTY_GAME_ID"));
            yield break;
        }

        _logger.LogInformation("Starting streaming setup guide generation for game {GameId}", query.GameId);

        // Emit initial state update
        yield return CreateEvent(StreamingEventType.StateUpdate,
            new StreamingStateUpdate("Preparing setup guide..."));

        await Task.Delay(TimeSpan.FromMilliseconds(50), cancellationToken).ConfigureAwait(false);
        cancellationToken.ThrowIfCancellationRequested();

        // Generate setup guide using internal method (no yield in try-catch)
        var result = await GenerateSetupGuideInternalAsync(query.GameId, cancellationToken).ConfigureAwait(false);

        if (!result.Success)
        {
            // Error occurred - stream error event and fallback to default steps
            yield return CreateEvent(StreamingEventType.StateUpdate,
                new StreamingStateUpdate("Using default setup steps..."));

            await foreach (var evt in StreamStepsAsync(
                result.Steps,
                result.PromptTokens,
                result.CompletionTokens,
                result.TotalTokens,
                result.Confidence,
                cancellationToken).ConfigureAwait(false))
            {
                yield return evt;
            }
            yield break;
        }

        // Success - stream the generated steps
        await foreach (var evt in StreamStepsAsync(
            result.Steps,
            result.PromptTokens,
            result.CompletionTokens,
            result.TotalTokens,
            result.Confidence,
            cancellationToken).ConfigureAwait(false))
        {
            yield return evt;
        }
    }
    /// <summary>
    /// Internal method to generate setup guide with try-catch (no yield statements)
    /// </summary>
    private async Task<SetupGuideGenerationResult> GenerateSetupGuideInternalAsync(
        string gameId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Step 1: Generate embedding for setup query
            var setupQuery = "game setup preparation initial components placement player starting conditions";
            var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(setupQuery, cancellationToken).ConfigureAwait(false);

            if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            {
                _logger.LogWarning("Failed to generate embedding for setup query: {GameId}", gameId);
                return SetupGuideGenerationResult.CreateDefault();
            }

            var queryEmbedding = embeddingResult.Embeddings[0];

            // Step 2: Search Qdrant for setup-related chunks
            var searchResult = await _qdrantService.SearchAsync(
                gameId,
                queryEmbedding,
                limit: 10,
                cancellationToken).ConfigureAwait(false);

            if (!searchResult.Success || searchResult.Results.Count == 0)
            {
                _logger.LogInformation("No RAG results found for game {GameId}, using default steps", gameId);
                return SetupGuideGenerationResult.CreateDefault();
            }

            // Build context from retrieved chunks
            var context = string.Join("\n\n---\n\n", searchResult.Results.Select(r =>
                $"[Page {r.Page}]\n{r.Text}"));

            // Build references for all steps
            var allReferences = searchResult.Results.Select(r => new Snippet(
                r.Text,
                $"PDF:{r.PdfId}",
                r.Page,
                0,
                r.Score
            )).ToList();

            var confidence = (double?)searchResult.Results.Max(r => r.Score);

            // Step 3: Generate setup steps with LLM
            var systemPrompt = await GetSetupGuideSystemPromptAsync(cancellationToken).ConfigureAwait(false);
            var userPrompt = $@"CONTEXT FROM RULEBOOK:
{context}

TASK: Generate a step-by-step setup guide for this board game. Focus on the initial setup before gameplay begins.";

            var llmResult = await _llmService.GenerateCompletionAsync(systemPrompt, userPrompt, cancellationToken).ConfigureAwait(false);

            if (!llmResult.Success || string.IsNullOrWhiteSpace(llmResult.Response))
            {
                _logger.LogWarning("LLM generation failed for game {GameId}, falling back to default", gameId);
                return SetupGuideGenerationResult.CreateDefault();
            }

            // Parse LLM response into steps
            var steps = ParseLlmStepsResponse(llmResult.Response, allReferences);

            _logger.LogInformation(
                "LLM generated {StepCount} setup steps for game {GameId}, tokens: {TotalTokens}",
                steps.Count, gameId, llmResult.Usage.TotalTokens);

            return new SetupGuideGenerationResult(
                Success: true,
                Steps: steps,
                TotalTokens: llmResult.Usage.TotalTokens,
                PromptTokens: llmResult.Usage.PromptTokens,
                CompletionTokens: llmResult.Usage.CompletionTokens,
                Confidence: confidence,
                ErrorMessage: null,
                ErrorCode: null
            );
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Service boundary - graceful degradation for AI/LLM operations
        // GRACEFUL DEGRADATION: Setup guide generation failures return default steps
        // Rationale: Setup guide is a helpful feature but not critical. If RAG/LLM pipeline
        // fails, returning default steps allows the API to respond successfully and
        // lets the UI display fallback content. Throwing would cause stream errors and poor UX.
        // Context: Failures typically from Qdrant/OpenRouter/LLM timeout or rate limiting
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating setup guide for game {GameId}", gameId);
            return SetupGuideGenerationResult.CreateError(ex.Message);
        }
#pragma warning restore CA1031
    }
    /// <summary>
    /// Stream individual setup steps with delays between each step
    /// </summary>
    private async IAsyncEnumerable<RagStreamingEvent> StreamStepsAsync(
        List<SetupGuideStep> steps,
        int promptTokens,
        int completionTokens,
        int totalTokens,
        double? confidence,
        [EnumeratorCancellation] CancellationToken cancellationToken)
    {
        // Stream each step individually
        for (int i = 0; i < steps.Count; i++)
        {
            cancellationToken.ThrowIfCancellationRequested();

            yield return CreateEvent(StreamingEventType.SetupStep,
                new StreamingSetupStep(steps[i]));

            // Small delay between steps for progressive delivery
            if (i < steps.Count - 1)
            {
                await Task.Delay(TimeSpan.FromMilliseconds(100), cancellationToken).ConfigureAwait(false);
            }
        }

        // Estimate setup time (2 minutes per step, minimum 5 minutes)
        var estimatedTime = Math.Max(5, steps.Count * 2);

        _logger.LogInformation(
            "Streamed {StepCount} setup steps with estimated {Minutes} min setup time",
            steps.Count, estimatedTime);

        // Emit final complete event
        yield return CreateEvent(StreamingEventType.Complete,
            new StreamingComplete(
                estimatedTime,
                promptTokens,
                completionTokens,
                totalTokens,
                confidence));
    }
    /// <summary>
    /// Parse LLM-generated steps response into structured SetupGuideStep objects
    /// </summary>
    private List<SetupGuideStep> ParseLlmStepsResponse(string llmResponse, List<Snippet> references)
    {
        var steps = new List<SetupGuideStep>();

        try
        {
            // Split response by STEP markers
            var lines = llmResponse.Split('\n', StringSplitOptions.RemoveEmptyEntries);
            int currentStepNumber = 0;
            string? currentTitle = null;
            var currentInstructionLines = new List<string>();

            foreach (var line in lines)
            {
                var trimmedLine = line.Trim();

                // Check if this is a new step
                if (trimmedLine.StartsWith("STEP ", StringComparison.OrdinalIgnoreCase))
                {
                    // Save previous step if exists
                    if (currentStepNumber > 0 && currentTitle != null)
                    {
                        var instruction = string.Join(" ", currentInstructionLines).Trim();
                        if (!string.IsNullOrWhiteSpace(instruction))
                        {
                            steps.Add(CreateParsedStep(currentStepNumber, currentTitle, instruction, references));
                        }
                    }

                    // Parse new step
                    var colonIndex = trimmedLine.IndexOf(':');
                    if (colonIndex > 0)
                    {
                        var stepPart = trimmedLine.Substring(0, colonIndex).Trim();
                        var titlePart = trimmedLine.Substring(colonIndex + 1).Trim();

                        // Extract step number
                        // FIX MA0009: Add timeout to prevent ReDoS attacks
                        var numberMatch = Regex.Match(stepPart, @"\d+", RegexOptions.None, TimeSpan.FromSeconds(1));
                        if (numberMatch.Success && int.TryParse(numberMatch.Value, CultureInfo.InvariantCulture, out var stepNumber))
                        {
                            currentStepNumber = stepNumber;
                            currentTitle = titlePart;
                            currentInstructionLines.Clear();
                        }
                    }
                }
                else if (currentStepNumber > 0)
                {
                    // This is part of the current step's instruction
                    if (!string.IsNullOrWhiteSpace(trimmedLine))
                    {
                        currentInstructionLines.Add(trimmedLine);
                    }
                }
            }

            // Don't forget the last step
            if (currentStepNumber > 0 && currentTitle != null)
            {
                var instruction = string.Join(" ", currentInstructionLines).Trim();
                if (!string.IsNullOrWhiteSpace(instruction))
                {
                    steps.Add(CreateParsedStep(currentStepNumber, currentTitle, instruction, references));
                }
            }

            // If parsing failed, return empty list (will trigger fallback to default steps)
            if (steps.Count == 0)
            {
                _logger.LogWarning("Failed to parse any steps from LLM response");
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Service boundary - graceful degradation for LLM parsing
        // GRACEFUL DEGRADATION: LLM parsing failures return empty list for fallback handling
        // Rationale: Caller checks for empty list and uses default steps as fallback.
        // Parsing failure indicates malformed LLM output (unexpected format, hallucination).
        // Returning empty list triggers graceful degradation to generic setup steps.
        // Context: LLM output format can vary due to model changes or prompt drift
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing LLM steps response");
        }
#pragma warning restore CA1031

        return steps;
    }

    /// <summary>
    /// Create a setup step from parsed LLM data
    /// </summary>
    private SetupGuideStep CreateParsedStep(
        int stepNumber,
        string title,
        string instruction,
        List<Snippet> references)
    {
        // Check if step is optional
        var isOptional = title.Contains("[OPTIONAL]", StringComparison.OrdinalIgnoreCase);
        if (isOptional)
        {
            title = title.Replace("[OPTIONAL]", "", StringComparison.OrdinalIgnoreCase).Trim();
        }

        // Clean up instruction text
        var cleanInstruction = CleanupInstruction(instruction);

        // Distribute references across steps (give first 2-3 references to each step)
        var stepReferences = references
            .Skip((stepNumber - 1) * 2)
            .Take(3)
            .ToList();

        return new SetupGuideStep(
            stepNumber,
            title,
            cleanInstruction,
            stepReferences,
            isOptional
        );
    }

    /// <summary>
    /// Clean up and format instruction text
    /// </summary>
    private string CleanupInstruction(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
        {
            return "Follow the rulebook instructions.";
        }

        // Truncate if too long (max 500 chars for readability)
        if (text.Length > 500)
        {
            text = text.Substring(0, 497) + "...";
        }

        return text.Trim();
    }

    /// <summary>
    /// ADMIN-01 Phase 3: Get setup guide system prompt using database-driven prompt management with fallback
    /// </summary>
    private async Task<string> GetSetupGuideSystemPromptAsync(CancellationToken ct = default)
    {
        // ADMIN-01: Check feature flag for database-driven prompts
        var usePromptDatabase = _configuration.GetValue<bool>("Features:PromptDatabase", false);

        if (usePromptDatabase)
        {
            try
            {
                var promptTemplate = await _promptTemplateService.GetActivePromptAsync("setup-guide-system-prompt", ct).ConfigureAwait(false);
                if (!string.IsNullOrWhiteSpace(promptTemplate))
                {
                    _logger.LogDebug("Using database-driven setup guide system prompt");
                    return promptTemplate;
                }
                else
                {
                    _logger.LogWarning("Database prompt 'setup-guide-system-prompt' not found, falling back to hardcoded prompt");
                }
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: Service boundary - fallback pattern for database prompt retrieval
            // FALLBACK PATTERN: Database prompt retrieval failures use hardcoded prompt
            // Rationale: Dynamic prompt loading from DB is an enhancement (ADMIN-01 Phase 3).
            // If PromptTemplateService fails (DB unavailable, Redis timeout), we must still
            // generate setup guides. Fallback to hardcoded prompt ensures feature availability.
            // Context: DB/Redis failures are typically transient (connection loss, resource exhaustion)
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to retrieve setup guide system prompt from database, falling back to hardcoded prompt");
            }
#pragma warning restore CA1031
        }

        // Fallback: Use hardcoded prompt (backward compatibility)
        return GetSetupGuideSystemPromptFallback();
    }

    /// <summary>
    /// ADMIN-01 Phase 3: Hardcoded fallback prompt for backward compatibility
    /// </summary>
    private string GetSetupGuideSystemPromptFallback()
    {
        return @"You are a board game setup assistant. Your job is to create clear, actionable setup instructions based ONLY on the provided rulebook context.

CRITICAL INSTRUCTIONS:
- Generate 3-7 numbered setup steps in a logical order
- Each step should be concrete and actionable (e.g., 'Place the board in the center')
- Keep each step instruction concise (1-2 sentences maximum)
- Mark optional steps with '[OPTIONAL]' prefix in the title
- Use information ONLY from the provided context
- If the context is insufficient, generate generic but helpful setup steps
- Return ONLY the steps in this exact format:

STEP 1: <title>
<instruction>

STEP 2: <title>
<instruction>

etc.";
    }
    /// <summary>
    /// Create a streaming event with timestamp
    /// </summary>
    private RagStreamingEvent CreateEvent(StreamingEventType type, object? data)
    {
        return new RagStreamingEvent(type, data, _timeProvider.GetUtcNow().UtcDateTime);
    }
    /// <summary>
    /// Result class for internal setup guide generation (no yield in try-catch)
    /// </summary>
    private sealed record SetupGuideGenerationResult(
        bool Success,
        List<SetupGuideStep> Steps,
        int TotalTokens,
        int PromptTokens,
        int CompletionTokens,
        double? Confidence,
        string? ErrorMessage = null,
        string? ErrorCode = null)
    {
        /// <summary>
        /// Create a default result with fallback steps
        /// </summary>
        public static SetupGuideGenerationResult CreateDefault()
        {
            return new SetupGuideGenerationResult(
                Success: true,
                Steps: CreateDefaultSetupStepsStatic(),
                TotalTokens: 0,
                PromptTokens: 0,
                CompletionTokens: 0,
                Confidence: null,
                ErrorMessage: null,
                ErrorCode: null
            );
        }

        /// <summary>
        /// Create an error result with fallback steps
        /// </summary>
        public static SetupGuideGenerationResult CreateError(string errorMessage, string? errorCode = null)
        {
            return new SetupGuideGenerationResult(
                Success: false,
                Steps: CreateDefaultSetupStepsStatic(),
                TotalTokens: 0,
                PromptTokens: 0,
                CompletionTokens: 0,
                Confidence: null,
                ErrorMessage: errorMessage,
                ErrorCode: errorCode
            );
        }

        /// <summary>
        /// Static version of CreateDefaultSetupSteps for use in record factory methods
        /// </summary>
        private static List<SetupGuideStep> CreateDefaultSetupStepsStatic()
        {
            return new List<SetupGuideStep>
            {
                new SetupGuideStep(
                    1,
                    "Prepare Components",
                    "Sort and organize all game components according to the rulebook.",
                    new List<Snippet>(),
                    isOptional: false
                ),
                new SetupGuideStep(
                    2,
                    "Setup Play Area",
                    "Place the game board and any shared components in the center of the table.",
                    new List<Snippet>(),
                    isOptional: false
                ),
                new SetupGuideStep(
                    3,
                    "Distribute Player Materials",
                    "Give each player their starting resources, cards, and player board as specified in the rules.",
                    new List<Snippet>(),
                    isOptional: false
                ),
                new SetupGuideStep(
                    4,
                    "Determine First Player",
                    "Choose or randomly determine the starting player according to the rules.",
                    new List<Snippet>(),
                    isOptional: false
                ),
                new SetupGuideStep(
                    5,
                    "Final Setup",
                    "Complete any remaining setup steps specific to this game as described in the rulebook.",
                    new List<Snippet>(),
                    isOptional: false
                )
            };
        }
    }
}
