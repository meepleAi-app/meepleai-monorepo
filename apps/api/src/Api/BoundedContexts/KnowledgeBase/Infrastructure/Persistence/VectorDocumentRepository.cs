using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IVectorDocumentRepository.
/// Maps domain VectorDocument entities to persistence layer.
/// </summary>
public class VectorDocumentRepository : IVectorDocumentRepository
{
    private readonly MeepleAiDbContext _context;

    public VectorDocumentRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<VectorDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // TODO: Map from EF entity (VectorDocumentEntity) to domain entity (VectorDocument)
        // This will require mapping logic between persistence and domain models
        throw new NotImplementedException("Mapping from EF entity to domain entity not yet implemented");
    }

    public async Task<VectorDocument?> GetByGameAndSourceAsync(
        Guid gameId,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default)
    {
        // TODO: Query EF context and map to domain entity
        throw new NotImplementedException("Mapping from EF entity to domain entity not yet implemented");
    }

    public async Task<List<VectorDocument>> GetByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        // TODO: Query EF context and map to domain entities
        throw new NotImplementedException("Mapping from EF entity to domain entity not yet implemented");
    }

    public async Task AddAsync(VectorDocument document, CancellationToken cancellationToken = default)
    {
        // TODO: Map from domain entity to EF entity and add to context
        throw new NotImplementedException("Mapping from domain entity to EF entity not yet implemented");
    }

    public async Task UpdateAsync(VectorDocument document, CancellationToken cancellationToken = default)
    {
        // TODO: Map from domain entity to EF entity and update
        throw new NotImplementedException("Mapping from domain entity to EF entity not yet implemented");
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        // Query existing EF entity
        var entity = await _context.VectorDocuments
            .FirstOrDefaultAsync(vd => vd.Id == id.ToString(), cancellationToken);

        if (entity != null)
        {
            _context.VectorDocuments.Remove(entity);
            await _context.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(
        Guid gameId,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.VectorDocuments
            .AnyAsync(vd =>
                vd.GameId == gameId.ToString() &&
                vd.SourceDocumentId == sourceDocumentId.ToString(),
                cancellationToken);
    }

    public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.VectorDocuments.CountAsync(cancellationToken);
    }

    public async Task<int> GetTotalEmbeddingsCountAsync(CancellationToken cancellationToken = default)
    {
        return await _context.VectorDocuments
            .SumAsync(vd => vd.ChunkCount, cancellationToken);
    }
}
