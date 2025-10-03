using Api.Infrastructure;
using Api.Models;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

/// <summary>
/// AI-03: Service for generating step-by-step setup guides using RAG
/// </summary>
public class SetupGuideService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQdrantService _qdrantService;
    private readonly ILogger<SetupGuideService> _logger;

    public SetupGuideService(
        MeepleAiDbContext dbContext,
        IEmbeddingService embeddingService,
        IQdrantService qdrantService,
        ILogger<SetupGuideService> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _qdrantService = qdrantService;
        _logger = logger;
    }

    /// <summary>
    /// AI-03: Generate step-by-step setup guide with references
    /// </summary>
    public async Task<SetupGuideResponse> GenerateSetupGuideAsync(
        string tenantId,
        string gameId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get game information
            var game = await _dbContext.Games
                .Where(g => g.Id == gameId && g.TenantId == tenantId)
                .FirstOrDefaultAsync(cancellationToken);

            if (game == null)
            {
                _logger.LogWarning("Game not found: {GameId} for tenant {TenantId}", gameId, tenantId);
                return CreateEmptySetupGuide("Unknown Game");
            }

            // Query RAG for setup-related content
            var setupQueries = new[]
            {
                "game setup",
                "preparation",
                "initial setup",
                "components placement",
                "player setup"
            };

            var allSteps = new List<SetupGuideStep>();
            var stepNumber = 1;

            foreach (var query in setupQueries)
            {
                var setupInfo = await QuerySetupInformationAsync(tenantId, gameId, query, cancellationToken);

                if (setupInfo.snippets.Count > 0)
                {
                    // Create step from retrieved information
                    var step = CreateSetupStep(
                        stepNumber++,
                        FormatStepTitle(query),
                        setupInfo.answer,
                        setupInfo.snippets);

                    allSteps.Add(step);
                }
            }

            // If no steps found via RAG, create default steps
            if (allSteps.Count == 0)
            {
                allSteps = CreateDefaultSetupSteps();
            }

            // Estimate setup time (1-2 minutes per step)
            var estimatedTime = allSteps.Count * 2;

            _logger.LogInformation(
                "Generated setup guide for game {GameId} with {StepCount} steps",
                gameId, allSteps.Count);

            return new SetupGuideResponse(
                game.Name,
                allSteps,
                estimatedTime
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error generating setup guide for game {GameId}", gameId);
            return CreateEmptySetupGuide("Unknown Game");
        }
    }

    /// <summary>
    /// Query RAG system for setup-related information
    /// </summary>
    private async Task<(string answer, List<Snippet> snippets)> QuerySetupInformationAsync(
        string tenantId,
        string gameId,
        string query,
        CancellationToken cancellationToken)
    {
        try
        {
            // Generate embedding for the query
            var embeddingResult = await _embeddingService.GenerateEmbeddingAsync(query, cancellationToken);
            if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            {
                return (string.Empty, new List<Snippet>());
            }

            var queryEmbedding = embeddingResult.Embeddings[0];

            // Search Qdrant for similar chunks
            var searchResult = await _qdrantService.SearchAsync(
                tenantId,
                gameId,
                queryEmbedding,
                limit: 2,
                cancellationToken);

            if (!searchResult.Success || searchResult.Results.Count == 0)
            {
                return (string.Empty, new List<Snippet>());
            }

            // Build snippets from results
            var snippets = searchResult.Results.Select(r => new Snippet(
                r.Text,
                $"PDF:{r.PdfId}",
                r.Page,
                0 // line number not tracked in chunks
            )).ToList();

            // Use top result as the answer
            var answer = searchResult.Results[0].Text;

            return (answer, snippets);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error querying setup information for: {Query}", query);
            return (string.Empty, new List<Snippet>());
        }
    }

    /// <summary>
    /// Create a setup step from RAG results
    /// </summary>
    private SetupGuideStep CreateSetupStep(
        int stepNumber,
        string title,
        string instruction,
        List<Snippet> references)
    {
        // Clean up instruction text
        var cleanInstruction = CleanupInstruction(instruction);

        return new SetupGuideStep(
            stepNumber,
            title,
            cleanInstruction,
            references,
            isOptional: false
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
    /// Format query as step title
    /// </summary>
    private string FormatStepTitle(string query)
    {
        // Capitalize first letter
        if (string.IsNullOrEmpty(query))
        {
            return "Setup Step";
        }

        return char.ToUpper(query[0]) + query.Substring(1);
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
