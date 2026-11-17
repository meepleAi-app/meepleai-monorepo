using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Domain.Chunking;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
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
/// #5003: passaggi e righe vettoriali si scrivono in UNA transazione, come nella pipeline a coda dal
/// #4660. Prima il commit dei passaggi era a sé, e ogni lotto di righe vettoriali un altro: in una
/// rielaborazione, un fallimento fra i due lasciava le righe vettoriali vecchie collegate a passaggi
/// già cancellati, e uno a metà dei lotti un indice parziale. Ora un fallimento lascia passaggi e righe
/// vettoriali di prima, che si corrispondono.
/// </para>
/// <para>
/// La transazione tiene indietro il database, non il change tracker: le righe di questo salvataggio,
/// salvate o no, si dimenticano all'uscita da un fallimento, e all'inizio di un tentativo che la
/// strategia di esecuzione ripete.
/// </para>
/// </remarks>
internal static class PdfChunkPersistence
{
    private const int EmbeddingBatchSize = 500;

    /// <summary>
    /// Sostituisce passaggi e righe vettoriali del PDF, in una transazione. Il documento vettoriale deve
    /// già esistere. La transazione la apre <c>unitOfWork</c>, che deve essere quella dello stesso scope di
    /// <c>db</c>. Un conflitto di concorrenza non scrive niente e si propaga: il chiamante non deve dichiarare
    /// l'indice completo né il PDF pronto.
    /// </summary>
    /// <exception cref="ArgumentException">Un embedding per passaggio manca, o uno non è valido.</exception>
    /// <exception cref="InvalidOperationException">Il nome del modello manca, o il salvataggio è fallito.</exception>
    /// <exception cref="DbUpdateConcurrencyException">Un'altra scrittura ha toccato le stesse righe; niente è stato scritto.</exception>
    public static async Task SaveAsync(
        MeepleAiDbContext db,
        IUnitOfWork unitOfWork,
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
        ArgumentNullException.ThrowIfNull(unitOfWork);
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

        // Phase D4: role_tags is populated on insert, so the pgvector rows can copy it. Classified here, before
        // the transaction: the classifier may call an LLM for every chunk, and neither the open transaction nor a
        // retry of it should wait for, or repeat, that (same rule as the queued pipeline, #4660).
        var roles = await TextChunkRoleClassifier.ClassifyRolesAsync(roleClassifier, chunks, logger, cancellationToken)
            .ConfigureAwait(false);

        try
        {
            // #3636: the unit of work, not BeginTransactionAsync: under the retrying execution strategy a
            // transaction opened directly throws, and the strategy may run the delegate more than once.
            await unitOfWork.ExecuteInTransactionAsync(async ct =>
            {
                ForgetStagedRows(db);
                var savedChunks = await SaveTextChunksAsync(db, pdfId, pdfDoc, chunks, roles, timeProvider, logger, ct)
                    .ConfigureAwait(false);
                await SaveEmbeddingsAsync(db, pdfId, pdfDoc, chunks, embeddings, savedChunks, modelName, logger, ct)
                    .ConfigureAwait(false);
            }, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            ForgetStagedRows(db);
            MeepleAiMetrics.RecordPdfConcurrencyConflict(handlerName, MeepleAiMetrics.PdfConcurrencyCategories.B);
            logger.LogWarning(ex,
                "Concurrency conflict on the text chunks or vector rows of PDF {PdfId} in {Handler} (Category B) — admin mutation wins, nothing was written",
                pdfId, handlerName);

            // Not swallowed: the caller would go on to declare the index completed (and the game's knowledge
            // base present) and the PDF Ready, with nothing of this run written.
            throw;
        }
        catch
        {
            ForgetStagedRows(db);
            throw;
        }
    }

    /// <summary>
    /// Stacca dal tracker ogni passaggio e ogni riga vettoriale, in qualunque stato: dopo un tentativo
    /// tornato indietro nessuno corrisponde al database (i nuovi <c>Added</c>, o <c>Unchanged</c> se il
    /// loro salvataggio è passato prima del rollback; i vecchi <c>Deleted</c>). I due caricamenti non
    /// tengono altre righe di questi tipi sul contesto.
    /// </summary>
    private static void ForgetStagedRows(MeepleAiDbContext db)
    {
        foreach (var entry in db.ChangeTracker.Entries()
                     .Where(e => e.Entity is TextChunkEntity or PgVectorEmbeddingEntity)
                     .ToList())
        {
            entry.State = EntityState.Detached;
        }
    }

    /// <returns>I passaggi salvati per <c>ChunkIndex</c>, a cui le righe vettoriali si collegano.</returns>
    private static async Task<IReadOnlyDictionary<int, TextChunkEntity>> SaveTextChunksAsync(
        MeepleAiDbContext db,
        Guid pdfId,
        PdfDocumentEntity pdfDoc,
        IReadOnlyList<DocumentChunkInput> chunks,
        IReadOnlyList<GameBookRole> roles,
        TimeProvider timeProvider,
        ILogger logger,
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
                PageNumber = ChunkPage.ForStorage(chunk.Page),
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

        TextChunkRoleClassifier.ApplyRoles(textChunkEntities, roles);

        db.TextChunks.AddRange(textChunkEntities);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        // #5003: a concurrency conflict leaves through SaveAsync, which rolls back and writes nothing. It used
        // to be swallowed here, and the vector rows were then saved without a link next to the old chunks.
        catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
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
                // #5003: a save of the transaction, not a commit.
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateException ex) when (ex is not DbUpdateConcurrencyException)
            {
                throw new InvalidOperationException("Saving the vector rows of the PDF failed.", ex);
            }

            logger.LogInformation("💾 [PG-VECTOR] Saved batch {Idx}/{Total}: {Count} embeddings for PDF {PdfId}",
                batchIdx + 1, batchCount, entities.Count, pdfId);
        }

        logger.LogInformation("✅ [PG-VECTOR] Completed pgvector indexing for PDF {PdfId}: {Total} embeddings saved",
            pdfId, chunks.Count);
    }
}
