using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.Configuration;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Observability;
using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using PdfIndexingErrorCode = Api.BoundedContexts.DocumentProcessing.Application.DTOs.PdfIndexingErrorCode;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Handler for IndexPdfCommand. Orchestrates the PDF indexing workflow:
/// 1. Load PDF document from repository
/// 2. Validate extraction status
/// 3. Chunk extracted text
/// 4. Generate embeddings
/// 5. Index embeddings to pgvector
/// 6. Update PDF document status
/// </summary>
internal class IndexPdfCommandHandler : ICommandHandler<IndexPdfCommand, IndexingResultDto>
{
    private readonly MeepleAiDbContext _db;
    private readonly ITextChunkingService _chunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IndexingSettings _indexingSettings;
    private readonly ILogger<IndexPdfCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ISemanticResponseCache _semanticCache;
    // Phase D4 (gamebook multi-book): optional role classifier for tagging chunks at ingest.
    // Optional so unit tests that pre-date Phase D continue to compile without updates.
    private readonly IRoleClassifierService? _roleClassifier;
    private readonly IPdfIndexingPipeline _pipeline;
    // SP2 task 7 (#3268): optional heading-aware chunker. IndexPdf only has the flat
    // pdf.ExtractedText — StructuredElementsJson (persisted at extraction time) is
    // rehydrated into ExtractedElements so hierarchy-aware chunking can run here too.
    // Optional so unit tests that pre-date SP2 continue to compile without updates.
    private readonly IHeadingAwareChunker? _headingAwareChunker;

    public IndexPdfCommandHandler(
        MeepleAiDbContext db,
        ITextChunkingService chunkingService,
        IEmbeddingService embeddingService,
        ILogger<IndexPdfCommandHandler> logger,
        IOptions<IndexingSettings> indexingSettings,
        ISemanticResponseCache semanticCache,
        IPdfIndexingPipeline pipeline,
        TimeProvider? timeProvider = null,
        IRoleClassifierService? roleClassifier = null,
        IHeadingAwareChunker? headingAwareChunker = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _chunkingService = chunkingService ?? throw new ArgumentNullException(nameof(chunkingService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _indexingSettings = indexingSettings?.Value ?? throw new ArgumentNullException(nameof(indexingSettings));
        _semanticCache = semanticCache ?? throw new ArgumentNullException(nameof(semanticCache));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _roleClassifier = roleClassifier;
        _headingAwareChunker = headingAwareChunker;
    }

    public async Task<IndexingResultDto> Handle(IndexPdfCommand command, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(command);
        var pdfId = command.PdfId;
        _logger.LogInformation("Starting indexing for PDF {PdfId}", pdfId);

        try
        {
            // Step 1: Validate PDF and prepare for indexing
            var (validationSuccess, pdf, vectorDoc, validationError, errorCode) = await ValidateAndPreparePdfForIndexingAsync(
                pdfId, cancellationToken).ConfigureAwait(false);
            if (!validationSuccess)
            {
                return IndexingResultDto.CreateFailure(validationError!, errorCode!.Value);
            }

            // Track processing state: mark as Indexing (covers chunk + embed + index phases)
            pdf!.ProcessingState = nameof(PdfProcessingState.Indexing);
            pdf.IndexingStartedAt = _timeProvider.GetUtcNow().UtcDateTime;
            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                MeepleAiMetrics.RecordPdfConcurrencyConflict(
                    nameof(IndexPdfCommandHandler),
                    MeepleAiMetrics.PdfConcurrencyCategories.B);
                _logger.LogWarning(ex,
                    "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                    pdfId, nameof(IndexPdfCommandHandler));
                return IndexingResultDto.CreateFailure("Concurrency conflict; please retry", PdfIndexingErrorCode.UnexpectedError);
            }

            // Step 2: Chunk text and generate embeddings
            var (chunkingSuccess, documentChunks, chunkingError, chunkErrorCode) = await ChunkAndEmbedTextAsync(
                pdfId, pdf, cancellationToken).ConfigureAwait(false);
            if (!chunkingSuccess)
            {
                pdf.ProcessingState = nameof(PdfProcessingState.Failed);
                return await MarkIndexingFailedAsync(vectorDoc!, chunkingError!, chunkErrorCode!.Value, cancellationToken).ConfigureAwait(false);
            }

            // Step 3: Update VectorDocument status
            // For private PDFs GameId is null — fall back to PrivateGameId so vectors are scoped
            // to the correct private game rather than collapsed under Guid.Empty.
            var effectiveGameId = pdf.PrivateGameId ?? pdf.SharedGameId ?? Guid.Empty;
            var indexingSuccess = await IndexChunksInVectorStoreAsync(
                pdfId, effectiveGameId.ToString(), pdf.ExtractedText!, documentChunks!, vectorDoc!, cancellationToken).ConfigureAwait(false);
            if (!indexingSuccess)
            {
                pdf.ProcessingState = nameof(PdfProcessingState.Failed);
                return await MarkIndexingFailedAsync(vectorDoc!, "Vector indexing failed", PdfIndexingErrorCode.VectorIndexingFailed, cancellationToken).ConfigureAwait(false);
            }

            // Transition VectorDocument processing → completed via the centralised pipeline
            // (#2244 / epic #2242). The pipeline raises VectorDocumentIndexedEvent on the
            // transition so has_knowledge_base is flipped projection-side. Replaces the
            // inline mutation that previously lived inside IndexChunksInVectorStoreAsync.
            await _pipeline.IndexAsync(
                pdfDocumentId: Guid.Parse(pdfId),
                gameId: pdf.PrivateGameId ?? pdf.SharedGameId,
                sharedGameId: pdf.SharedGameId,
                chunkCount: documentChunks!.Count,
                totalCharacters: pdf.ExtractedText!.Length,
                language: string.IsNullOrWhiteSpace(pdf.Language) ? "en" : pdf.Language,
                cancellationToken: cancellationToken).ConfigureAwait(false);

            // Step 4: Save text chunks to PostgreSQL for hybrid search
            // text_chunks.GameId is FK to games.Id (NOT shared_games.id) — resolve via PdfGameIdResolver.
            var chunkGameId = await PdfGameIdResolver.ResolveAsync(_db, pdf, cancellationToken).ConfigureAwait(false);
            await SaveTextChunksToPostgresAsync(pdfId, chunkGameId, pdf.SharedGameId, documentChunks!, cancellationToken).ConfigureAwait(false);

            // Mark processing complete
            pdf.ProcessingState = nameof(PdfProcessingState.Ready);
            pdf.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
            pdf.IsActiveForRag = true; // Auto-enable after successful indexing so vectors are searchable

            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                MeepleAiMetrics.RecordPdfConcurrencyConflict(
                    nameof(IndexPdfCommandHandler),
                    MeepleAiMetrics.PdfConcurrencyCategories.B);
                _logger.LogWarning(ex,
                    "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                    pdfId, nameof(IndexPdfCommandHandler));
                return IndexingResultDto.CreateFailure("Concurrency conflict; please retry", PdfIndexingErrorCode.UnexpectedError);
            }

            // Invalidate semantic response cache so stale answers are not served after re-index
            await _semanticCache.InvalidateGameAsync(effectiveGameId, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation("Successfully indexed PDF {PdfId}: {ChunkCount} chunks, {TotalChars} characters",
                pdfId, documentChunks!.Count, pdf.ExtractedText!.Length);

            return IndexingResultDto.CreateSuccess(
                vectorDoc!.Id.ToString(),
                documentChunks.Count,
                // IndexedAt timestamp is the moment "this call" completed indexing.
                // The pipeline persists its own IndexedAt to the DB; we emit ours
                // to the DTO so callers don't have to re-read after we just
                // wrote — keeps the return self-contained.
                _timeProvider.GetUtcNow().UtcDateTime);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // SERVICE BOUNDARY PATTERN: Error state management for complex multi-system operation
        // PDF indexing involves multiple external systems (pgvector, DB, file system) that must maintain consistency
#pragma warning restore S125
        catch (Exception ex)
        {
            // ERROR STATE MANAGEMENT: Top-level catch ensures graceful failure handling
            // Rationale: PDF indexing involves multiple external systems (pgvector, DB, file system).
            // Any unhandled error should be captured, logged, and persisted as a failed indexing
            // attempt rather than throwing to the caller. This maintains data consistency and
            // provides operators with debugging context via the indexing_error field.
            // Context: Covers unforeseen errors after specific exception handlers above
            _logger.LogError(ex, "Unexpected error indexing PDF {PdfId}", pdfId);

            // Persist failed state so the PDF doesn't remain stuck in "Indexing"
            try
            {
                var failedPdf = await _db.PdfDocuments
                    .AsTracking()
                    .FirstOrDefaultAsync(p => p.Id.ToString() == pdfId, CancellationToken.None).ConfigureAwait(false);
                if (failedPdf != null)
                {
                    failedPdf.ProcessingState = nameof(PdfProcessingState.Failed);
                    failedPdf.ProcessingError = $"Unexpected error: {ex.Message}";
                    try
                    {
                        await _db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
                    }
                    catch (DbUpdateConcurrencyException concEx)
                    {
                        MeepleAiMetrics.RecordPdfConcurrencyConflict(
                            nameof(IndexPdfCommandHandler),
                            MeepleAiMetrics.PdfConcurrencyCategories.B);
                        _logger.LogWarning(concEx,
                            "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                            pdfId, nameof(IndexPdfCommandHandler));
                    }
                }
            }
            catch (Exception dbEx)
            {
                _logger.LogError(dbEx, "Failed to persist error state for PDF {PdfId}", pdfId);
            }

            return IndexingResultDto.CreateFailure($"Unexpected error: {ex.Message}", PdfIndexingErrorCode.UnexpectedError);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Validates PDF document and prepares VectorDocument for indexing with idempotency check.
    /// Returns (success, pdf, vectorDoc, errorMessage, errorCode).
    /// </summary>
    private async Task<(bool success, PdfDocumentEntity? pdf, VectorDocumentEntity? vectorDoc, string? errorMessage, PdfIndexingErrorCode? errorCode)> ValidateAndPreparePdfForIndexingAsync(
        string pdfId,
        CancellationToken cancellationToken)
    {
        // Retrieve PDF document with tracking enabled (global NoTracking default must be overridden)
        var pdf = await _db.PdfDocuments
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id.ToString() == pdfId, cancellationToken).ConfigureAwait(false);

        if (pdf == null)
        {
            _logger.LogWarning("PDF {PdfId} not found", pdfId);
            return (false, null, null, "PDF not found", PdfIndexingErrorCode.PdfNotFound);
        }

        // Validate text extraction is complete
        if (string.IsNullOrWhiteSpace(pdf.ExtractedText))
        {
            _logger.LogWarning("PDF {PdfId} has no extracted text", pdfId);
            return (false, pdf, null, "PDF text extraction required. Please extract text before indexing.", PdfIndexingErrorCode.TextExtractionRequired);
        }

        // Check if already indexed (for idempotency)
        var pdfGuid = Guid.Parse(pdfId);
        var existingVectorDoc = await _db.Set<VectorDocumentEntity>()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfGuid, cancellationToken).ConfigureAwait(false);

        if (existingVectorDoc != null)
        {
            _logger.LogInformation("PDF {PdfId} already indexed, re-indexing", pdfId);

            // Update existing entity status to "processing"
            existingVectorDoc.IndexingStatus = "processing";
            existingVectorDoc.IndexingError = null;
            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                MeepleAiMetrics.RecordPdfConcurrencyConflict(
                    nameof(IndexPdfCommandHandler),
                    MeepleAiMetrics.PdfConcurrencyCategories.B);
                _logger.LogWarning(ex,
                    "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                    pdfId, nameof(IndexPdfCommandHandler));
                return (false, null, null, "Concurrency conflict; please retry", PdfIndexingErrorCode.UnexpectedError);
            }
        }
        else
        {
            // Create new VectorDocumentEntity in "processing" state. IPdfIndexingPipeline.IndexAsync
            // (called downstream by the Handler) transitions it to "completed" and raises
            // VectorDocumentIndexedEvent structurally via the VectorDocument domain aggregate.
            // The intermediate "processing" entity exists so the PgVectorEmbedding rows have
            // a stable VectorDocumentId FK to reference during embedding generation.
            var embeddingDimensions = _embeddingService.GetEmbeddingDimensions();

#pragma warning disable MAI005 // Two-phase create: this entity is the placeholder for the
                                // FK during embedding generation; pipeline.IndexAsync later
                                // owns the structural event raising (see #2244 / P234).
            existingVectorDoc = new VectorDocumentEntity
            {
                Id = Guid.NewGuid(),
                GameId = pdf.SharedGameId,
                SharedGameId = pdf.SharedGameId, // Issue #5185: propagate SharedGameId from PDF
                PdfDocumentId = pdfGuid,
                IndexingStatus = "processing",
                EmbeddingModel = _embeddingService.GetModelName(),
                EmbeddingDimensions = embeddingDimensions
            };
#pragma warning restore MAI005
            _db.Set<VectorDocumentEntity>().Add(existingVectorDoc);
            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                MeepleAiMetrics.RecordPdfConcurrencyConflict(
                    nameof(IndexPdfCommandHandler),
                    MeepleAiMetrics.PdfConcurrencyCategories.B);
                _logger.LogWarning(ex,
                    "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                    pdfId, nameof(IndexPdfCommandHandler));
                return (false, null, null, "Concurrency conflict; please retry", PdfIndexingErrorCode.UnexpectedError);
            }
        }

        return (true, pdf, existingVectorDoc, null, null);
    }

    /// <summary>
    /// Chunks PDF text and generates embeddings.
    /// Returns (success, documentChunks, errorMessage, errorCode).
    /// </summary>
    private async Task<(bool success, List<DocumentChunk>? documentChunks, string? errorMessage, PdfIndexingErrorCode? errorCode)> ChunkAndEmbedTextAsync(
        string pdfId,
        PdfDocumentEntity pdf,
        CancellationToken cancellationToken)
    {
        var extractedText = pdf.ExtractedText!;

        // Chunk the text
        _logger.LogInformation("Chunking text for PDF {PdfId} ({CharCount} characters)",
            pdfId, extractedText.Length);

        // SP2 task 7 (#3268): IndexPdf only has the flat ExtractedText — rehydrate the
        // persisted StructuredElementsJson (written at extraction time) so the heading-aware
        // chunker can rebuild hierarchy-aware chunks. Malformed/legacy JSON degrades to null
        // (TryDeserialize never throws), which falls through to the flat path below.
        var structured = StructuredElementsPayload.TryDeserialize(pdf.StructuredElementsJson);
        List<DocumentChunkInput> chunkInputs =
            (_headingAwareChunker != null
                ? await _headingAwareChunker.ChunkAsync(Guid.Parse(pdfId), pdf.PrivateGameId ?? pdf.SharedGameId, structured, extractedText, cancellationToken).ConfigureAwait(false)
                : null) is { Count: > 0 } hc
            ? hc
            : (_chunkingService.PrepareForEmbedding(extractedText) ?? new List<DocumentChunkInput>());

        chunkInputs = chunkInputs.Where(c => c != null && !string.IsNullOrWhiteSpace(c.Text)).ToList();

        if (chunkInputs.Count == 0)
        {
            _logger.LogWarning("No chunks created for PDF {PdfId}", pdfId);
            return (false, null, "No chunks created from text", PdfIndexingErrorCode.ChunkingFailed);
        }

        _logger.LogInformation("Created {ChunkCount} chunks for PDF {PdfId}", chunkInputs.Count, pdfId);

        // Generate embeddings in batches to reduce memory footprint
        var embeddingBatchSize = _indexingSettings.EmbeddingBatchSize;
        var documentChunks = new List<DocumentChunk>(chunkInputs.Count);

        _logger.LogInformation("Generating embeddings for {ChunkCount} chunks in batches of {BatchSize}",
            chunkInputs.Count, embeddingBatchSize);

        for (int i = 0; i < chunkInputs.Count; i += embeddingBatchSize)
        {
            var batchSize = Math.Min(embeddingBatchSize, chunkInputs.Count - i);

            _logger.LogDebug("Processing embedding batch {BatchNumber}/{TotalBatches} ({BatchSize} chunks)",
                (i / embeddingBatchSize) + 1,
                (int)Math.Ceiling((double)chunkInputs.Count / embeddingBatchSize),
                batchSize);

            var texts = chunkInputs.Skip(i).Take(batchSize).Select(c => c.Text).ToList();
            var embeddingResult = await _embeddingService.GenerateEmbeddingsAsync(texts, cancellationToken).ConfigureAwait(false);

            if (!embeddingResult.Success || embeddingResult.Embeddings.Count == 0)
            {
                _logger.LogError("Failed to generate embeddings for PDF {PdfId}: {Error}",
                    pdfId, embeddingResult.ErrorMessage);
                return (false, null, $"Embedding generation failed: {embeddingResult.ErrorMessage}", PdfIndexingErrorCode.EmbeddingFailed);
            }

            if (embeddingResult.Embeddings.Count != batchSize)
            {
                _logger.LogError("Embedding count mismatch: expected {Expected}, got {Actual}",
                    batchSize, embeddingResult.Embeddings.Count);
                return (false, null, "Embedding count mismatch", PdfIndexingErrorCode.EmbeddingFailed);
            }

            // Issue #730 / SP2 task 7: hierarchy fields (Heading, Level, ParentChunkId, ElementType) are
            // carried from DocumentChunkInput for both paths — the heading-aware chunker populates them,
            // the flat PrepareForEmbedding path leaves them at their DocumentChunkInput defaults (null/1/null/"NarrativeText").
            var batchChunks = chunkInputs.Skip(i).Take(batchSize)
                .Select((chunk, index) => new DocumentChunk
                {
                    Text = chunk.Text,
                    Embedding = embeddingResult.Embeddings[index],
                    Page = chunk.Page,
                    CharStart = chunk.CharStart,
                    CharEnd = chunk.CharEnd,
                    Heading = chunk.Heading,
                    Level = chunk.Level,
                    ParentChunkId = chunk.ParentChunkId,
                    ElementType = chunk.ElementType,
                })
                .ToList();

            documentChunks.AddRange(batchChunks);

            _logger.LogDebug("Completed batch {BatchNumber}, total chunks processed: {ProcessedCount}/{TotalCount}",
                (i / embeddingBatchSize) + 1, documentChunks.Count, chunkInputs.Count);
        }

        return (true, documentChunks, null, null);
    }

    /// <summary>
    /// Indexes document chunks in pgvector and updates VectorDocument.
    /// </summary>
    private async Task<bool> IndexChunksInVectorStoreAsync(
        string pdfId,
        string gameId,
        string extractedText,
        List<DocumentChunk> documentChunks,
        VectorDocumentEntity vectorDoc,
        CancellationToken cancellationToken)
    {
        var pdfGuid = Guid.Parse(pdfId);
        var gameGuid = Guid.TryParse(gameId, out var g) ? g : Guid.Empty;

        // Remove old embeddings
        var existing = await _db.PgVectorEmbeddings
            .Where(e => e.VectorDocumentId == vectorDoc.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existing.Count > 0)
        {
            _db.PgVectorEmbeddings.RemoveRange(existing);
            _logger.LogInformation("🗑️ [REINDEX] Removed {Count} old embeddings for PDF {PdfId}", existing.Count, pdfId);
        }

        // Get language from the PDF document
        var pdfDoc = await _db.PdfDocuments
            .FirstOrDefaultAsync(p => p.Id == pdfGuid, cancellationToken)
            .ConfigureAwait(false);
        var language = pdfDoc?.Language ?? "en";
        var modelName = _embeddingService.GetModelName();

        // Save in batches of 500
        const int saveBatchSize = 500;
        var batchCount = (int)Math.Ceiling((double)documentChunks.Count / saveBatchSize);

        for (var batchIdx = 0; batchIdx < batchCount; batchIdx++)
        {
            var batchChunks = documentChunks.Skip(batchIdx * saveBatchSize).Take(saveBatchSize).ToList();
            var entities = batchChunks.Select((chunk, i) => new PgVectorEmbeddingEntity
            {
                Id = Guid.NewGuid(),
                VectorDocumentId = vectorDoc.Id,
                GameId = gameGuid,
                TextContent = chunk.Text,
                Vector = new Pgvector.Vector(chunk.Embedding),
                Model = modelName,
                ChunkIndex = batchIdx * saveBatchSize + i,
                PageNumber = Math.Max(1, chunk.Page),
                Lang = language,
                CreatedAt = DateTimeOffset.UtcNow
            }).ToList();

            await _db.PgVectorEmbeddings.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
            try
            {
                await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                MeepleAiMetrics.RecordPdfConcurrencyConflict(
                    nameof(IndexPdfCommandHandler),
                    MeepleAiMetrics.PdfConcurrencyCategories.B);
                _logger.LogWarning(ex,
                    "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                    pdfId, nameof(IndexPdfCommandHandler));
                return false;
            }
        }

        // VectorDocument transition processing → completed is centralised in
        // IPdfIndexingPipeline (called by the Handle method right after this
        // function returns true). Kept out of here so the domain event
        // (VectorDocumentIndexedEvent) is raised from a single code path —
        // see epic #2242 / #2244.

        _logger.LogInformation("✅ [REINDEX] PDF {PdfId}: {Count} chunks indexed in pgvector", pdfId, documentChunks.Count);
        return true;
    }

    /// <summary>
    /// Saves text chunks to PostgreSQL for hybrid search with FTS.
    /// </summary>
    private async Task SaveTextChunksToPostgresAsync(
        string pdfId,
        Guid? gameId,
        Guid? sharedGameId,
        List<DocumentChunk> documentChunks,
        CancellationToken cancellationToken)
    {
        var pdfGuid = Guid.Parse(pdfId);

        // Delete existing chunks
        var existingChunks = await _db.TextChunks
            .Where(tc => tc.PdfDocumentId == pdfGuid)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (existingChunks.Count > 0)
        {
            _db.TextChunks.RemoveRange(existingChunks);
        }

        // Create new text chunk entities
        var textChunkEntities = documentChunks
            .Select((chunk, index) => new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                GameId = gameId,
                SharedGameId = sharedGameId,
                PdfDocumentId = pdfGuid,
                Content = chunk.Text,
                ChunkIndex = index,
                PageNumber = chunk.Page,
                CharacterCount = chunk.Text.Length,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime,
                // Issue #730: persist chunk hierarchy fields from chunking pipeline
                Heading = chunk.Heading,
                Level = chunk.Level,
                ParentChunkId = chunk.ParentChunkId,
                ElementType = chunk.ElementType
            })
            .ToList();

        // Phase D4: classify chunks by GameBookRole before persistence so
        // text_chunks.role_tags is populated on insert.
        await TextChunkRoleClassifier.AssignRoleTagsAsync(
            _roleClassifier, textChunkEntities, documentChunks, _logger, cancellationToken)
            .ConfigureAwait(false);

        _db.TextChunks.AddRange(textChunkEntities);
        _logger.LogInformation("Saved {ChunkCount} text chunks to PostgreSQL for hybrid search (PDF {PdfId})",
            textChunkEntities.Count, pdfId);
    }

    private async Task<IndexingResultDto> MarkIndexingFailedAsync(
        VectorDocumentEntity vectorDoc,
        string errorMessage,
        PdfIndexingErrorCode errorCode,
        CancellationToken cancellationToken)
    {
        vectorDoc.IndexingStatus = "failed";
        vectorDoc.IndexingError = errorMessage;
        try
        {
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(IndexPdfCommandHandler),
                MeepleAiMetrics.PdfConcurrencyCategories.B);
            _logger.LogWarning(ex,
                "Concurrency conflict on VectorDocument in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                nameof(IndexPdfCommandHandler));
            return IndexingResultDto.CreateFailure(errorMessage, errorCode);
        }

        return IndexingResultDto.CreateFailure(errorMessage, errorCode);
    }
}
