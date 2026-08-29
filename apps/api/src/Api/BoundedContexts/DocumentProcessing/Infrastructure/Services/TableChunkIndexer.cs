using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Services;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using ChunkingModels = Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using KbEntities = Api.BoundedContexts.KnowledgeBase.Domain.Entities;
using KbValueObjects = Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.Services;

/// <summary>
/// <see cref="ITableChunkIndexer"/> implementation. Mirrors the retrievable-chunk contract of
/// <c>PdfProcessingPipelineService.IndexInVectorStoreAsync</c>: a <c>text_chunks</c> row (with the
/// region bbox) plus a <c>pgvector_embeddings</c> row whose <c>source_chunk_id</c> points back to it
/// (the JOIN that carries the bbox into the citation). Appends the table chunk after the PDF's
/// narrative chunks and scopes idempotency to the single region (never the delete-all-per-pdf path).
/// </summary>
internal sealed class TableChunkIndexer : ITableChunkIndexer
{
    private const string TableElementType = "Table";

    private readonly MeepleAiDbContext _db;
    private readonly IEmbeddingService _embeddingService;
    private readonly IVectorStoreAdapter _vectorStore;
    private readonly ILogger<TableChunkIndexer> _logger;

    public TableChunkIndexer(
        MeepleAiDbContext db,
        IEmbeddingService embeddingService,
        IVectorStoreAdapter vectorStore,
        ILogger<TableChunkIndexer> logger)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _vectorStore = vectorStore ?? throw new ArgumentNullException(nameof(vectorStore));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<Guid?> IndexTableAsync(
        PdfDocumentEntity pdf,
        int pageNumber,
        double x,
        double y,
        double width,
        double height,
        string markdown,
        string regionHash,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pdf);

        // pgvector game scoping: mirror IndexInVectorStoreAsync (PrivateGameId ?? SharedGameId).
        var gameId = pdf.PrivateGameId ?? pdf.SharedGameId ?? Guid.Empty;
        if (gameId == Guid.Empty)
        {
            _logger.LogWarning(
                "TableChunkIndexer: PDF {PdfId} has no game; table not retrievable, skipping index", pdf.Id);
            return null;
        }

        var vectorDoc = await _db.VectorDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdf.Id, cancellationToken)
            .ConfigureAwait(false);
        if (vectorDoc is null)
        {
            _logger.LogWarning(
                "TableChunkIndexer: PDF {PdfId} has no vector document; skipping index", pdf.Id);
            return null;
        }

        // Embed FIRST so a transient embedding failure can't leave a half-written chunk.
        var embedResult = await _embeddingService
            .GenerateEmbeddingAsync(markdown, cancellationToken)
            .ConfigureAwait(false);
        if (!embedResult.Success || embedResult.Embeddings.Count == 0)
        {
            throw new InvalidOperationException(
                $"Embedding generation failed for table chunk (pdf {pdf.Id}): {embedResult.ErrorMessage ?? "no vector returned"}");
        }
        var vector = embedResult.Embeddings[0];

        var chunkId = TableRegionKey.ChunkIdFromRegionHash(regionHash);
        var resolvedGameId = await PdfGameIdResolver.ResolveAsync(_db, pdf, cancellationToken).ConfigureAwait(false);
        var bboxJson = ChunkBoundingBoxJson.Serialize(
            ChunkingModels.BoundingBox.FromCoordinates((float)x, (float)y, (float)width, (float)height),
            pageNumber);
        var now = DateTime.UtcNow;

        // Upsert the text_chunks row (deterministic id -> idempotent replace of this one region).
        // #3882: .AsTracking() richiesto — il default del DbContext e' NoTracking (PERF-06),
        // quindi senza di esso questa lettura e' DETACHED: le mutazioni sotto non raggiungono
        // il change tracker e SaveChangesAsync non scrive, e non solleva.
        var existing = await _db.TextChunks
            .AsTracking()
            .FirstOrDefaultAsync(tc => tc.Id == chunkId, cancellationToken)
            .ConfigureAwait(false);
        int chunkIndex;
        if (existing is null)
        {
            // Append after the current max so the table chunk never collides with a narrative
            // chunk on the fusion dedup key {PdfId}:{ChunkIndex}.
            var maxIndex = await _db.TextChunks
                .Where(tc => tc.PdfDocumentId == pdf.Id)
                .Select(tc => (int?)tc.ChunkIndex)
                .MaxAsync(cancellationToken)
                .ConfigureAwait(false) ?? -1;
            chunkIndex = maxIndex + 1;

            _db.TextChunks.Add(new TextChunkEntity
            {
                Id = chunkId,
                GameId = resolvedGameId,
                SharedGameId = pdf.SharedGameId,
                PdfDocumentId = pdf.Id,
                Content = markdown,
                ChunkIndex = chunkIndex,
                PageNumber = pageNumber,
                CharacterCount = markdown.Length,
                CreatedAt = now,
                ElementType = TableElementType,
                BoundingBoxesJson = bboxJson,
            });
        }
        else
        {
            chunkIndex = existing.ChunkIndex;
            existing.Content = markdown;
            existing.CharacterCount = markdown.Length;
            existing.PageNumber = pageNumber;
            existing.ElementType = TableElementType;
            existing.BoundingBoxesJson = bboxJson;
            existing.GameId = resolvedGameId;
            existing.SharedGameId = pdf.SharedGameId;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Replace this chunk's embedding (scoped delete), then index the new one wired to
        // source_chunk_id so its region bbox reaches the citation.
        await _vectorStore.EnsureCollectionExistsAsync(gameId, vector.Length, cancellationToken).ConfigureAwait(false);
        await _vectorStore.DeleteBySourceChunkIdsAsync(new[] { chunkId }, cancellationToken).ConfigureAwait(false);

        var embedding = new KbEntities.Embedding(
            id: Guid.NewGuid(),
            vectorDocumentId: vectorDoc.Id,
            textContent: markdown,
            vector: new KbValueObjects.Vector(vector),
            model: _embeddingService.GetModelName(),
            chunkIndex: chunkIndex,
            pageNumber: Math.Max(1, pageNumber),
            language: "en",
            sourceChunkId: chunkId,
            isTranslation: false,
            roleTags: 0);

        await _vectorStore.IndexBatchAsync(new List<KbEntities.Embedding> { embedding }, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "TableChunkIndexer: indexed table chunk {ChunkId} (idx {ChunkIndex}, page {Page}) for PDF {PdfId}",
            chunkId, chunkIndex, pageNumber, pdf.Id);
        return chunkId;
    }
}
