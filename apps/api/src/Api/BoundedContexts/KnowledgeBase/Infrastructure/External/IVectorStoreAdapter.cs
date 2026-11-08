using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// Adapter interface for vector store operations (currently backed by pgvector).
/// Abstracts vector database details from domain layer.
/// </summary>
internal interface IVectorStoreAdapter
{
    /// <summary>
    /// Performs vector similarity search.
    /// Issue #2051: supports document filtering.
    /// Issue #4243: the filter is a list of <c>pdf_documents.Id</c>, the identity the keyword arm
    /// compares, never <c>vector_documents.Id</c>.
    /// </summary>
    Task<List<Embedding>> SearchAsync(
        Guid gameId,
        Vector queryVector,
        int topK,
        double minScore,
        IReadOnlyList<Guid>? pdfDocumentIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs vector similarity search across multiple game IDs.
    /// Used for session-aware RAG where context includes primary game + expansions.
    /// Issue #5580: Session-aware RAG chat.
    /// </summary>
    Task<List<Embedding>> SearchByMultipleGameIdsAsync(
        IReadOnlyList<Guid> gameIds,
        Vector queryVector,
        int topK,
        double minScore,
        IReadOnlyList<Guid>? pdfDocumentIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Indexes a batch of embeddings into the vector store.
    /// </summary>
    Task IndexBatchAsync(
        List<Embedding> embeddings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Replaces ALL the embeddings of a vector document with <paramref name="embeddings"/>, atomically: the
    /// old ones are deleted and the new ones inserted in ONE transaction, so an insert that fails leaves the
    /// old embeddings in place (#4660). A re-index that deleted first and inserted after, as two separate
    /// statements, left a vector document that says "completed" with no embeddings at all when the insert
    /// failed. If the context already has a transaction open the replacement joins it and follows its outcome.
    /// An empty batch does nothing: there is nothing to replace the index with, and deleting it because of an
    /// upstream mistake is worse than keeping the old one. Every embedding must carry
    /// <paramref name="vectorDocumentId"/>, or the call throws <see cref="ArgumentException"/> before touching
    /// anything.
    /// </summary>
    /// <remarks>
    /// Atomic on the pgvector table only. Whatever else the caller saved before (the text chunks of the same
    /// run, in the queue pipeline) is not part of the transaction: if the insert fails the old embeddings
    /// stay, but their <c>source_chunk_id</c> may point at text chunks that run has already replaced.
    /// </remarks>
    Task ReplaceByVectorDocumentIdAsync(
        Guid vectorDocumentId,
        List<Embedding> embeddings,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes all embeddings for a specific vector document.
    /// </summary>
    Task DeleteByVectorDocumentIdAsync(
        Guid vectorDocumentId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Deletes embeddings whose <c>source_chunk_id</c> is in <paramref name="sourceChunkIds"/>.
    /// Scoped delete (a single chunk's embedding), used to replace one chunk's vector without
    /// wiping the whole document — e.g. the #3435 SP4 per-region table-chunk re-index. No-op on empty.
    /// </summary>
    Task DeleteBySourceChunkIdsAsync(
        IReadOnlyList<Guid> sourceChunkIds,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Performs vector similarity search and returns each result paired with its cosine-similarity
    /// score.  Additive method — does NOT replace <see cref="SearchAsync"/>.
    /// Issue #1653: F3-FU-4 — per-document scored similarity-search.
    /// </summary>
    Task<List<ScoredEmbedding>> SearchWithScoresAsync(
        Guid gameId,
        Vector queryVector,
        int topK,
        double minScore,
        IReadOnlyList<Guid>? pdfDocumentIds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if vector data exists for the given game.
    /// </summary>
    Task<bool> CollectionExistsAsync(
        Guid gameId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Ensures the vector store is ready for the given game.
    /// </summary>
    Task EnsureCollectionExistsAsync(
        Guid gameId,
        int vectorDimension = 768,
        CancellationToken cancellationToken = default);
}
