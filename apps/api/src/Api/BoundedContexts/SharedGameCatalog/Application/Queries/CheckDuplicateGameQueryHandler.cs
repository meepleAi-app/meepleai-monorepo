using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Utilities.StringSimilarity;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for enhanced duplicate detection using BggId exact match and Levenshtein fuzzy title matching.
/// Supports the PDF wizard flow by identifying potential duplicates before game import.
/// Issue #4158: Backend - Duplicate Detection Enhancement
/// </summary>
internal sealed class CheckDuplicateGameQueryHandler : IRequestHandler<CheckDuplicateGameQuery, DuplicateCheckResultDto>
{
    private readonly ISharedGameRepository _repository;
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<CheckDuplicateGameQueryHandler> _logger;

    /// <summary>
    /// Minimum similarity threshold for considering a game as potential duplicate.
    /// Games with title similarity ≥70% are flagged for review.
    /// </summary>
    private const double SimilarityThreshold = 70.0;

    /// <summary>
    /// Maximum number of fuzzy duplicate suggestions to return.
    /// Prevents overwhelming the user with too many options.
    /// </summary>
    private const int MaxFuzzyDuplicates = 5;

    public CheckDuplicateGameQueryHandler(
        ISharedGameRepository repository,
        MeepleAiDbContext context,
        ILogger<CheckDuplicateGameQueryHandler> logger)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<DuplicateCheckResultDto> Handle(
        CheckDuplicateGameQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Checking duplicates for Title='{Title}', BggId={BggId}",
            query.Title, query.BggId);

        // Step 1: Check for exact duplicate by BggId (if provided)
        var (hasExact, exactId, exactTitle) = await CheckExactDuplicateByBggIdAsync(
            query.BggId,
            cancellationToken).ConfigureAwait(false);

        // Step 2: Check for fuzzy duplicates by title similarity using Levenshtein distance
        var fuzzyDuplicates = await FindFuzzyDuplicatesByTitleAsync(
            query.Title,
            exactId, // Exclude exact match from fuzzy results
            cancellationToken).ConfigureAwait(false);

        // Step 3: Determine recommended action based on duplicate detection results
        var recommendedAction = DetermineRecommendedAction(hasExact, fuzzyDuplicates.Count > 0);

        _logger.LogInformation(
            "Duplicate check complete: ExactMatch={HasExact}, FuzzyMatches={FuzzyCount}, Recommended={Action}",
            hasExact, fuzzyDuplicates.Count, recommendedAction);

        return new DuplicateCheckResultDto(
            HasExactDuplicate: hasExact,
            ExactDuplicateId: exactId,
            ExactDuplicateTitle: exactTitle,
            HasFuzzyDuplicates: fuzzyDuplicates.Count > 0,
            FuzzyDuplicates: fuzzyDuplicates,
            RecommendedAction: recommendedAction);
    }

    /// <summary>
    /// Checks for exact duplicate by BGG ID.
    /// </summary>
    private async Task<(bool HasExact, Guid? ExactId, string? ExactTitle)> CheckExactDuplicateByBggIdAsync(
        int? bggId,
        CancellationToken cancellationToken)
    {
        if (!bggId.HasValue)
        {
            _logger.LogDebug("No BggId provided, skipping exact duplicate check");
            return (false, null, null);
        }

        var exactMatch = await _repository.GetByBggIdAsync(
            bggId.Value,
            cancellationToken).ConfigureAwait(false);

        if (exactMatch != null)
        {
            _logger.LogWarning(
                "Exact BggId duplicate found: BggId={BggId} → SharedGame {GameId} '{Title}'",
                bggId, exactMatch.Id, exactMatch.Title);

            return (true, exactMatch.Id, exactMatch.Title);
        }

        return (false, null, null);
    }

    /// <summary>
    /// Finds fuzzy duplicates by calculating Levenshtein similarity for all shared game titles.
    /// Returns top N candidates with similarity ≥ threshold, sorted by similarity descending.
    /// </summary>
    private async Task<List<FuzzyDuplicateDto>> FindFuzzyDuplicatesByTitleAsync(
        string targetTitle,
        Guid? excludeGameId,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(targetTitle))
        {
            _logger.LogDebug("Empty title provided, skipping fuzzy duplicate check");
            return [];
        }

        // Fetch all shared game titles for fuzzy matching
        // Note: For large catalogs (>10k games), consider implementing database-level fuzzy search
        // Global query filter (HasQueryFilter) automatically excludes soft-deleted games
        var allGames = await _context.SharedGames
            .AsNoTracking()
            .Select(g => new { g.Id, g.Title, g.YearPublished })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var fuzzyMatches = allGames
            .Where(g => excludeGameId == null || g.Id != excludeGameId) // Exclude exact match if found
            .Select(g => new
            {
                g.Id,
                g.Title,
                g.YearPublished,
                Similarity = LevenshteinDistance.CalculateSimilarityScore(targetTitle, g.Title)
            })
            .Where(x => x.Similarity >= SimilarityThreshold)
            .OrderByDescending(x => x.Similarity)
            .Take(MaxFuzzyDuplicates)
            .Select(x => new FuzzyDuplicateDto(
                SharedGameId: x.Id,
                Title: x.Title,
                YearPublished: x.YearPublished,
                SimilarityScore: (int)Math.Round(x.Similarity)))
            .ToList();

        if (fuzzyMatches.Count > 0)
        {
            _logger.LogWarning(
                "Found {Count} fuzzy duplicates for title '{Title}' (similarity ≥{Threshold}%)",
                fuzzyMatches.Count, targetTitle, SimilarityThreshold);
        }
        else
        {
            _logger.LogInformation(
                "No fuzzy duplicates found for title '{Title}' (similarity threshold: {Threshold}%)",
                targetTitle, SimilarityThreshold);
        }

        return fuzzyMatches;
    }

    /// <summary>
    /// Determines the recommended approval action based on duplicate detection results.
    /// </summary>
    private static ProposalApprovalAction DetermineRecommendedAction(
        bool hasExactDuplicate,
        bool hasFuzzyDuplicates)
    {
        if (hasExactDuplicate)
        {
            // Exact BggId match found - recommend merging knowledge base
            return ProposalApprovalAction.MergeKnowledgeBase;
        }

        if (hasFuzzyDuplicates)
        {
            // Similar titles found - recommend creating as variant or reviewing manually
            return ProposalApprovalAction.ApproveAsVariant;
        }

        // No duplicates - safe to approve as new game
        return ProposalApprovalAction.ApproveAsNew;
    }
}
