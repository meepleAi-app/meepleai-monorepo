using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Api.BoundedContexts.Administration.Domain.Services;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.Infrastructure;
using Api.Services;

namespace Api.BoundedContexts.Administration.Infrastructure.Services;

#pragma warning disable S1144 // Unused private fields/methods - Will be used when RAG is implemented
#pragma warning disable S4487 // Unread private fields - Will be used in future RAG implementation

/// <summary>
/// Generates game recommendations using RAG with Qdrant vector similarity search.
/// </summary>
internal sealed class RAGRecommender : IRAGRecommender
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly ILogger<RAGRecommender> _logger;
    private const int TopGamesToAnalyze = 5;

    public RAGRecommender(
        MeepleAiDbContext dbContext,
        IEmbeddingService embeddingService,
        ILogger<RAGRecommender> logger)
    {
        _dbContext = dbContext;
        _embeddingService = embeddingService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<List<AIInsight>> RecommendSimilarGamesAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        try
        {
            // Get user's top favorite games (most played or favorites)
            var topGames = await _dbContext.Set<UserLibraryEntry>()
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .OrderByDescending(e => e.Sessions.Count) // Most played
                .ThenByDescending(e => e.IsFavorite) // Then favorites
                .Take(TopGamesToAnalyze)
                .Select(e => new
                {
                    e.GameId
                })
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            if (topGames.Count == 0)
            {
                _logger.LogInformation(
                    "No top games found for user {UserId}, skipping RAG recommendations",
                    userId);
                return new List<AIInsight>();
            }

            // MVP: Skip RAG for now, return empty (will use rule-based fallback)
            // FUTURE: Implement game embeddings collection and query
            _logger.LogInformation(
                "RAG recommendations not yet implemented for user {UserId}, returning empty",
                userId);
            return new List<AIInsight>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Error generating RAG recommendations for user {UserId}. Falling back to empty insights.",
                userId);
            return new List<AIInsight>(); // Graceful degradation if Qdrant down
        }
    }

    /// <summary>
    /// Averages multiple embedding vectors into a single preference vector.
    /// </summary>
    private static float[] AverageEmbeddings(List<float[]> embeddings)
    {
        if (embeddings.Count == 0)
            throw new ArgumentException("Cannot average empty embeddings list", nameof(embeddings));

        var dimension = embeddings[0].Length;
        var avg = new float[dimension];

        foreach (var embedding in embeddings)
        {
            for (int i = 0; i < dimension; i++)
            {
                avg[i] += embedding[i];
            }
        }

        for (int i = 0; i < dimension; i++)
        {
            avg[i] /= embeddings.Count;
        }

        return avg;
    }
}
