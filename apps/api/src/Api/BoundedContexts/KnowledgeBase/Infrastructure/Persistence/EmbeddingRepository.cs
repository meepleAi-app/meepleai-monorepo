using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IEmbeddingRepository.
/// Note: Embeddings are stored in Qdrant, not PostgreSQL.
/// This repository coordinates between domain layer and Qdrant adapter.
/// </summary>
public class EmbeddingRepository : IEmbeddingRepository
{
    private readonly MeepleAiDbContext _context;
    private readonly IQdrantVectorStoreAdapter _qdrantAdapter;

    public EmbeddingRepository(
        MeepleAiDbContext context,
        IQdrantVectorStoreAdapter qdrantAdapter)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _qdrantAdapter = qdrantAdapter ?? throw new ArgumentNullException(nameof(qdrantAdapter));
    }

    public async Task<Embedding?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Note: Qdrant doesn't support direct ID lookup in current implementation
        // Would need to enhance QdrantService with GetPointById method
        // For now, return null as this is rarely used
        return await Task.FromResult<Embedding?>(null).ConfigureAwait(false);
    }

    public async Task<List<Embedding>> GetByVectorDocumentIdAsync(
        Guid vectorDocumentId,
        CancellationToken cancellationToken = default)
    {
        // Note: Current QdrantService doesn't support filtering by vector document ID
        // Would need to add filter capability to SearchAsync
        // For now, return empty list
        return await Task.FromResult(new List<Embedding>()).ConfigureAwait(false);
    }

    public async Task<List<Embedding>> GetByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        // Note: This would require a full scan of Qdrant with gameId filter
        // Not efficient and not supported by current QdrantService
        // Use SearchByVectorAsync with broad query instead
        return await Task.FromResult(new List<Embedding>()).ConfigureAwait(false);
    }

    public async Task<List<Embedding>> SearchByVectorAsync(
        Guid gameId,
        Vector queryVector,
        int topK,
        double minScore,
        CancellationToken cancellationToken = default)
    {
        // Delegate to Qdrant adapter for vector similarity search
        return await _qdrantAdapter.SearchAsync(
            gameId,
            queryVector,
            topK,
            minScore,
            cancellationToken);
    }

    public async Task AddBatchAsync(
        List<Embedding> embeddings,
        CancellationToken cancellationToken = default)
    {
        // Delegate to Qdrant adapter for batch insertion
        await _qdrantAdapter.IndexBatchAsync(embeddings, cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteByVectorDocumentIdAsync(
        Guid vectorDocumentId,
        CancellationToken cancellationToken = default)
    {
        // Delegate to Qdrant adapter for deletion
        await _qdrantAdapter.DeleteByVectorDocumentIdAsync(vectorDocumentId, cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> GetCountByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        // Query from PostgreSQL VectorDocuments table
        return await _context.VectorDocuments
            .Where(vd => vd.GameId == gameId)
            .SumAsync(vd => vd.ChunkCount, cancellationToken);
    }
}
