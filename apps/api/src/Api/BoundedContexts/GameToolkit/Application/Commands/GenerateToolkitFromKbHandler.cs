using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Application.Validators;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure.Entities;
using Api.Middleware.Exceptions;
using Api.Services;
using Api.SharedKernel.Application;
using Api.SharedKernel.Domain.ValueObjects;
using FluentValidation;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameToolkit.Application.Commands;

/// <summary>
/// Generates an AI-powered toolkit suggestion by querying KB vector chunks
/// for game rules and asking the LLM to extract mechanical components.
/// Returns the suggestion DTO (not persisted — caller reviews and applies).
/// </summary>
internal class GenerateToolkitFromKbHandler
    : IRequestHandler<GenerateToolkitFromKbCommand, AiToolkitSuggestionDto>
{
    private const float ConfidenceThreshold = 0.6f;
    private const int MinChunksForFullConfidence = 5;
    private const int MaxChunksPerQuery = 5;
    private const int MaxTotalChunks = 15;

    private static readonly string[] ExtractionQueries =
    [
        "dice requirements types quantity faces rolling",
        "counters resources tokens points tracking per player",
        "timer time limit turn duration countdown",
        "scoring points victory conditions ranking dimensions",
        "turn order round phases sequence players"
    ];

    private readonly IHybridSearchService _hybridSearchService;
    private readonly ILlmService _llmService;
    private readonly IRagAccessService _ragAccessService;
    private readonly IGameCoreDataProvider _gameCoreData;
    private readonly IValidator<AiToolkitSuggestionDto> _suggestionValidator;
    private readonly ILogger<GenerateToolkitFromKbHandler> _logger;

    public GenerateToolkitFromKbHandler(
        IHybridSearchService hybridSearchService,
        ILlmService llmService,
        IRagAccessService ragAccessService,
        IGameCoreDataProvider gameCoreData,
        ILogger<GenerateToolkitFromKbHandler> logger,
        IValidator<AiToolkitSuggestionDto>? suggestionValidator = null)
    {
        _hybridSearchService = hybridSearchService ?? throw new ArgumentNullException(nameof(hybridSearchService));
        _llmService = llmService ?? throw new ArgumentNullException(nameof(llmService));
        _ragAccessService = ragAccessService ?? throw new ArgumentNullException(nameof(ragAccessService));
        _gameCoreData = gameCoreData ?? throw new ArgumentNullException(nameof(gameCoreData));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        // Validator is optional with default fallback to allow legacy DI registrations
        // (existing tests didn't pass a validator). Use AiToolkitSuggestionValidator as default.
        _suggestionValidator = suggestionValidator ?? new AiToolkitSuggestionValidator();
    }

    public async Task<AiToolkitSuggestionDto> Handle(
        GenerateToolkitFromKbCommand command,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);

        // 1. Validate game exists
        var coreData = await _gameCoreData
            .GetCoreDataAsync(GameRef.Shared(command.GameId), cancellationToken)
            .ConfigureAwait(false);

        if (coreData is null)
            throw new NotFoundException("Game", command.GameId.ToString());

        // 2. Resolve accessible KB card IDs for this user/game
        var accessibleCardIds = await _ragAccessService
            .GetAccessibleKbCardsAsync(command.UserId, command.GameId, UserRole.Admin, cancellationToken)
            .ConfigureAwait(false);

        if (accessibleCardIds.Count == 0)
            throw new InvalidOperationException(
                $"No documents found in knowledge base for game {command.GameId}. Upload and index PDF rulebooks first.");

        // 3. Fan-out hybrid search across extraction query categories
        var searchTasks = ExtractionQueries.Select(q =>
            _hybridSearchService.SearchAsync(
                q, command.GameId, SearchMode.Hybrid, MaxChunksPerQuery,
                accessibleCardIds, vectorWeight: 0.7f, keywordWeight: 0.3f,
                keywordMinScore: 0.01,
                cancellationToken: cancellationToken));

        var allResults = await Task.WhenAll(searchTasks).ConfigureAwait(false);

        // 4. Deduplicate by ChunkId, keep top N by score
        var uniqueChunks = allResults
            .SelectMany(r => r)
            .GroupBy(r => r.ChunkId, StringComparer.Ordinal)
            .Select(g => g.OrderByDescending(r => r.HybridScore).First())
            .OrderByDescending(r => r.HybridScore)
            .Take(MaxTotalChunks)
            .ToList();

        _logger.LogInformation(
            "GenerateToolkitFromKb: retrieved {ChunkCount} unique chunks for game {GameId}",
            uniqueChunks.Count, command.GameId);

        // 5. Build prompts
        var gameTitle = coreData.Title;
        var userPrompt = BuildUserPrompt(gameTitle, uniqueChunks);

        // 6. Call LLM (primary attempt)
        var suggestion = await _llmService
            .GenerateJsonAsync<AiToolkitSuggestionDto>(
                ToolkitExtractionPrompts.SystemPrompt, userPrompt,
                RequestSource.RagPipeline, cancellationToken)
            .ConfigureAwait(false);

        // 7. Retry with simplified prompt if primary returned null
        if (suggestion is null)
        {
            _logger.LogWarning(
                "Primary LLM call returned null for game {GameId}, retrying with simplified prompt",
                command.GameId);

            suggestion = await _llmService
                .GenerateJsonAsync<AiToolkitSuggestionDto>(
                    ToolkitExtractionPrompts.RetrySystemPrompt, userPrompt,
                    RequestSource.RagPipeline, cancellationToken)
                .ConfigureAwait(false);
        }

        if (suggestion is null)
            throw new InvalidOperationException(
                $"LLM failed to generate a valid toolkit suggestion for game {command.GameId} after retry.");

        // 7b. Validate LLM output against schema invariants (issue #1747 B19-3c).
        // We do NOT throw on validation failure — we log, force human review, and return the
        // suggestion anyway so an admin can correct it via ApplyAiToolkitSuggestionCommand.
        var validation = await _suggestionValidator
            .ValidateAsync(suggestion, cancellationToken)
            .ConfigureAwait(false);

        var validationFailed = !validation.IsValid;
        if (validationFailed)
        {
            var errors = string.Join(" | ",
                validation.Errors.Select(e => $"{e.PropertyName}: {e.ErrorMessage}"));
            _logger.LogWarning(
                "AI toolkit suggestion failed schema validation for game {GameId}. Errors: {ValidationErrors}. Forcing RequiresHumanReview=true.",
                command.GameId, errors);
        }

        // 8. Compute confidence metrics
        var avgScore = uniqueChunks.Count > 0
            ? uniqueChunks.Average(c => c.HybridScore)
            : 0f;
        var coverageFactor = Math.Min(1f, (float)uniqueChunks.Count / MinChunksForFullConfidence);
        var confidenceScore = avgScore * coverageFactor;
        // Force review if either confidence is low OR schema validation failed.
        var requiresHumanReview = confidenceScore < ConfidenceThreshold || validationFailed;

        _logger.LogInformation(
            "GenerateToolkitFromKb: game={GameId} chunks={Chunks} avgScore={AvgScore:F2} confidence={Confidence:F2} validationOk={ValidationOk} requiresReview={RequiresReview}",
            command.GameId, uniqueChunks.Count, avgScore, confidenceScore, !validationFailed, requiresHumanReview);

        return suggestion with
        {
            ConfidenceScore = confidenceScore,
            ChunksAnalyzed = uniqueChunks.Count,
            KbCoveragePercent = coverageFactor * 100f,
            RequiresHumanReview = requiresHumanReview
        };
    }

    private static string BuildUserPrompt(string gameTitle, IReadOnlyList<HybridSearchResult> chunks)
    {
        var contextParts = chunks.Select((c, i) =>
            $"[{i + 1}] (score={c.HybridScore:F2})\n{c.Content}");

        return
            $"Game: {gameTitle}\n\n" +
            $"Rulebook excerpts:\n{string.Join("\n\n---\n\n", contextParts)}\n\n" +
            $"Extract the toolkit configuration for \"{gameTitle}\" from the excerpts above.";
    }
}
