using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
using Api.Configuration;
using Api.Constants;
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
    private readonly IAdvancedChunkingService _advancedChunkingService;
    private readonly IEmbeddingService _embeddingService;
    private readonly IndexingSettings _indexingSettings;
    private readonly ILogger<IndexPdfCommandHandler> _logger;
    private readonly TimeProvider _timeProvider;
    private readonly ISemanticResponseCache _semanticCache;
    // Phase D4 (gamebook multi-book): optional role classifier for tagging chunks at ingest.
    // Optional so unit tests that pre-date Phase D continue to compile without updates.
    private readonly IRoleClassifierService? _roleClassifier;
    private readonly IPdfIndexingPipeline _pipeline;

    public IndexPdfCommandHandler(
        MeepleAiDbContext db,
        IAdvancedChunkingService advancedChunkingService,
        IEmbeddingService embeddingService,
        ILogger<IndexPdfCommandHandler> logger,
        IOptions<IndexingSettings> indexingSettings,
        ISemanticResponseCache semanticCache,
        IPdfIndexingPipeline pipeline,
        TimeProvider? timeProvider = null,
        IRoleClassifierService? roleClassifier = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _advancedChunkingService = advancedChunkingService ?? throw new ArgumentNullException(nameof(advancedChunkingService));
        _embeddingService = embeddingService ?? throw new ArgumentNullException(nameof(embeddingService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _indexingSettings = indexingSettings?.Value ?? throw new ArgumentNullException(nameof(indexingSettings));
        _semanticCache = semanticCache ?? throw new ArgumentNullException(nameof(semanticCache));
        _pipeline = pipeline ?? throw new ArgumentNullException(nameof(pipeline));
        _timeProvider = timeProvider ?? TimeProvider.System;
        _roleClassifier = roleClassifier;
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

            // Classify chunk roles ONCE, before pgvector ingestion, so BOTH sinks get role_tags.
            // Previously classification ran only in SaveTextChunksToPostgresAsync (after the vector
            // COPY), leaving pgvector_embeddings.role_tags at 0 — so the vector-arm role-match boost
            // (hybrid + semantic) had no data. Classifier faults degrade to None per chunk.
            var chunkRoles = await TextChunkRoleClassifier
                .ClassifyRolesAsync(_roleClassifier, documentChunks!, _logger, cancellationToken)
                .ConfigureAwait(false);

            // Step 3: Update VectorDocument status
            // For private PDFs GameId is null — fall back to PrivateGameId so vectors are scoped
            // to the correct private game rather than collapsed under Guid.Empty.
            var effectiveGameId = pdf.PrivateGameId ?? pdf.SharedGameId ?? Guid.Empty;
            var indexingSuccess = await IndexChunksInVectorStoreAsync(
                pdfId, effectiveGameId.ToString(), pdf.ExtractedText!, documentChunks!, chunkRoles, vectorDoc!, cancellationToken).ConfigureAwait(false);
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
            await SaveTextChunksToPostgresAsync(pdfId, chunkGameId, pdf.SharedGameId, documentChunks!, chunkRoles, cancellationToken).ConfigureAwait(false);

            // Mark processing complete
            pdf.ProcessingState = nameof(PdfProcessingState.Ready);
            pdf.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
            pdf.IsActiveForRag = true; // Auto-enable after successful indexing so vectors are searchable
            // Issue #3269 (SP3): stamp the current indexer version so `IndexerVersion == null`
            // means only true pre-versioning legacy (bulk re-index selector correctness).
            // Null-coalescing preserves an explicit version already stamped by a reindex reset.
            pdf.IndexerVersion ??= IndexerVersionRegistry.Current.Version;

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
        // .AsTracking() overrides the global QueryTrackingBehavior.NoTracking default (mirrors the
        // tracked `pdf` load above). Without it this existing VectorDocument is detached, so the
        // "reset to processing" write below AND the terminal MarkIndexingFailedAsync write are silent
        // no-ops — a failed re-index would leave the VectorDocument 'completed' with stale embeddings
        // while the PDF is Failed (divergent tables).
        var pdfGuid = Guid.Parse(pdfId);
        var existingVectorDoc = await _db.Set<VectorDocumentEntity>()
            .AsTracking()
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
    /// Chunks PDF text (heading-aware, Slice D) and generates embeddings.
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

        List<ExtractedElement>? structuredElements = string.IsNullOrWhiteSpace(pdf.StructuredElementsJson)
            ? null
            : System.Text.Json.JsonSerializer.Deserialize<List<ExtractedElement>>(pdf.StructuredElementsJson);

        var chunks = await HeadingAwareChunker.BuildAsync(
            structuredElements,
            extractedText,
            Guid.Parse(pdfId),
            pdf.PrivateGameId ?? pdf.SharedGameId,
            _advancedChunkingService,
            cancellationToken).ConfigureAwait(false);

        if (chunks.Count == 0)
        {
            _logger.LogWarning("No chunks created for PDF {PdfId}", pdfId);
            return (false, null, "No chunks created from text", PdfIndexingErrorCode.ChunkingFailed);
        }

        _logger.LogInformation("Created {ChunkCount} chunks for PDF {PdfId}", chunks.Count, pdfId);

        // Generate embeddings in batches to reduce memory footprint
        var embeddingBatchSize = _indexingSettings.EmbeddingBatchSize;
        var documentChunks = new List<DocumentChunk>(chunks.Count);

        // Slice D robustness guard: HeadingAwareChunker (unlike the old flat chunker) can emit a
        // Level-0 parent chunk whose Text is an entire document section — EmbeddingService does
        // not truncate, so an oversized chunk could fail the whole re-index. Cap ONLY the text
        // sent to the embedding provider; the persisted DocumentChunk/text_chunks/pgvector row
        // keeps the FULL Text for retrieval.
        var cappedChunkCount = 0;

        string CapTextForEmbedding(string text)
        {
            if (text.Length <= ChunkingConstants.MaxEmbeddingChars)
            {
                return text;
            }

            cappedChunkCount++;
            return text[..ChunkingConstants.MaxEmbeddingChars];
        }

        _logger.LogInformation("Generating embeddings for {ChunkCount} chunks in batches of {BatchSize}",
            chunks.Count, embeddingBatchSize);

        for (int i = 0; i < chunks.Count; i += embeddingBatchSize)
        {
            var batchSize = Math.Min(embeddingBatchSize, chunks.Count - i);

            _logger.LogDebug("Processing embedding batch {BatchNumber}/{TotalBatches} ({BatchSize} chunks)",
                (i / embeddingBatchSize) + 1,
                (int)Math.Ceiling((double)chunks.Count / embeddingBatchSize),
                batchSize);

            var batchChunks = chunks.Skip(i).Take(batchSize).ToList();
            var texts = batchChunks.Select(c => CapTextForEmbedding(c.Text)).ToList();
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

            // DocumentChunk is an init-only record: set the batch-computed Embedding via a
            // `with` clone rather than in-place mutation, preserving the Id/Heading/Level/
            // ParentChunkId/ElementType assigned upstream by HeadingAwareChunker.BuildAsync.
            for (var index = 0; index < batchChunks.Count; index++)
            {
                documentChunks.Add(batchChunks[index] with { Embedding = embeddingResult.Embeddings[index] });
            }

            _logger.LogDebug("Completed batch {BatchNumber}, total chunks processed: {ProcessedCount}/{TotalCount}",
                (i / embeddingBatchSize) + 1, documentChunks.Count, chunks.Count);
        }

        if (cappedChunkCount > 0)
        {
            _logger.LogWarning(
                "Capped {CappedChunkCount} of {ChunkCount} oversized chunk(s) to {MaxEmbeddingChars} characters before embedding for PDF {PdfId}; full chunk text is still persisted",
                cappedChunkCount, chunks.Count, ChunkingConstants.MaxEmbeddingChars, pdfId);
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
        IReadOnlyList<GameBookRole> roles,
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
            var entities = batchChunks.Select((chunk, i) =>
            {
                var globalIndex = batchIdx * saveBatchSize + i;
                return new PgVectorEmbeddingEntity
                {
                    Id = Guid.NewGuid(),
                    VectorDocumentId = vectorDoc.Id,
                    GameId = gameGuid,
                    TextContent = chunk.Text,
                    Vector = new Pgvector.Vector(chunk.Embedding),
                    Model = modelName,
                    ChunkIndex = globalIndex,
                    PageNumber = Math.Max(1, chunk.Page),
                    Lang = language,
                    CreatedAt = DateTimeOffset.UtcNow,
                    // role_tags population: denormalize the pre-computed role so the vector-arm
                    // role-match boost has data (was always 0 — classification used to run only
                    // after this COPY). Guarded against a None/mismatched roles list.
                    RoleTags = globalIndex < roles.Count ? (int)roles[globalIndex] : 0,
                };
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
        IReadOnlyList<GameBookRole> roles,
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
                Id = chunk.Id == Guid.Empty ? Guid.NewGuid() : chunk.Id,
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

        // Phase D4: apply the roles classified once up-front (Handle) so text_chunks.role_tags
        // matches the pgvector_embeddings.role_tags written for the same chunks (single classify).
        TextChunkRoleClassifier.ApplyRoles(textChunkEntities, roles);

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
