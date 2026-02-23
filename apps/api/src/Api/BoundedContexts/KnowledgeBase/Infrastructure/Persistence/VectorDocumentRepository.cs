using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
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
internal class VectorDocumentRepository : RepositoryBase, IVectorDocumentRepository
{
    public VectorDocumentRepository(MeepleAiDbContext context, IDomainEventCollector eventCollector)
        : base(context, eventCollector)
    {
    }

    public async Task<VectorDocument?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.VectorDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(vd => vd.Id == id, cancellationToken).ConfigureAwait(false);

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
                cancellationToken).ConfigureAwait(false);

        return entity?.ToDomain();
    }

    public async Task<List<VectorDocument>> GetByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.VectorDocuments
            .AsNoTracking()
            .Where(vd => vd.GameId == gameId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(e => e.ToDomain()).ToList();
    }

    public async Task AddAsync(VectorDocument document, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(document);
        var entity = document.ToEntity();
        await DbContext.VectorDocuments.AddAsync(entity, cancellationToken).ConfigureAwait(false);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(VectorDocument document, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(document);
        var entity = document.ToEntity();
        DbContext.VectorDocuments.Update(entity);
        await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task DeleteAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.VectorDocuments
            .FirstOrDefaultAsync(vd => vd.Id == id, cancellationToken).ConfigureAwait(false);

        if (entity != null)
        {
            DbContext.VectorDocuments.Remove(entity);
            await DbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
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
                cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> GetTotalCountAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.VectorDocuments.CountAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<int> GetTotalEmbeddingsCountAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.VectorDocuments
            .SumAsync(vd => vd.ChunkCount, cancellationToken).ConfigureAwait(false);
    }

    public async Task<VectorDocumentIndexingInfo?> GetIndexingInfoByGameIdAsync(
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var info = await DbContext.VectorDocuments
            .AsNoTracking()
            .Where(vd => vd.GameId == gameId)
            // Null IndexedAt (pending/processing) sorts to top so in-progress trumps older completed docs.
            .OrderByDescending(vd => vd.IndexedAt ?? DateTime.MaxValue)
            .Select(vd => new { vd.IndexingStatus, vd.ChunkCount, vd.IndexingError })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (info is null)
            return null;

        // Parse the raw persistence string to the typed enum (case-insensitive for robustness).
        var status = Enum.Parse<VectorDocumentIndexingStatus>(info.IndexingStatus, ignoreCase: true);
        return new VectorDocumentIndexingInfo(status, info.ChunkCount, info.IndexingError);
    }

    public async Task<bool> AnyBelongsToGameAsync(
        IEnumerable<Guid> ids,
        Guid gameId,
        CancellationToken cancellationToken = default)
    {
        var idList = ids.ToList();
        if (idList.Count == 0)
            return false;

        return await DbContext.VectorDocuments
            .AnyAsync(vd => idList.Contains(vd.Id) && vd.GameId == gameId, cancellationToken)
            .ConfigureAwait(false);
    }
}
