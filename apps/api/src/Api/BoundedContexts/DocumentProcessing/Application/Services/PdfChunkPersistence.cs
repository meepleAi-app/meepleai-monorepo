using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Observability;
using Api.Services;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// #4395: il salvataggio dei passaggi e delle righe vettoriali di un PDF, comune ai due caricamenti
/// fatti in processo (<c>/ingest/pdf</c> e <c>/ingest/pdf/chunked/complete</c>).
/// </summary>
/// <remarks>
/// <para>
/// Erano due copie, e la seconda era andata alla deriva. Il caricamento a pezzi non salvava
/// <c>text_chunks.SharedGameId</c>, e dalla rimozione di Qdrant (<c>c828f26bb</c>) calcolava gli
/// embedding senza salvarli. Un campo nuovo, o una regola come quella del collegamento (#4384), va
/// scritto qui una volta sola.
/// </para>
/// <para>
/// L'ordine conta: prima i passaggi, poi le righe vettoriali, che puntano a quelli
/// (<c>source_chunk_id</c>). La pipeline di Quartz (<c>PdfProcessingPipelineService</c>) e la
/// reindicizzazione (<c>IndexPdfCommandHandler</c>) scrivono per altre vie.
/// </para>
/// </remarks>
internal static class PdfChunkPersistence
{
    private const int EmbeddingBatchSize = 500;

    /// <summary>
    /// Sostituisce i passaggi del PDF con <paramref name="chunks"/>, classificati per ruolo.
    /// </summary>
    /// <returns>
    /// I passaggi salvati per <c>ChunkIndex</c>, a cui le righe vettoriali si collegano; vuoto se il
    /// salvataggio ha perso un conflitto di concorrenza e niente è stato scritto.
    /// </returns>
    public static async Task<IReadOnlyDictionary<int, TextChunkEntity>> SaveTextChunksAsync(
        MeepleAiDbContext db,
        Guid pdfId,
        PdfDocumentEntity pdfDoc,
        IReadOnlyList<DocumentChunkInput> chunks,
        IRoleClassifierService? roleClassifier,
        TimeProvider timeProvider,
        ILogger logger,
        string handlerName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(pdfDoc);
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        // Re-processing: the old chunks go.
        var existingChunks = await db.TextChunks
            .Where(tc => tc.PdfDocumentId == pdfId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (existingChunks.Count > 0)
        {
            db.TextChunks.RemoveRange(existingChunks);
        }

        // #4244: text_chunks.GameId is an FK to shared_games, so it only ever carries the shared
        // game; a private game goes in its own column. The keyword arm filters by these columns.
        var now = timeProvider.GetUtcNow().UtcDateTime;
        var textChunkEntities = chunks
            .Select((chunk, index) => new TextChunkEntity
            {
                Id = chunk.Id == Guid.Empty ? Guid.NewGuid() : chunk.Id,
                GameId = pdfDoc.SharedGameId,
                SharedGameId = pdfDoc.SharedGameId,
                PrivateGameId = pdfDoc.PrivateGameId,
                PdfDocumentId = pdfId,
                Content = chunk.Text,
                ChunkIndex = index,
                PageNumber = chunk.Page,
                CharacterCount = chunk.Text.Length,
                CreatedAt = now,
                // Issue #730: chunk hierarchy fields from the chunking pipeline
                Heading = chunk.Heading,
                Level = chunk.Level,
                ParentChunkId = chunk.ParentChunkId,
                ElementType = chunk.ElementType,
                // SP-A (#3405): char offsets for citation grounding
                CharStart = chunk.CharStart,
                CharEnd = chunk.CharEnd,
                // SP-B (#3406): the normalized region for citation grounding
                BoundingBoxesJson = ChunkBoundingBoxJson.Serialize(chunk.BBox, chunk.Page),
            })
            .ToList();

        // Phase D4: role_tags is populated on insert, so the pgvector rows can copy it.
        await TextChunkRoleClassifier.AssignRoleTagsAsync(roleClassifier, textChunkEntities, chunks, logger, cancellationToken)
            .ConfigureAwait(false);

        db.TextChunks.AddRange(textChunkEntities);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(handlerName, MeepleAiMetrics.PdfConcurrencyCategories.B);
            logger.LogWarning(ex,
                "Concurrency conflict on TextChunks for PDF {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                pdfId, handlerName);

            // #4384: the failed inserts and deletes stay in the change tracker, and the next
            // SaveChangesAsync (the pgvector batch) would submit them again. Drop them: nothing was
            // written, so the pgvector rows get no link rather than a link to rows that may not exist.
            foreach (var entry in db.ChangeTracker.Entries<TextChunkEntity>().ToList())
            {
                entry.State = EntityState.Detached;
            }

            return new Dictionary<int, TextChunkEntity>();
        }

        logger.LogInformation("Saved {ChunkCount} text chunks to PostgreSQL for hybrid search (PDF {PdfId})",
            textChunkEntities.Count, pdfId);
        return textChunkEntities.ToDictionary(tc => tc.ChunkIndex);
    }

    /// <summary>
    /// Sostituisce le righe vettoriali del PDF: una per passaggio, collegata al passaggio salvato con lo
    /// stesso indice (#4384). Il documento vettoriale deve già esistere.
    /// </summary>
    public static async Task SaveEmbeddingsAsync(
        MeepleAiDbContext db,
        Guid pdfId,
        PdfDocumentEntity pdfDoc,
        IReadOnlyList<DocumentChunkInput> chunks,
        IReadOnlyList<float[]> embeddings,
        IReadOnlyDictionary<int, TextChunkEntity> savedChunks,
        string modelName,
        ILogger logger,
        string handlerName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(pdfDoc);
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(embeddings);
        ArgumentNullException.ThrowIfNull(savedChunks);
        ArgumentNullException.ThrowIfNull(logger);
        if (embeddings.Count != chunks.Count)
        {
            throw new ArgumentException(
                $"One embedding per chunk is required: {embeddings.Count} embeddings for {chunks.Count} chunks", nameof(embeddings));
        }

        var gameId = pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId ?? Guid.Empty;
        var language = pdfDoc.Language ?? "en";

        var vectorDoc = await db.VectorDocuments
            .AsTracking()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"VectorDocument not found for PDF {pdfId} — cannot persist pgvector embeddings");

        // Re-index: the old rows go.
        var existing = await db.PgVectorEmbeddings
            .Where(e => e.VectorDocumentId == vectorDoc.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existing.Count > 0)
        {
            db.PgVectorEmbeddings.RemoveRange(existing);
            logger.LogInformation("🗑️ [PG-VECTOR] Removed {Count} existing embeddings for {PdfId}", existing.Count, pdfId);
        }

        var batchCount = (int)Math.Ceiling((double)chunks.Count / EmbeddingBatchSize);
        for (var batchIdx = 0; batchIdx < batchCount; batchIdx++)
        {
            var skip = batchIdx * EmbeddingBatchSize;
            var entities = chunks
                .Skip(skip)
                .Take(EmbeddingBatchSize)
                .Select((chunk, i) => PgVectorEmbeddingEntity.ForChunk(
                    vectorDocumentId: vectorDoc.Id,
                    gameId: gameId,
                    chunkIndex: skip + i,
                    textContent: chunk.Text,
                    vector: embeddings[skip + i],
                    model: modelName,
                    pageNumber: Math.Max(1, chunk.Page),
                    lang: language,
                    sourceChunk: savedChunks.TryGetValue(skip + i, out var saved) ? TextChunkLink.From(saved) : null))
                .ToList();

            await db.PgVectorEmbeddings.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                MeepleAiMetrics.RecordPdfConcurrencyConflict(handlerName, MeepleAiMetrics.PdfConcurrencyCategories.B);
                logger.LogWarning(ex,
                    "Concurrency conflict on PgVectorEmbeddings for PDF {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                    pdfId, handlerName);
                return;
            }
            catch (DbUpdateException)
            {
                // The failed rows stay in the change tracker, and the caller's failure handler would
                // submit them again with its own save: that save fails too, and the PDF stays in flight
                // forever instead of ending Failed. Drop them before the error reaches the caller.
                foreach (var entry in db.ChangeTracker.Entries<PgVectorEmbeddingEntity>().Where(e => e.State == EntityState.Added).ToList())
                {
                    entry.State = EntityState.Detached;
                }

                throw;
            }

            logger.LogInformation("💾 [PG-VECTOR] Saved batch {Idx}/{Total}: {Count} embeddings for PDF {PdfId}",
                batchIdx + 1, batchCount, entities.Count, pdfId);
        }

        logger.LogInformation("✅ [PG-VECTOR] Completed pgvector indexing for PDF {PdfId}: {Total} embeddings saved",
            pdfId, chunks.Count);
    }
}
