using Api.BoundedContexts.Administration.Domain.ValueObjects;

namespace Api.BoundedContexts.Administration.Domain.Services;

/// <summary>
/// Generates game recommendations using RAG (Retrieval-Augmented Generation) with pgvector embeddings.
/// </summary>
public interface IRAGRecommender
{
    /// <summary>
    /// Recommends similar games based on user's top favorites using vector similarity search.
    /// </summary>
    /// <param name="userId">Target user ID</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>List of recommendation insights (0-3 recommendations)</returns>
    /// <remarks>
    /// Uses pgvector to find games with similar embeddings to user's favorite games.
    /// Falls back to empty list if vector search service is unavailable.
    /// </remarks>
    Task<List<AIInsight>> RecommendSimilarGamesAsync(Guid userId, CancellationToken cancellationToken = default);
}
