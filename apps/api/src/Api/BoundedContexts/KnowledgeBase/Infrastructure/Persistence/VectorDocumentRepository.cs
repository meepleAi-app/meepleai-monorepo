using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence.Mappers;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of IVectorDocumentRepository.
/// Maps domain VectorDocument entities to persistence layer using KnowledgeBaseMappers.
/// </summary>
public class VectorDocumentRepository : RepositoryBase, IVectorDocumentRepository
{
    public VectorDocumentRepository(MeepleAiDbContext context, IDomainEventCollector eventCollector)
        : base(context, eventCollector)
    {
    }

    public async Task<VectorDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.VectorDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(vd => vd.Id == id, cancellationToken);

        return entity?.ToDomain();
    }

    public async Task<VectorDocument?> GetByGameAndSourceAsync(
        Guid gameId,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.VectorDocuments
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
        var entities = await DbContext.VectorDocuments
            .AsNoTracking()
            .Where(vd => vd.GameId == gameId)
            .ToListAsync(cancellationToken);

        return entities.Select(e => e.ToDomain()).ToList();
    }

    public async Task AddAsync(VectorDocument document, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(document);
        var entity = document.ToEntity();
        await DbContext.VectorDocuments.AddAsync(entity, cancellationToken);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task UpdateAsync(VectorDocument document, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(document);
        var entity = document.ToEntity();
        DbContext.VectorDocuments.Update(entity);
        await DbContext.SaveChangesAsync(cancellationToken);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.VectorDocuments
            .FirstOrDefaultAsync(vd => vd.Id == id, cancellationToken);

        if (entity != null)
        {
            DbContext.VectorDocuments.Remove(entity);
            await DbContext.SaveChangesAsync(cancellationToken);
        }
    }

    public async Task<bool> ExistsAsync(
        Guid gameId,
        Guid sourceDocumentId,
        CancellationToken cancellationToken = default)
    {
        return await DbContext.VectorDocuments
            .AnyAsync(vd =>
                vd.GameId == gameId &&
                vd.PdfDocumentId == sourceDocumentId,
                cancellationToken);
    }

    public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.VectorDocuments.CountAsync(cancellationToken);
    }

    public async Task<int> GetTotalEmbeddingsCountAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.VectorDocuments
            .SumAsync(vd => vd.ChunkCount, cancellationToken);
    }
}
