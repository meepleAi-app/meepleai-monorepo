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
        // First: direct match on VectorDocument.GameId (shared games)
        var info = await DbContext.VectorDocuments
            .AsNoTracking()
            .Where(vd => vd.GameId == gameId)
            // Null IndexedAt (pending/processing) sorts to top so in-progress trumps older completed docs.
            .OrderByDescending(vd => vd.IndexedAt ?? DateTime.MaxValue)
            .Select(vd => new { vd.IndexingStatus, vd.ChunkCount, vd.IndexingError })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        // Fallback: join via PdfDocumentEntity.PrivateGameId (private games have GameId = null)
        if (info is null)
        {
            info = await (
                from pdf in DbContext.PdfDocuments.AsNoTracking()
                join vd in DbContext.VectorDocuments.AsNoTracking()
                    on pdf.Id equals vd.PdfDocumentId
                where pdf.PrivateGameId == gameId
                orderby vd.IndexedAt == null ? DateTime.MaxValue : vd.IndexedAt descending
                select new { vd.IndexingStatus, vd.ChunkCount, vd.IndexingError }
            ).FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        }

        if (info is not null)
        {
            // Parse the raw persistence string to the typed enum (case-insensitive for robustness).
            var status = Enum.Parse<VectorDocumentIndexingStatus>(info.IndexingStatus, ignoreCase: true);
            return new VectorDocumentIndexingInfo(status, info.ChunkCount, info.IndexingError);
        }

        // Third fallback: VectorDocument not yet created (PDF still in pipeline).
        // Read PdfDocumentEntity.ProcessingState (authoritative 7-state field) so polling
        // returns "processing" instead of 404 during extraction/chunking/embedding.
        // Covers both:
        //   - Private games (PrivateGameId == gameId)
        //   - Shared catalog games (GameId == gameId, Issue #5217)
        var pdfProcessingState = await DbContext.PdfDocuments
            .AsNoTracking()
            .Where(pdf => pdf.PrivateGameId == gameId || pdf.GameId == gameId)
            // Order by most recently uploaded so multi-PDF games return the latest state.
            .OrderByDescending(pdf => pdf.UploadedAt)
            .Select(pdf => pdf.ProcessingState)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pdfProcessingState is not null)
        {
            var inferredStatus = pdfProcessingState.ToLowerInvariant() switch
            {
                "failed" => VectorDocumentIndexingStatus.Failed,
                "ready" => VectorDocumentIndexingStatus.Completed,       // ProcessingState.Ready = fully indexed
                "pending" => VectorDocumentIndexingStatus.Pending,
                _ => VectorDocumentIndexingStatus.Processing             // uploading/extracting/chunking/embedding/indexing
            };
            return new VectorDocumentIndexingInfo(inferredStatus, 0, null);
        }

        // Fourth fallback: the caller passed a SharedGame.Id (catalog game ID), but
        // UploadPdfCommandHandler.FindOrCreateGameAsync resolves it to an internal
        // GameEntity.Id (games table) before saving PdfDocument.GameId.
        // So PdfDocument.GameId = internalId != sharedGameId.
        // Resolve via: GameEntity.SharedGameId == gameId → GameEntity.Id → PdfDocument.GameId.
        var internalGameIds = await DbContext.Games
            .AsNoTracking()
            .Where(g => g.SharedGameId == gameId)
            .Select(g => g.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (internalGameIds.Count == 0)
            return null;

        // Retry VectorDocument lookup with internal game IDs (post-indexing)
        var infoViaInternal = await DbContext.VectorDocuments
            .AsNoTracking()
            .Where(vd => vd.GameId != null && internalGameIds.Contains(vd.GameId.Value))
            .OrderByDescending(vd => vd.IndexedAt ?? DateTime.MaxValue)
            .Select(vd => new { vd.IndexingStatus, vd.ChunkCount, vd.IndexingError })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (infoViaInternal is not null)
        {
            var statusViaInternal = Enum.Parse<VectorDocumentIndexingStatus>(infoViaInternal.IndexingStatus, ignoreCase: true);
            return new VectorDocumentIndexingInfo(statusViaInternal, infoViaInternal.ChunkCount, infoViaInternal.IndexingError);
        }

        // Retry PdfDocument ProcessingState lookup with internal game IDs (in-pipeline)
        var pdfStateViaInternal = await DbContext.PdfDocuments
            .AsNoTracking()
            .Where(pdf => pdf.GameId != null && internalGameIds.Contains(pdf.GameId.Value))
            .Select(pdf => pdf.ProcessingState)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (pdfStateViaInternal is null)
            return null;

        var inferredStatusViaInternal = pdfStateViaInternal.ToLowerInvariant() switch
        {
            "failed" => VectorDocumentIndexingStatus.Failed,
            "ready" => VectorDocumentIndexingStatus.Completed,
            "pending" => VectorDocumentIndexingStatus.Pending,
            _ => VectorDocumentIndexingStatus.Processing
        };
        return new VectorDocumentIndexingInfo(inferredStatusViaInternal, 0, null);
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
