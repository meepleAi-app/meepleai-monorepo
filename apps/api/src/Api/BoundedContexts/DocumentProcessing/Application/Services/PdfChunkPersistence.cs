using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
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
/// Un solo punto d'ingresso, <see cref="SaveAsync"/>, perché l'ordine conta: prima si controllano gli
/// ingressi (nessuna scrittura se il nome del modello manca o un vettore non è valido), poi si salvano i
/// passaggi, poi le righe vettoriali, che puntano a quelli (<c>source_chunk_id</c>). La pipeline di Quartz
/// (<c>PdfProcessingPipelineService</c>) e la reindicizzazione (<c>IndexPdfCommandHandler</c>) scrivono
/// per altre vie.
/// </para>
/// <para>
/// Un salvataggio che fallisce lascia le righe nel change tracker. Lo ripuliscono i gestori d'errore
/// dei due caricamenti, prima di segnare il PDF <c>Failed</c>: una regola sola per ogni tipo di riga.
/// </para>
/// </remarks>
internal static class PdfChunkPersistence
{
    private const int EmbeddingBatchSize = 500;

    /// <summary>
    /// Sostituisce passaggi e righe vettoriali del PDF. Il documento vettoriale deve già esistere.
    /// </summary>
    /// <exception cref="ArgumentException">Un embedding per passaggio manca, o uno non è valido.</exception>
    /// <exception cref="InvalidOperationException">Il nome del modello manca, o il salvataggio è fallito.</exception>
    public static async Task SaveAsync(
        MeepleAiDbContext db,
        Guid pdfId,
        PdfDocumentEntity pdfDoc,
        IReadOnlyList<DocumentChunkInput> chunks,
        IReadOnlyList<float[]> embeddings,
        string? modelName,
        IRoleClassifierService? roleClassifier,
        TimeProvider timeProvider,
        ILogger logger,
        string handlerName,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(db);
        ArgumentNullException.ThrowIfNull(pdfDoc);
        ArgumentNullException.ThrowIfNull(chunks);
        ArgumentNullException.ThrowIfNull(embeddings);
        ArgumentNullException.ThrowIfNull(timeProvider);
        ArgumentNullException.ThrowIfNull(logger);

        // Inputs first: nothing is rewritten if the vectors cannot be saved.
        if (string.IsNullOrWhiteSpace(modelName))
        {
            throw new InvalidOperationException("The embedding model name is missing: the vector rows cannot be saved.");
        }

        if (embeddings.Count != chunks.Count)
        {
            throw new ArgumentException(
                $"One embedding per chunk is required: {embeddings.Count} embeddings for {chunks.Count} chunks", nameof(embeddings));
        }

        var invalid = embeddings.Count(v => v is null || v.Length == 0 || Array.Exists(v, x => float.IsNaN(x) || float.IsInfinity(x)));
        if (invalid > 0)
        {
            throw new ArgumentException($"{invalid} embedding(s) are empty or contain NaN/Infinity", nameof(embeddings));
        }

        var savedChunks = await SaveTextChunksAsync(db, pdfId, pdfDoc, chunks, roleClassifier, timeProvider, logger, handlerName, cancellationToken)
            .ConfigureAwait(false);
        await SaveEmbeddingsAsync(db, pdfId, pdfDoc, chunks, embeddings, savedChunks, modelName, logger, handlerName, cancellationToken)
            .ConfigureAwait(false);
    }

    /// <returns>
    /// I passaggi salvati per <c>ChunkIndex</c>, a cui le righe vettoriali si collegano; vuoto se il
    /// salvataggio ha perso un conflitto di concorrenza e niente è stato scritto.
    /// </returns>
    private static async Task<IReadOnlyDictionary<int, TextChunkEntity>> SaveTextChunksAsync(
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

            // #4384: the failed inserts and deletes stay in the change tracker, and the next save (the
            // pgvector batch) would submit them again. Drop them: nothing was written, so the pgvector
            // rows get no link rather than a link to rows that may not exist.
            DetachAll<TextChunkEntity>(db);
            return new Dictionary<int, TextChunkEntity>();
        }
        catch (DbUpdateException ex)
        {
            throw new InvalidOperationException("Saving the text chunks of the PDF failed.", ex);
        }

        logger.LogInformation("Saved {ChunkCount} text chunks to PostgreSQL for hybrid search (PDF {PdfId})",
            textChunkEntities.Count, pdfId);
        return textChunkEntities.ToDictionary(tc => tc.ChunkIndex);
    }

    private static async Task SaveEmbeddingsAsync(
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
        var gameId = pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId ?? Guid.Empty;
        var language = pdfDoc.Language ?? "en";

        var vectorDoc = await db.VectorDocuments
            .AsTracking()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("VectorDocument", pdfId.ToString());

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

                // The pending inserts and deletes would go out again with the caller's next save (the
                // Ready transition), fail again, and leave the PDF in flight. Drop them.
                DetachAll<PgVectorEmbeddingEntity>(db);
                return;
            }
            catch (DbUpdateException ex)
            {
                throw new InvalidOperationException("Saving the vector rows of the PDF failed.", ex);
            }

            logger.LogInformation("💾 [PG-VECTOR] Saved batch {Idx}/{Total}: {Count} embeddings for PDF {PdfId}",
                batchIdx + 1, batchCount, entities.Count, pdfId);
        }

        logger.LogInformation("✅ [PG-VECTOR] Completed pgvector indexing for PDF {PdfId}: {Total} embeddings saved",
            pdfId, chunks.Count);
    }

    private static void DetachAll<TEntity>(MeepleAiDbContext db)
        where TEntity : class
    {
        foreach (var entry in db.ChangeTracker.Entries<TEntity>().Where(e => e.State != EntityState.Unchanged).ToList())
        {
            entry.State = EntityState.Detached;
        }
    }
}
