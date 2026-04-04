using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IEmbeddingRepository.
/// Embeddings are stored in PostgreSQL via pgvector (IVectorStoreAdapter → PgVectorStoreAdapter).
/// </summary>
internal class EmbeddingRepository : RepositoryBase, IEmbeddingRepository
{
    private readonly IVectorStoreAdapter _vectorStore;

    public EmbeddingRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector,
        IVectorStoreAdapter vectorStore)
        : base(dbContext, eventCollector)
    {
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
    }

    public async Task<Embedding?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Direct lookup by embedding ID is not used — pgvector search is vector-based
        // Return null; callers rely on SearchByVectorAsync for retrieval
        return await Task.FromResult<Embedding?>(null).ConfigureAwait(false);
    }

    public async Task<List<Embedding>> GetByVectorDocumentIdAsync(
        Guid vectorDocumentId,
        CancellationToken cancellationToken = default)
    {
        // Filtering embeddings by VectorDocument ID is not exposed via IVectorStoreAdapter
        // Use SearchByVectorAsync for actual retrieval
        return await Task.FromResult(new List<Embedding>()).ConfigureAwait(false);
    }

    public async Task<List<Embedding>> GetByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        // Bulk retrieval by gameId is not exposed via IVectorStoreAdapter
        // Use SearchByVectorAsync for similarity-based retrieval
        return await Task.FromResult(new List<Embedding>()).ConfigureAwait(false);
    }

    public async Task<List<Embedding>> SearchByVectorAsync(
        Guid gameId,
        Vector queryVector,
        int topK,
        double minScore,
        IReadOnlyList<Guid>? documentIds = null,
        CancellationToken cancellationToken = default)
    {
        // Delegate to pgvector adapter for vector similarity search
        // Issue #2051: Pass documentIds for filtering
        return await _vectorStore.SearchAsync(
            gameId,
            queryVector,
            topK,
            minScore,
            documentIds,
            cancellationToken).ConfigureAwait(false);
    }

    public async Task AddBatchAsync(
        List<Embedding> embeddings,
        CancellationToken cancellationToken = default)
    {
        // Delegate to pgvector adapter for batch insertion
        await _vectorStore.IndexBatchAsync(embeddings, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteByVectorDocumentIdAsync(
        Guid vectorDocumentId,
        CancellationToken cancellationToken = default)
    {
        // Delegate to pgvector adapter for deletion
        await _vectorStore.DeleteByVectorDocumentIdAsync(vectorDocumentId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> GetCountByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        // Query from PostgreSQL VectorDocuments table
        return await DbContext.VectorDocuments
            .Where(vd => vd.GameId == gameId)
            .SumAsync(vd => vd.ChunkCount, cancellationToken).ConfigureAwait(false);
    }
}
