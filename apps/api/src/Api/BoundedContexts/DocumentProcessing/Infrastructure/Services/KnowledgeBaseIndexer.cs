using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Generates embeddings for KnowledgeChunks and persists them to the KB vector store
/// via the <see cref="IKnowledgeBaseIngestService"/> anti-corruption layer.
///
/// BC isolation is preserved: this class only depends on the ACL interface published
/// by the KnowledgeBase BC's Application layer — no direct EF or domain-entity access.
///
/// Libro Game AI Assistant MVP — Phase 2 Task 2.3a + Gap G3.
/// </summary>
internal sealed class KnowledgeBaseIndexer(
    IEmbeddingService embeddingService,
    IKnowledgeBaseIngestService ingestService,
    ILogger<KnowledgeBaseIndexer> logger) : IKnowledgeBaseIndexer
{
    public async Task<int> IndexBatchAsync(
        Guid photoBatchUploadId,
        Guid gameId,
        IReadOnlyList<KnowledgeChunk> chunks,
        IProgress<int>? progress = null,
        CancellationToken ct = default)
    {
        if (chunks.Count == 0) return 0;

        var requests = new List<ChunkIngestionRequest>(chunks.Count);

        for (var i = 0; i < chunks.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var chunk = chunks[i];
            try
            {
                var embResult = await embeddingService
                    .GenerateEmbeddingAsync(chunk.TextContent, chunk.Language, ct)
                    .ConfigureAwait(false);

                if (!embResult.Success || embResult.Embeddings is not { Count: > 0 })
                {
                    logger.LogWarning(
                        "[KnowledgeBaseIndexer] Embedding failed for chunk {ChunkIndex} of batch {BatchId}: {Error}",
                        chunk.ChunkIndex, photoBatchUploadId, embResult.ErrorMessage ?? "no embedding returned");
                    continue;
                }

                var modelName = embeddingService.GetModelName();

                requests.Add(new ChunkIngestionRequest(
                    PageNumber: chunk.PageNumber,
                    ChunkIndex: chunk.ChunkIndex,
                    TextContent: chunk.TextContent,
                    Embedding: embResult.Embeddings[0],
                    Language: chunk.Language,
                    EmbeddingModel: modelName,
                    OcrConfidence: chunk.ConfidenceScore));

                progress?.Report(requests.Count);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[KnowledgeBaseIndexer] Failed to embed chunk {ChunkIndex} of batch {BatchId}",
                    chunk.ChunkIndex, photoBatchUploadId);
            }
        }

        if (requests.Count == 0) return 0;

        var indexed = await ingestService
            .IngestChunksAsync(photoBatchUploadId, gameId, requests, ct)
            .ConfigureAwait(false);

        logger.LogInformation(
            "[KnowledgeBaseIndexer] Batch {BatchId} for game {GameId}: indexed {Indexed}/{Total} chunks",
            photoBatchUploadId, gameId, indexed, chunks.Count);

        return indexed;
    }

    public Task<int> DeleteBatchAsync(Guid photoBatchUploadId, Guid gameId, CancellationToken ct = default)
        => ingestService.RemoveBySourceAsync(photoBatchUploadId, gameId, ct);
}
