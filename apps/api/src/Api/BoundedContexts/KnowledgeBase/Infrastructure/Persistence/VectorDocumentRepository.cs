using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Mappers;
using Api.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IVectorDocumentRepository.
/// Maps domain VectorDocument entities to persistence layer using KnowledgeBaseMappers.
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
        var entity = await _context.VectorDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(vd => vd.Id == id, cancellationToken);

        return entity?.ToDomain();
    }

    public async Task<VectorDocument?> GetByGameAndSourceAsync(
        Guid gameId,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default)
    {
        var entity = await _context.VectorDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(vd =>
                vd.GameId == gameId &&
                vd.PdfDocumentId == sourceDocumentId,
                cancellationToken);

        return entity?.ToDomain();
    }

    public async Task<List<VectorDocument>> GetByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var entities = await _context.VectorDocuments
            .AsNoTracking()
            .Where(vd => vd.GameId == gameId)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToDomain()).ToList();
    }

    public async Task AddAsync(VectorDocument document, CancellationToken cancellationToken = default)
    {
        var entity = document.ToEntity();
        await _context.VectorDocuments.AddAsync(entity, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(VectorDocument document, CancellationToken cancellationToken = default)
    {
        var entity = document.ToEntity();
        _context.VectorDocuments.Update(entity);
        await _context.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await _context.VectorDocuments
            .FirstOrDefaultAsync(vd => vd.Id == id, cancellationToken);

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
                vd.GameId == gameId &&
                vd.PdfDocumentId == sourceDocumentId,
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
