using Api.BoundedContexts.GameManagement.Application.Queries;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.GameManagement.Application.Handlers;

/// <summary>
/// Handles finding similar games using a hybrid approach:
/// 1. Category/Mechanic similarity (content-based filtering)
/// 2. Attribute similarity (player count, complexity, duration)
///
/// Issue #3353: Similar Games Discovery with RAG
///
/// Note: For full semantic similarity via embeddings, a dedicated game embeddings
/// collection would be needed. This implementation uses attribute-based similarity
/// which provides good results without additional infrastructure.
/// </summary>
internal class GetSimilarGamesQueryHandler : IQueryHandler<GetSimilarGamesQuery, GetSimilarGamesResult>
{
    private readonly ISharedGameRepository _sharedGameRepository;
    private readonly IUserLibraryRepository _userLibraryRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetSimilarGamesQueryHandler> _logger;

    public GetSimilarGamesQueryHandler(
        ISharedGameRepository sharedGameRepository,
        IUserLibraryRepository userLibraryRepository,
        MeepleAiDbContext dbContext,
        ILogger<GetSimilarGamesQueryHandler> logger)
    {
        _sharedGameRepository = sharedGameRepository ?? throw new ArgumentNullException(nameof(sharedGameRepository));
        _userLibraryRepository = userLibraryRepository ?? throw new ArgumentNullException(nameof(userLibraryRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<GetSimilarGamesResult> Handle(GetSimilarGamesQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        _logger.LogInformation(
            "Finding similar games for gameId={GameId}, topK={TopK}, minSimilarity={MinSimilarity}",
            query.GameId, query.TopK, query.MinSimilarity);

        // 1. Get source game with related entities
        var sourceGame = await _sharedGameRepository.GetByIdAsync(query.GameId, cancellationToken)
            .ConfigureAwait(false);

        if (sourceGame == null)
        {
            throw new NotFoundException($"Game with ID {query.GameId} not found");
        }

        // 2. Get user's owned game IDs to filter out (if userId provided)
        HashSet<Guid>? ownedGameIds = null;
        if (query.UserId.HasValue)
        {
            var ownedGames = await _userLibraryRepository.GetUserGamesAsync(query.UserId.Value, null, cancellationToken)
                .ConfigureAwait(false);
            ownedGameIds = ownedGames.Select(g => g.GameId).ToHashSet();
            _logger.LogDebug("User {UserId} owns {Count} games to filter out", query.UserId, ownedGameIds.Count);
        }

        // 3. Find similar games using content-based filtering
        var sourceCategoryNames = sourceGame.Categories.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sourceMechanicNames = sourceGame.Mechanics.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);

        // Query candidates that share at least one category or mechanic
        var candidateGames = await _dbContext.Set<SharedGame>()
            .AsNoTracking()
            .Where(g => !g.IsDeleted && g.Id != query.GameId)
            .Include(g => g.Categories)
            .Include(g => g.Mechanics)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // 4. Calculate similarity scores
        var scoredGames = new List<(SharedGame Game, double Score, string Reason)>();

        foreach (var candidate in candidateGames)
        {
            // Skip owned games
            if (ownedGameIds?.Contains(candidate.Id) == true)
                continue;

            var (score, reason) = CalculateSimilarity(sourceGame, candidate, sourceCategoryNames, sourceMechanicNames);

            if (score >= query.MinSimilarity)
            {
                scoredGames.Add((candidate, score, reason));
            }
        }

        // 5. Sort by score and take top K
        var topGames = scoredGames
            .OrderByDescending(g => g.Score)
            .Take(query.TopK)
            .Select(g => new SimilarGameDto(
                Id: g.Game.Id,
                Title: g.Game.Title,
                ThumbnailUrl: g.Game.ThumbnailUrl,
                MinPlayers: g.Game.MinPlayers,
                MaxPlayers: g.Game.MaxPlayers,
                PlayingTimeMinutes: g.Game.PlayingTimeMinutes,
                ComplexityRating: g.Game.ComplexityRating,
                AverageRating: g.Game.AverageRating,
                SimilarityScore: g.Score,
                SimilarityReason: g.Reason))
            .ToList();

        _logger.LogInformation("Found {Count} similar games for {Title}", topGames.Count, sourceGame.Title);

        return new GetSimilarGamesResult(topGames, query.GameId, sourceGame.Title);
    }

    /// <summary>
    /// Calculates similarity score between source and candidate game.
    /// Uses weighted combination of:
    /// - Category overlap (40%)
    /// - Mechanic overlap (30%)
    /// - Player count similarity (15%)
    /// - Complexity similarity (10%)
    /// - Duration similarity (5%)
    /// </summary>
    private static (double Score, string Reason) CalculateSimilarity(
        SharedGame source,
        SharedGame candidate,
        HashSet<string> sourceCategoryNames,
        HashSet<string> sourceMechanicNames)
    {
        var reasons = new List<string>();
        var totalScore = 0.0;

        // Category similarity (Jaccard index, 40% weight)
        var candidateCategoryNames = candidate.Categories.Select(c => c.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sharedCategories = sourceCategoryNames.Intersect(candidateCategoryNames, StringComparer.OrdinalIgnoreCase).ToList();
        var unionCategories = sourceCategoryNames.Union(candidateCategoryNames, StringComparer.OrdinalIgnoreCase).Count();

        if (unionCategories > 0)
        {
            var categoryScore = (double)sharedCategories.Count / unionCategories;
            totalScore += categoryScore * 0.40;

            if (sharedCategories.Count > 0)
            {
                reasons.Add($"Categorie: {string.Join(", ", sharedCategories.Take(2))}");
            }
        }

        // Mechanic similarity (Jaccard index, 30% weight)
        var candidateMechanicNames = candidate.Mechanics.Select(m => m.Name).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var sharedMechanics = sourceMechanicNames.Intersect(candidateMechanicNames, StringComparer.OrdinalIgnoreCase).ToList();
        var unionMechanics = sourceMechanicNames.Union(candidateMechanicNames, StringComparer.OrdinalIgnoreCase).Count();

        if (unionMechanics > 0)
        {
            var mechanicScore = (double)sharedMechanics.Count / unionMechanics;
            totalScore += mechanicScore * 0.30;

            if (sharedMechanics.Count > 0)
            {
                reasons.Add($"Meccaniche: {string.Join(", ", sharedMechanics.Take(2))}");
            }
        }

        // Player count similarity (15% weight)
        // Score based on how much the player ranges overlap
        var playerScore = CalculateRangeOverlap(
            source.MinPlayers, source.MaxPlayers,
            candidate.MinPlayers, candidate.MaxPlayers);
        totalScore += playerScore * 0.15;

        if (playerScore > 0.7)
        {
            reasons.Add($"Giocatori: {candidate.MinPlayers}-{candidate.MaxPlayers}");
        }

        // Complexity similarity (10% weight)
        if (source.ComplexityRating.HasValue && candidate.ComplexityRating.HasValue)
        {
            var complexityDiff = Math.Abs((double)source.ComplexityRating.Value - (double)candidate.ComplexityRating.Value);
            var complexityScore = Math.Max(0, 1 - complexityDiff / 4.0); // Max diff is 4 (1-5 scale)
            totalScore += complexityScore * 0.10;

            if (complexityScore > 0.8)
            {
                reasons.Add($"Complessità: {candidate.ComplexityRating:F1}");
            }
        }
        else
        {
            // Give partial score if complexity unknown
            totalScore += 0.05;
        }

        // Duration similarity (5% weight)
        var durationScore = CalculateDurationSimilarity(source.PlayingTimeMinutes, candidate.PlayingTimeMinutes);
        totalScore += durationScore * 0.05;

        if (durationScore > 0.8)
        {
            reasons.Add($"Durata: ~{candidate.PlayingTimeMinutes} min");
        }

        // Build final reason string
        var reasonText = reasons.Count > 0
            ? string.Join(" • ", reasons.Take(3))
            : "Stile di gioco simile";

        return (totalScore, reasonText);
    }

    /// <summary>
    /// Calculates overlap between two numeric ranges (0-1 scale).
    /// </summary>
    private static double CalculateRangeOverlap(int min1, int max1, int min2, int max2)
    {
        var overlapStart = Math.Max(min1, min2);
        var overlapEnd = Math.Min(max1, max2);

        if (overlapStart > overlapEnd)
            return 0;

        var overlap = overlapEnd - overlapStart + 1;
        var totalRange = Math.Max(max1, max2) - Math.Min(min1, min2) + 1;

        return (double)overlap / totalRange;
    }

    /// <summary>
    /// Calculates similarity between two durations (0-1 scale).
    /// Uses logarithmic scale since duration differences matter less for longer games.
    /// </summary>
    private static double CalculateDurationSimilarity(int duration1, int duration2)
    {
        if (duration1 <= 0 || duration2 <= 0)
            return 0.5; // Unknown, give neutral score

        // Use log scale for comparison
        var log1 = Math.Log(duration1 + 1);
        var log2 = Math.Log(duration2 + 1);
        var logDiff = Math.Abs(log1 - log2);

        // Normalize: log(241) - log(1) ≈ 5.49 is max practical diff (1 min to 240 min)
        return Math.Max(0, 1 - logDiff / 2.5);
    }
}
