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
        // TODO: Query from Qdrant and map to domain entity
        throw new NotImplementedException("Qdrant query not yet implemented");
    }

    public async Task<List<Embedding>> GetByVectorDocumentIdAsync(
        Guid vectorDocumentId,
        CancellationToken cancellationToken = default)
    {
        // TODO: Query from Qdrant by vector document ID filter
        throw new NotImplementedException("Qdrant query not yet implemented");
    }

    public async Task<List<Embedding>> GetByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        // TODO: Query from Qdrant by game ID filter
        throw new NotImplementedException("Qdrant query not yet implemented");
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
        await _qdrantAdapter.IndexBatchAsync(embeddings, cancellationToken);
    }

    public async Task DeleteByVectorDocumentIdAsync(
        Guid vectorDocumentId,
        CancellationToken cancellationToken = default)
    {
        // Delegate to Qdrant adapter for deletion
        await _qdrantAdapter.DeleteByVectorDocumentIdAsync(vectorDocumentId, cancellationToken);
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
