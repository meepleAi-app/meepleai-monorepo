using Api.BoundedContexts.Administration.Domain.Entities;
using Api.BoundedContexts.Administration.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Services;

/// <summary>
/// Re-embeds all vector documents using the current embedding provider (mxbai-embed-large).
/// Used for model migration: e5-large → mxbai-embed-large.
/// Reads text from TextChunks table, generates new embeddings, writes to pgvector.
/// </summary>
internal sealed class VectorReembeddingService
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly IEmbeddingService _embeddingService;
    private readonly IQdrantVectorStoreAdapter _vectorStore;
    private readonly IBatchJobRepository _batchJobRepository;
    private readonly ILogger<VectorReembeddingService> _logger;

    public VectorReembeddingService(
        MeepleAiDbContext dbContext,
        IEmbeddingService embeddingService,
        IQdrantVectorStoreAdapter vectorStore,
        IBatchJobRepository batchJobRepository,
        ILogger<VectorReembeddingService> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _batchJobRepository = batchJobRepository ?? throw new ArgumentNullException(nameof(batchJobRepository));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <summary>
    /// Re-embeds all vector documents, updating the batch job with progress.
    /// </summary>
    public async Task ExecuteAsync(BatchJob job, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(job);

        // Get all vector documents ordered by game for efficient processing
        var vectorDocs = await _dbContext.Set<VectorDocumentEntity>()
            .OrderBy(vd => vd.GameId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (vectorDocs.Count == 0)
        {
            _logger.LogInformation("No vector documents found for re-embedding");
            return;
        }

        _logger.LogInformation(
            "Starting re-embedding of {DocCount} vector documents using {Model}",
            vectorDocs.Count, _embeddingService.GetModelName());

        // Ensure pgvector table exists
        await _vectorStore.EnsureCollectionExistsAsync(
            vectorDocs[0].GameId ?? Guid.Empty,
            _embeddingService.GetEmbeddingDimensions(),
            cancellationToken).ConfigureAwait(false);

        var totalProcessed = 0;
        var totalEmbeddings = 0;
        var errors = new List<string>();

        for (var i = 0; i < vectorDocs.Count; i++)
        {
            var doc = vectorDocs[i];
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var embeddingCount = await ReembedDocumentAsync(doc, cancellationToken).ConfigureAwait(false);
                totalEmbeddings += embeddingCount;
                totalProcessed++;

                _logger.LogInformation(
                    "Re-embedded document {DocId} ({Index}/{Total}): {ChunkCount} embeddings",
                    doc.Id, i + 1, vectorDocs.Count, embeddingCount);
            }
            catch (Exception ex)
            {
                var errorMsg = $"Document {doc.Id}: {ex.Message}";
                errors.Add(errorMsg);
                _logger.LogError(ex, "Failed to re-embed document {DocId}", doc.Id);
            }

            // Update progress
            var progress = (int)((i + 1) * 100.0 / vectorDocs.Count);
            job.UpdateProgress(progress);
            await _batchJobRepository.UpdateAsync(job, cancellationToken).ConfigureAwait(false);
        }

        var summary = $"Re-embedded {totalProcessed}/{vectorDocs.Count} documents, {totalEmbeddings} total embeddings using {_embeddingService.GetModelName()}";
        if (errors.Count > 0)
        {
            summary += $". {errors.Count} errors.";
        }

        _logger.LogInformation("Re-embedding complete: {Summary}", summary);
    }

    private async Task<int> ReembedDocumentAsync(
        VectorDocumentEntity doc,
        CancellationToken cancellationToken)
    {
        // Get text chunks from the TextChunks table (stored during PDF upload)
        var textChunks = await _dbContext.TextChunks
            .Where(tc => tc.PdfDocumentId == doc.PdfDocumentId)
            .OrderBy(tc => tc.ChunkIndex)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        if (textChunks.Count == 0)
        {
            _logger.LogWarning("No text chunks found for document {DocId} (PdfId={PdfId})", doc.Id, doc.PdfDocumentId);
            return 0;
        }

        // Delete old embeddings from pgvector
        await _vectorStore.DeleteByVectorDocumentIdAsync(doc.Id, cancellationToken).ConfigureAwait(false);

        // Generate new embeddings in batches
        var texts = textChunks.Select(tc => tc.Content).ToList();
        var embeddings = new List<Embedding>();
        var batchSize = 10; // Sequential Ollama - keep batches small

        for (var i = 0; i < texts.Count; i += batchSize)
        {
            var batch = texts.Skip(i).Take(batchSize).ToList();
            var result = await _embeddingService.GenerateEmbeddingsAsync(batch, cancellationToken).ConfigureAwait(false);

            if (!result.Success || result.Embeddings.Count != batch.Count)
            {
                throw new InvalidOperationException(
                    $"Embedding generation failed for document {doc.Id}: {result.ErrorMessage}");
            }

            for (var j = 0; j < batch.Count; j++)
            {
                var chunkIndex = i + j;
                var textChunk = textChunks[chunkIndex];
                var vector = new Vector(result.Embeddings[j]);

                embeddings.Add(new Embedding(
                    id: Guid.NewGuid(),
                    vectorDocumentId: doc.Id,
                    textContent: textChunk.Content,
                    vector: vector,
                    model: _embeddingService.GetModelName(),
                    chunkIndex: chunkIndex,
                    pageNumber: Math.Max(1, textChunk.PageNumber ?? 1)));
            }
        }

        // Bulk insert new embeddings into pgvector
        if (embeddings.Count > 0)
        {
            await _vectorStore.IndexBatchAsync(embeddings, cancellationToken).ConfigureAwait(false);
        }

        // Update VectorDocumentEntity metadata
        doc.EmbeddingModel = _embeddingService.GetModelName();
        doc.EmbeddingDimensions = _embeddingService.GetEmbeddingDimensions();
        doc.IndexedAt = DateTime.UtcNow;
        doc.IndexingStatus = "completed";
        doc.IndexingError = null;
        await _dbContext.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return embeddings.Count;
    }
}
