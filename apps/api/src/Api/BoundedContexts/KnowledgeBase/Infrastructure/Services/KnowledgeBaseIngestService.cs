using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Infrastructure.Services;

/// <summary>
/// Persists chunk + embedding pairs from external BCs into the KB vector store.
/// Implements the ACL defined by <see cref="IKnowledgeBaseIngestService"/> to preserve
/// DocumentProcessing ↔ KnowledgeBase BC isolation.
///
/// Strategy: upsert VectorDocument by (gameId, sourceDocumentId), then bulk-insert Embeddings.
/// Idempotent if called multiple times with the same source (existing VD is reused).
///
/// Libro Game AI Assistant MVP — Gap G3 ACL implementation.
/// </summary>
internal sealed class KnowledgeBaseIngestService(
    IVectorDocumentRepository vectorDocumentRepository,
    IEmbeddingRepository embeddingRepository,
    IUnitOfWork unitOfWork,
    ILogger<KnowledgeBaseIngestService> logger) : IKnowledgeBaseIngestService
{
    public async Task<int> IngestChunksAsync(
        Guid sourceDocumentId,
        Guid gameId,
        IReadOnlyList<ChunkIngestionRequest> requests,
        CancellationToken ct = default)
    {
        if (requests.Count == 0) return 0;

        // Find or create VectorDocument for this (game, source) pair.
        var vd = await vectorDocumentRepository
            .GetByGameAndSourceAsync(gameId, sourceDocumentId, ct).ConfigureAwait(false);

        // Determine primary language by majority vote across requests.
        var primaryLanguage = requests
            .GroupBy(r => r.Language, StringComparer.OrdinalIgnoreCase)
            .OrderByDescending(g => g.Count())
            .First().Key;

        if (vd is null)
        {
            vd = new VectorDocument(
                id: Guid.NewGuid(),
                gameId: gameId,
                pdfDocumentId: sourceDocumentId,
                language: primaryLanguage,
                totalChunks: requests.Count);

            // Tag source type for disambiguation (photo batch vs PDF pipeline).
            vd.UpdateMetadata("""{"source_type":"photo_batch"}""");

            await vectorDocumentRepository.AddAsync(vd, ct).ConfigureAwait(false);
        }

        // Map ACL DTOs → internal Embedding domain entities.
        // Issue #1391: photo-batch chunks have no parent TextChunk row → RoleTags
        // defaults to 0 (no role-match boost in semantic-mode search). When/if photo
        // ingestion adopts role classification, propagate the classifier output here.
        var embeddings = requests
            .Select(r => new Embedding(
                id: Guid.NewGuid(),
                vectorDocumentId: vd.Id,
                textContent: r.TextContent,
                vector: new Vector(r.Embedding),
                model: r.EmbeddingModel,
                chunkIndex: r.ChunkIndex,
                pageNumber: r.PageNumber,
                language: r.Language))
            .ToList();

        await embeddingRepository.AddBatchAsync(embeddings, ct).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "[KbIngest] Persisted {Count} embeddings for source {SourceId} into VectorDocument {VdId} game {GameId}",
            embeddings.Count, sourceDocumentId, vd.Id, gameId);

        return embeddings.Count;
    }

    public async Task<int> RemoveBySourceAsync(
        Guid sourceDocumentId,
        Guid gameId,
        CancellationToken ct = default)
    {
        var vd = await vectorDocumentRepository
            .GetByGameAndSourceAsync(gameId, sourceDocumentId, ct).ConfigureAwait(false);

        if (vd is null)
        {
            logger.LogDebug(
                "[KbIngest] RemoveBySourceAsync: no VectorDocument for source {SourceId} game {GameId} — no-op",
                sourceDocumentId, gameId);
            return 0;
        }

        var totalChunks = vd.TotalChunks;

        await embeddingRepository.DeleteByVectorDocumentIdAsync(vd.Id, ct).ConfigureAwait(false);
        await vectorDocumentRepository.DeleteAsync(vd.Id, ct).ConfigureAwait(false);
        await unitOfWork.SaveChangesAsync(ct).ConfigureAwait(false);

        logger.LogInformation(
            "[KbIngest] Removed VectorDocument {VdId} ({Count} chunks) for source {SourceId} game {GameId}",
            vd.Id, totalChunks, sourceDocumentId, gameId);

        return totalChunks;
    }
}
