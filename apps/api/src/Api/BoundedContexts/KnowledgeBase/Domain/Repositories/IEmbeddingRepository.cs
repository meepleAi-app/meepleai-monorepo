using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Repositories;

/// <summary>
/// Repository interface for Embedding entities.
/// Provides access to embeddings for search and retrieval operations.
/// </summary>
internal interface IEmbeddingRepository
{
    /// <summary>
    /// Gets an embedding by its unique identifier.
    /// </summary>
    Task<Embedding?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all embeddings for a specific vector document.
    /// </summary>
    Task<List<Embedding>> GetByVectorDocumentIdAsync(
        Guid vectorDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets embeddings for a specific game (across all documents).
    /// </summary>
    Task<List<Embedding>> GetByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs vector similarity search using the provided query vector.
    /// Issue #2051: Supports document filtering via documentIds
    /// </summary>
    Task<List<Embedding>> SearchByVectorAsync(
        Guid gameId,
        Vector queryVector,
        int topK,
        double minScore,
        IReadOnlyList<Guid>? documentIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs vector similarity search across multiple game IDs.
    /// Issue #1661: Cross-game KB search for user-facing global knowledge base.
    /// Delegates to IVectorStoreAdapter.SearchByMultipleGameIdsAsync.
    /// Returns empty list immediately when gameIds is empty (no adapter call).
    /// </summary>
    Task<List<Embedding>> SearchByMultipleGameIdsAsync(
        IReadOnlyList<Guid> gameIds,
        Vector queryVector,
        int topK,
        double minScore,
        IReadOnlyList<Guid>? documentIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Adds a batch of embeddings to the repository.
    /// </summary>
    Task AddBatchAsync(
        List<Embedding> embeddings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all embeddings for a specific vector document.
    /// </summary>
    Task DeleteByVectorDocumentIdAsync(
        Guid vectorDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the count of embeddings for a specific game.
    /// </summary>
    Task<int> GetCountByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs vector similarity search and returns each matching embedding together with
    /// its cosine-similarity score (additive — does NOT replace SearchByVectorAsync).
    /// Issue #1653: F3-FU-4 — per-document scored similarity-search for the admin KB explorer.
    /// </summary>
    Task<IReadOnlyList<ScoredEmbedding>> SearchByVectorWithScoresAsync(
        Guid gameId,
        Vector queryVector,
        int topK,
        double minScore,
        IReadOnlyList<Guid>? documentIds = null,
        CancellationToken cancellationToken = default);
}
