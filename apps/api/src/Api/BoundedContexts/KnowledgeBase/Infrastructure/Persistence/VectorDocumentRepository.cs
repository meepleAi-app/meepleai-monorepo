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
        // #4244: shared games only, on purpose. This method does not check who is asking and is
        // reached from routes that check no ownership (GET /games/{id}/knowledge-base among them):
        // matching PrivateGameId here would tell anyone who knows the id whether a private game
        // is indexed, and how much. The documents of a private game are read with
        // GetByPrivateGameIdAsync, by callers that have passed the RAG access check.
        var entities = await DbContext.VectorDocuments
            .AsNoTracking()
            .Where(vd => vd.GameId == gameId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(e => e.ToDomain()).ToList();
    }

    public async Task<List<VectorDocument>> GetByPrivateGameIdAsync(
        Guid privateGameId,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.VectorDocuments
            .AsNoTracking()
            .Where(vd => vd.PrivateGameId == privateGameId)
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
        Guid sharedGameId,
        CancellationToken cancellationToken = default)
    {
        // Which document stands for the game when it has several PDFs (#4660, decided 2026-10-28): the PDF
        // uploaded LAST, and its state is read from the PDF itself (ProcessingState, the granular state the
        // pipeline writes at every transition). This is the poll of whoever just uploaded a PDF, so it answers
        // for that PDF: a second PDF that fails shows up even if an older one is indexed, an older failed PDF no
        // longer hides a newer one that completed, and a PDF that fails before its vector document exists is
        // seen. Tie on the upload instant: the higher PDF id wins, so the answer never depends on the order the
        // database hands the rows back.
        //
        // Consequences of reading the PDF, all intended with the rule and none a reason to read the vector
        // document instead: a re-index sends the PDF back to Pending, so the poll says "pending" while the old
        // index still serves; any category of PDF (errata, expansion) can be the last upload; a PDF that the
        // pipeline never moved on from Indexing (a lost concurrency race on the final save) keeps the poll at
        // "processing". Whether the game has a usable knowledge base is a different question, answered by
        // GetKbReadinessQueryHandler and GetGameRagReadinessQueryHandler (they count the Ready PDFs), not here.
        //
        // The game is on the PDF: PdfDocumentEntity.SharedGameId for catalogue games, PrivateGameId for private
        // ones (Task 8: direct resolution, no legacy GameEntity bridge).
        var latestPdf = await DbContext.PdfDocuments
            .AsNoTracking()
            .Where(pdf => pdf.SharedGameId == sharedGameId || pdf.PrivateGameId == sharedGameId)
            .OrderByDescending(pdf => pdf.UploadedAt)
            .ThenByDescending(pdf => pdf.Id)
            .Select(pdf => new { pdf.Id, pdf.ProcessingState })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (latestPdf is not null)
        {
            // The chunk count and the error text belong to the vector document OF THAT PDF; the PDF may have
            // none yet (still extracting) or none at all (failed before it was created).
            var vector = await DbContext.VectorDocuments
                .AsNoTracking()
                .Where(vd => vd.PdfDocumentId == latestPdf.Id) // unique: at most one per PDF
                .Select(vd => new { vd.ChunkCount, vd.IndexingError })
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);

            return new VectorDocumentIndexingInfo(
                MapProcessingState(latestPdf.ProcessingState),
                vector?.ChunkCount ?? 0,
                vector?.IndexingError);
        }

        // No PDF carries the game: what is left is a vector document of the game whose PDF does not carry it
        // (legacy rows from before PdfDocumentEntity.SharedGameId/PrivateGameId). There is no PDF of the game to
        // read the state from, so the document answers: an unfinished one (IndexedAt null: pending, processing,
        // failed) before a completed one, then the most recent.
        var legacy = await DbContext.VectorDocuments
            .AsNoTracking()
            .Where(vd => vd.GameId == sharedGameId)
            .OrderByDescending(vd => vd.IndexedAt ?? DateTime.MaxValue)
            .ThenByDescending(vd => vd.Id)
            .Select(vd => new { vd.IndexingStatus, vd.ChunkCount, vd.IndexingError })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (legacy is null)
            return null;

        // Parse the raw persistence string to the typed enum (case-insensitive for robustness).
        var status = Enum.Parse<VectorDocumentIndexingStatus>(legacy.IndexingStatus, ignoreCase: true);
        return new VectorDocumentIndexingInfo(status, legacy.ChunkCount, legacy.IndexingError);
    }

    private static VectorDocumentIndexingStatus MapProcessingState(string processingState) =>
        processingState.ToLowerInvariant() switch
        {
            "failed" => VectorDocumentIndexingStatus.Failed,
            "ready" => VectorDocumentIndexingStatus.Completed,       // ProcessingState.Ready = fully indexed
            "pending" => VectorDocumentIndexingStatus.Pending,
            _ => VectorDocumentIndexingStatus.Processing             // uploading/extracting/chunking/embedding/indexing
        };

    public async Task<List<Guid>> GetGameIdsWithDocumentsAsync(
        IEnumerable<Guid> gameIds,
        CancellationToken cancellationToken = default)
    {
        var idList = gameIds.ToList();
        if (idList.Count == 0)
            return new List<Guid>();

        return await DbContext.VectorDocuments
            .AsNoTracking()
            .Where(vd => vd.GameId.HasValue && idList.Contains(vd.GameId.Value))
            .Select(vd => vd.GameId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
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
