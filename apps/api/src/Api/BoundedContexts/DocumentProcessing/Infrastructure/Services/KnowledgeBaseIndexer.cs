using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// Generates embeddings for KnowledgeChunks via <see cref="IEmbeddingService"/>.
/// Vector store persistence (pgvector) is deferred to Phase 2 Task 2.3b / Phase 3
/// once the cross-BC write interface is defined.
/// Libro Game AI Assistant MVP Phase 2 — Task 2.3a.
/// </summary>
/// <remarks>
/// Phase 2 Task 2.3a scope: embedding generation + progress reporting + logging.
/// Phase 3 scope: persist (chunk + embedding) to KB vector store via
/// a dedicated write-side anti-corruption layer (avoids direct cross-BC DB access).
/// See docs/superpowers/plans/2026-05-04-libro-game-assistant-phase2-detailed.md §2.3b.
/// </remarks>
internal sealed class KnowledgeBaseIndexer(
    IEmbeddingService embeddingService,
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

        var indexed = 0;

        for (var i = 0; i < chunks.Count; i++)
        {
            ct.ThrowIfCancellationRequested();

            var chunk = chunks[i];
            try
            {
                // Generate embedding vector for the chunk text.
                // Language-aware overload used when language metadata is available.
                var result = await embeddingService
                    .GenerateEmbeddingAsync(chunk.TextContent, chunk.Language, ct)
                    .ConfigureAwait(false);

                if (!result.Success)
                {
                    logger.LogWarning(
                        "[KnowledgeBaseIndexer] Embedding failed for chunk {ChunkIndex} of batch {BatchId}: {Error}",
                        chunk.ChunkIndex, photoBatchUploadId, result.ErrorMessage);
                    continue;
                }

#pragma warning disable S1135, MA0026
                // TODO Phase 2 Task 2.3b / Phase 3: persist (chunk + embedding result.Embeddings[0])
                // to KB vector store via cross-BC write adapter.
                // The float[] vector is: result.Embeddings[0]
                // Anti-corruption layer needed to avoid direct EF cross-BC access.
#pragma warning restore S1135, MA0026

                logger.LogDebug(
                    "[KnowledgeBaseIndexer] Generated embedding for chunk {ChunkIndex} of batch {BatchId} " +
                    "(page {Page}, {Length} chars, lang={Lang})",
                    chunk.ChunkIndex, photoBatchUploadId, chunk.PageNumber, chunk.CharLength, chunk.Language);

                indexed++;
                progress?.Report(indexed);
            }
            catch (OperationCanceledException)
            {
                throw;
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "[KnowledgeBaseIndexer] Failed to index chunk {ChunkIndex} of batch {BatchId}",
                    chunk.ChunkIndex, photoBatchUploadId);
            }
        }

        logger.LogInformation(
            "[KnowledgeBaseIndexer] Batch {BatchId} for game {GameId}: indexed {Indexed}/{ChunkCount} chunks",
            photoBatchUploadId, gameId, indexed, chunks.Count);

        return indexed;
    }

    public Task DeleteBatchAsync(Guid photoBatchUploadId, CancellationToken ct = default)
    {
#pragma warning disable S1135, MA0026
        // TODO Phase 2 Task 2.3b / Phase 3: delete vector documents for batch from KB vector store
        // via cross-BC write adapter (same ACL as IndexBatchAsync persistence).
#pragma warning restore S1135, MA0026

        logger.LogInformation(
            "[KnowledgeBaseIndexer] DeleteBatchAsync called for batch {BatchId} — " +
            "full vector store deletion deferred to Phase 3",
            photoBatchUploadId);
        return Task.CompletedTask;
    }
}
