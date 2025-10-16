using Api.Infrastructure;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;

namespace Api.Services;

/// <summary>
/// AI-03: Service for generating step-by-step setup guides using RAG + LLM
/// AI-05: Now with caching support for reduced latency
/// </summary>
public class SetupGuideService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQdrantService _qdrantService;
    private readonly ILlmService _llmService;
    private readonly IAiResponseCacheService _cache;
    private readonly ILogger<SetupGuideService> _logger;

    public SetupGuideService(
        MeepleAiDbContext dbContext,
        IEmbeddingService embeddingService,
        IQdrantService qdrantService,
        ILlmService llmService,
        IAiResponseCacheService cache,
        ILogger<SetupGuideService> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _qdrantService = qdrantService;
        _llmService = llmService;
        _cache = cache;
        _logger = logger;
    }

    /// <summary>
    /// AI-03: Generate step-by-step setup guide with references using RAG + LLM
    /// AI-05: Now with caching support for reduced latency
    /// </summary>
    public async Task<SetupGuideResponse> GenerateSetupGuideAsync(
        string gameId,
        CancellationToken cancellationToken = default)
    {
        // AI-05: Check cache first
        var cacheKey = _cache.GenerateSetupCacheKey(gameId);
        var cachedResponse = await _cache.GetAsync<SetupGuideResponse>(cacheKey, cancellationToken);
        if (cachedResponse != null)
        {
            _logger.LogInformation("Returning cached Setup guide for game {GameId}", gameId);
            return cachedResponse;
        }

        try
        {
            // Get game information
            var game = await _dbContext.Games
                .Where(g => g.Id == gameId)
                .FirstOrDefaultAsync(cancellationToken);

            if (game == null)
            {
                _logger.LogWarning("Game not found: {GameId}", gameId);
                return CreateEmptySetupGuide("Unknown Game");
            }

            // Query RAG for comprehensive setup-related content
            var setupQuery = "game setup preparation initial components placement player starting conditions";
            var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(setupQuery, cancellationToken);

            if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            {
                _logger.LogWarning("Failed to generate embedding for setup query: {GameId}", gameId);
                return CreateEmptySetupGuide(game.Name);
            }

            var queryEmbedding = embeddingResult.Embeddings[0];

            // Search Qdrant for setup-related chunks (get more for comprehensive guide)
            var searchResult = await _qdrantService.SearchAsync(
                gameId,
                queryEmbedding,
                limit: 10, // Increased from 2 for better coverage
                cancellationToken);

            List<SetupGuideStep> allSteps;
            int totalTokens = 0;
            int promptTokens = 0;
            int completionTokens = 0;
            double? confidence = null;

            if (searchResult.Success && searchResult.Results.Count > 0)
            {
                // Build context from retrieved chunks
                var context = string.Join("\n\n---\n\n", searchResult.Results.Select(r =>
                    $"[Page {r.Page}]\n{r.Text}"));

                // Build references for all steps
                var allReferences = searchResult.Results.Select(r => new Snippet(
                    r.Text,
                    $"PDF:{r.PdfId}",
                    r.Page,
                    0 // line number not tracked in chunks
                )).ToList();

                // Use LLM to synthesize structured setup steps from the context
                var systemPrompt = @"You are a board game setup assistant. Your job is to create clear, actionable setup instructions based ONLY on the provided rulebook context.

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

                var userPrompt = $@"CONTEXT FROM RULEBOOK:
{context}

TASK: Generate a step-by-step setup guide for this board game. Focus on the initial setup before gameplay begins.";

                var llmResult = await _llmService.GenerateCompletionAsync(systemPrompt, userPrompt, cancellationToken);

                if (llmResult.Success && !string.IsNullOrWhiteSpace(llmResult.Response))
                {
                    // Parse LLM response into steps
                    allSteps = ParseLlmStepsResponse(llmResult.Response, allReferences);
                    totalTokens = llmResult.Usage.TotalTokens;
                    promptTokens = llmResult.Usage.PromptTokens;
                    completionTokens = llmResult.Usage.CompletionTokens;

                    _logger.LogInformation(
                        "LLM generated {StepCount} setup steps for game {GameId}, tokens: {TotalTokens}",
                        allSteps.Count, gameId, totalTokens);
                }
                else
                {
                    _logger.LogWarning("LLM generation failed for game {GameId}, falling back to default", gameId);
                    allSteps = CreateDefaultSetupSteps();
                }

                confidence = (double?)searchResult.Results.Max(r => r.Score);
            }
            else
            {
                _logger.LogInformation("No RAG results found for game {GameId}, using default steps", gameId);
                allSteps = CreateDefaultSetupSteps();
            }

            // Estimate setup time (2 minutes per step, minimum 5 minutes)
            var estimatedTime = Math.Max(5, allSteps.Count * 2);

            _logger.LogInformation(
                "Generated setup guide for game {GameId} with {StepCount} steps, estimated {Minutes} min",
                gameId, allSteps.Count, estimatedTime);

            var response = new SetupGuideResponse(
                game.Name,
                allSteps,
                estimatedTime,
                promptTokens,
                completionTokens,
                totalTokens,
                confidence
            );

            // AI-05: Cache the response for future requests
            await _cache.SetAsync(cacheKey, response, 86400, cancellationToken);

            return response;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating setup guide for game {GameId}", gameId);
            return CreateEmptySetupGuide("Unknown Game");
        }
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
                        var numberMatch = System.Text.RegularExpressions.Regex.Match(stepPart, @"\d+");
                        if (numberMatch.Success && int.TryParse(numberMatch.Value, out int stepNum))
                        {
                            currentStepNumber = stepNum;
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing LLM steps response");
        }

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
    /// Create default setup steps when no RAG data available
    /// </summary>
    private List<SetupGuideStep> CreateDefaultSetupSteps()
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

    /// <summary>
    /// Create empty setup guide response
    /// </summary>
    private SetupGuideResponse CreateEmptySetupGuide(string gameTitle)
    {
        return new SetupGuideResponse(
            gameTitle,
            CreateDefaultSetupSteps(),
            10 // default estimated time
        );
    }
}
