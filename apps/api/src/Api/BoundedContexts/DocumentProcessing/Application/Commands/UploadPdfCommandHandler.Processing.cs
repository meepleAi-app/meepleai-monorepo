using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Observability;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

internal partial class UploadPdfCommandHandler
{
    private async Task ProcessPdfAsync(string pdfId, string filePath, Guid userId, CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔄 [PDF-DEBUG] ProcessPdfAsync START for PDF {PdfId}, User {UserId}", pdfId, userId);

        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var quotaService = scope.ServiceProvider.GetRequiredService<IPdfUploadQuotaService>();
        var startTime = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            _logger.LogInformation("🔍 [PDF-DEBUG] Calling ValidateAndPrepareProcessingAsync for {PdfId}", pdfId);
            var pdfDoc = await ValidateAndPrepareProcessingAsync(pdfId, userId, db, quotaService, cancellationToken).ConfigureAwait(false);
            if (pdfDoc == null)
            {
                _logger.LogWarning("⚠️ [PDF-DEBUG] ValidateAndPrepareProcessingAsync returned null for {PdfId} - EARLY EXIT", pdfId);
                return; // Validation failed
            }
            _logger.LogInformation("✅ [PDF-DEBUG] Validation passed for {PdfId}, State: {State}", pdfId, pdfDoc.ProcessingState);

            // Step 1: Extract text with page tracking (20-40%)
            _logger.LogInformation("📄 [PDF-DEBUG] Step 1: Starting ExtractPdfContentAsync for {PdfId}", pdfId);
            var (extractionSuccess, fullText, extractResult) = await ExtractPdfContentAsync(
                pdfId, filePath, pdfDoc, db, scope, startTime, cancellationToken).ConfigureAwait(false);

            if (!extractionSuccess)
            {
                _logger.LogWarning("❌ [PDF-DEBUG] Extraction FAILED for {PdfId} - releasing quota and exiting", pdfId);
                await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);
                return;
            }
            _logger.LogInformation("✅ [PDF-DEBUG] Extraction SUCCESS for {PdfId}: {CharCount} chars, {Pages} pages", pdfId, fullText?.Length ?? 0, extractResult?.TotalPages ?? 0);

            // Step 2: Chunk text with page tracking (40-60%)
            _logger.LogInformation("✂️ [PDF-DEBUG] Step 2: Starting ChunkExtractedTextAsync for {PdfId}", pdfId);
            var allDocumentChunks = await ChunkExtractedTextAsync(
                pdfId, fullText!, extractResult!, db, scope, startTime, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("✅ [PDF-DEBUG] Chunking SUCCESS: {ChunkCount} chunks created", allDocumentChunks.Count);

            // Step 3: Generate embeddings (60-80%)
            _logger.LogInformation("🧠 [PDF-DEBUG] Step 3: Starting GenerateAndValidateEmbeddingsAsync for {ChunkCount} chunks", allDocumentChunks.Count);
            var (embeddingsSuccess, embeddings) = await GenerateAndValidateEmbeddingsAsync(
                pdfId, userId, allDocumentChunks, pdfDoc, db, quotaService, scope, startTime, cancellationToken).ConfigureAwait(false);

            if (!embeddingsSuccess)
            {
                _logger.LogWarning("❌ [PDF-DEBUG] Embeddings FAILED for {PdfId} - exiting", pdfId);
                return;
            }
            _logger.LogInformation("✅ [PDF-DEBUG] Embeddings SUCCESS: {EmbeddingCount} vectors generated", embeddings!.Count);

            // Step 4: Index in Qdrant (80-100%)
            _logger.LogInformation("🔍 [PDF-DEBUG] Step 4: Starting IndexInVectorStoreAsync for {PdfId}", pdfId);
            await IndexInVectorStoreAsync(
                pdfId, userId, pdfDoc, allDocumentChunks, embeddings!,
                db, scope, startTime, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("✅ [PDF-DEBUG] Indexing completed for {PdfId}", pdfId);

            // Complete processing
            _logger.LogInformation("🎉 [PDF-DEBUG] Step 5: Finalizing processing for {PdfId}", pdfId);
            await FinalizeProcessingAsync(pdfId, pdfDoc, userId, db, quotaService, startTime, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("✅ [PDF-DEBUG] ProcessPdfAsync COMPLETE for {PdfId}", pdfId);
        }
        catch (OperationCanceledException)
        {
            await HandleProcessingCancellationAsync(pdfId, userId, db, quotaService, startTime, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            await HandleProcessingErrorAsync(pdfId, userId, db, quotaService, startTime, ex, "Invalid operation", cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            await HandleProcessingErrorAsync(pdfId, userId, db, quotaService, startTime, ex, "Database error occurred", cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKGROUND SERVICE: PDF processing runs async; must catch all exceptions
        // to properly update document status, release quota, and prevent background task crash
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            await HandleProcessingErrorAsync(pdfId, userId, db, quotaService, startTime, ex, ex.Message, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Validates PDF ID and prepares document for processing with idempotency check.
    /// </summary>
    private async Task<PdfDocumentEntity?> ValidateAndPrepareProcessingAsync(
        string pdfId,
        Guid userId,
        MeepleAiDbContext db,
        IPdfUploadQuotaService quotaService,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("🔍 [PDF-DEBUG-VALIDATE] START validation for {PdfId}", pdfId);

        if (!Guid.TryParse(pdfId, out var pdfGuid))
        {
            _logger.LogError("❌ [PDF-DEBUG-VALIDATE] Invalid PDF ID format {PdfId}", pdfId);
            await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);
            return null;
        }

        _logger.LogInformation("🔍 [PDF-DEBUG-VALIDATE] Querying database for PDF {PdfId}", pdfId);
        var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfGuid }, cancellationToken).ConfigureAwait(false);
        if (pdfDoc == null)
        {
            _logger.LogError("❌ [PDF-DEBUG-VALIDATE] PDF document {PdfId} NOT FOUND in database", pdfId);
            await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);
            return null;
        }

        _logger.LogInformation("✅ [PDF-DEBUG-VALIDATE] PDF found, current state: {State}", pdfDoc.ProcessingState);

        // IDEMPOTENCY CHECK (#1742): Skip if already processing/processed
        var pendingState = nameof(PdfProcessingState.Pending);
        if (!string.Equals(pdfDoc.ProcessingState, pendingState, StringComparison.Ordinal))
        {
            _logger.LogInformation(
                "⏭️ [PDF-DEBUG-VALIDATE] PDF {PdfId} already processed (state: {State}), skipping duplicate background task",
                pdfId, pdfDoc.ProcessingState);

            var failedState = nameof(PdfProcessingState.Failed);
            if (string.Equals(pdfDoc.ProcessingState, failedState, StringComparison.Ordinal))
            {
                await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);
            }

            return null;
        }

        // Mark as processing (optimistic locking)
        _logger.LogInformation("🔄 [PDF-DEBUG-VALIDATE] Updating status from 'pending' to 'processing'");
        pdfDoc.ProcessingState = "Uploading";
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("✅ [PDF-DEBUG-VALIDATE] Status updated, proceeding with processing");

        return pdfDoc;
    }

    /// <summary>
    /// Extracts PDF text and structured content (tables, diagrams).
    /// Returns (success, fullText, extractResult).
    /// </summary>
    private async Task<(bool success, string? fullText, PagedTextExtractionResult? result)> ExtractPdfContentAsync(
        string pdfId,
        string filePath,
        PdfDocumentEntity pdfDoc,
        MeepleAiDbContext db,
        IServiceScope scope,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Extracting, 0, 0, startTime, null, cancellationToken).ConfigureAwait(false);

        var extractionStopwatch = Stopwatch.StartNew();
        // E2E fix: Use blob storage service instead of direct filesystem access (supports S3/R2)
        var gameIdForStorage = (pdfDoc.PrivateGameId ?? pdfDoc.GameId)?.ToString() ?? string.Empty;
        var fileStream = await _blobStorageService.RetrieveAsync(pdfId, gameIdForStorage, cancellationToken).ConfigureAwait(false);
        if (fileStream == null)
        {
            // Fallback to local filesystem for backward compatibility
            _logger.LogWarning("[PDF-DEBUG] Blob storage returned null for {PdfId}, falling back to filesystem: {FilePath}", pdfId, filePath);
            fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        }
        await using (fileStream.ConfigureAwait(false))
        {
            var extractResult = await _pdfTextExtractor.ExtractPagedTextAsync(fileStream, enableOcrFallback: true, cancellationToken).ConfigureAwait(false);
            extractionStopwatch.Stop();

            RecordPipelineMetricSafely("extraction", extractionStopwatch.Elapsed.TotalMilliseconds);

            if (!extractResult.Success)
            {
                RecordPipelineMetricSafely("extraction_error", 0);
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, extractResult.ErrorMessage, cancellationToken).ConfigureAwait(false);
                pdfDoc.ProcessingState = "Failed";
                pdfDoc.ProcessingError = extractResult.ErrorMessage;
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                return (false, null, null);
            }

            var fullText = string.Join("\n\n", extractResult.PageChunks
                .Where(pc => !pc.IsEmpty)
                .Select(pc => pc.Text));

            pdfDoc.ExtractedText = fullText;
            pdfDoc.PageCount = extractResult.TotalPages;
            pdfDoc.CharacterCount = extractResult.TotalCharacters;
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            RecordPipelineMetricSafely("pages_processed", extractResult.TotalPages);

            // Extract structured content (tables, diagrams)
            await ExtractStructuredContentAsync(filePath, pdfDoc, db, scope, cancellationToken).ConfigureAwait(false);

            await UpdateProgressAsync(db, pdfId, ProcessingStep.Extracting, extractResult.TotalPages, extractResult.TotalPages, startTime, null, cancellationToken).ConfigureAwait(false);

            return (true, fullText, extractResult);
        }
    }

    /// <summary>
    /// Extracts structured content (tables, diagrams, atomic rules) from PDF.
    /// </summary>
    private async Task ExtractStructuredContentAsync(
                string filePath,
        PdfDocumentEntity pdfDoc,
        MeepleAiDbContext db,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        var tableExtractor = scope.ServiceProvider.GetService<IPdfTableExtractor>() ?? _tableExtractor;
        if (tableExtractor == null) return;

        var structuredResult = await tableExtractor.ExtractStructuredContentAsync(filePath, cancellationToken).ConfigureAwait(false);
        if (!structuredResult.Success) return;

        pdfDoc.ExtractedTables = System.Text.Json.JsonSerializer.Serialize(structuredResult.Tables);
        pdfDoc.ExtractedDiagrams = System.Text.Json.JsonSerializer.Serialize(
            structuredResult.Diagrams.Select(d => new
            {
                d.PageNumber,
                d.DiagramType,
                d.Description,
                d.Width,
                d.Height
            }));
        pdfDoc.AtomicRules = System.Text.Json.JsonSerializer.Serialize(structuredResult.AtomicRules);
        pdfDoc.TableCount = structuredResult.TableCount;
        pdfDoc.DiagramCount = structuredResult.DiagramCount;
        pdfDoc.AtomicRuleCount = structuredResult.AtomicRuleCount;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Chunks extracted PDF text into document chunks for embedding.
    /// </summary>
    private async Task<List<DocumentChunkInput>> ChunkExtractedTextAsync(
        string pdfId,
        string fullText,
        PagedTextExtractionResult extractResult,
        MeepleAiDbContext db,
        IServiceScope scope,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Chunking, 0, extractResult.TotalPages, startTime, null, cancellationToken).ConfigureAwait(false);

        var chunkingStopwatch = Stopwatch.StartNew();
        var chunkingService = scope.ServiceProvider.GetRequiredService<ITextChunkingService>();
        const int chunkSize = 512;
        const int chunkOverlap = 50;

        var allDocumentChunks = chunkingService.PrepareForEmbedding(fullText, chunkSize, chunkOverlap)
            ?.Where(chunk => chunk != null && !string.IsNullOrWhiteSpace(chunk.Text))
            .Select(chunk => new DocumentChunkInput
            {
                Text = chunk.Text,
                Page = chunk.Page,
                CharStart = chunk.CharStart,
                CharEnd = chunk.CharEnd
            })
            .ToList()
            ?? new List<DocumentChunkInput>();

        if (allDocumentChunks.Count == 0)
        {
            foreach (var pageChunk in extractResult.PageChunks.Where(pc => !pc.IsEmpty))
            {
                var pageTextChunks = chunkingService.ChunkText(pageChunk.Text, chunkSize, chunkOverlap);

                foreach (var textChunk in pageTextChunks.Where(t => !string.IsNullOrWhiteSpace(t.Text)))
                {
                    allDocumentChunks.Add(new DocumentChunkInput
                    {
                        Text = textChunk.Text,
                        Page = pageChunk.PageNumber,
                        CharStart = textChunk.CharStart,
                        CharEnd = textChunk.CharEnd
                    });
                }
            }
        }

        allDocumentChunks = allDocumentChunks
            .Where(chunk => chunk != null && !string.IsNullOrWhiteSpace(chunk.Text))
            .ToList();

        chunkingStopwatch.Stop();
        RecordPipelineMetricSafely("chunking", chunkingStopwatch.Elapsed.TotalMilliseconds, allDocumentChunks.Count);

        return allDocumentChunks;
    }

    /// <summary>
    /// Generates and validates embeddings for document chunks using BATCH PROCESSING.
    /// Processes chunks in batches to avoid OutOfMemoryException with large PDFs.
    /// Returns (success, embeddings list).
    /// </summary>
    private async Task<(bool success, List<float[]>? embeddings)> GenerateAndValidateEmbeddingsAsync(
        string pdfId,
        Guid userId,
        List<DocumentChunkInput> allDocumentChunks,
        PdfDocumentEntity pdfDoc,
        MeepleAiDbContext db,
        IPdfUploadQuotaService quotaService,
        IServiceScope scope,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        const int BATCH_SIZE = 20; // Process 20 chunks at a time to avoid OOM
        var totalPages = pdfDoc.PageCount ?? 0;
        var totalChunks = allDocumentChunks.Count;

        await UpdateProgressAsync(db, pdfId, ProcessingStep.Embedding, 0, totalPages, startTime, null, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("🧠 [BATCH-EMBED] Starting batch embedding generation: {TotalChunks} chunks, batch size: {BatchSize}",
            totalChunks, BATCH_SIZE);

        // Generate embeddings in batches
        var embeddingStopwatch = Stopwatch.StartNew();
        var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
        var allEmbeddings = new List<float[]>();
        var batchCount = (int)Math.Ceiling((double)totalChunks / BATCH_SIZE);

        for (var batchIndex = 0; batchIndex < batchCount; batchIndex++)
        {
            var skip = batchIndex * BATCH_SIZE;
            var batchChunks = allDocumentChunks.Skip(skip).Take(BATCH_SIZE).ToList();
            var batchTexts = batchChunks.Select(c => c.Text).ToList();

            _logger.LogInformation("📦 [BATCH-EMBED] Processing batch {Current}/{Total}: {ChunkCount} chunks",
                batchIndex + 1, batchCount, batchTexts.Count);

            var batchResult = await embeddingService.GenerateEmbeddingsAsync(batchTexts).ConfigureAwait(false);

            if (!batchResult.Success)
            {
                _logger.LogError("❌ [BATCH-EMBED] Batch {Current}/{Total} FAILED: {Error}",
                    batchIndex + 1, batchCount, batchResult.ErrorMessage);
                await HandleEmbeddingFailureAsync(pdfId, userId, pdfDoc, db, quotaService, startTime,
                    $"Embedding generation failed at batch {batchIndex + 1}/{batchCount}: {batchResult.ErrorMessage}", cancellationToken).ConfigureAwait(false);
                return (false, null);
            }

            if (batchResult.Embeddings == null || batchResult.Embeddings.Count != batchTexts.Count)
            {
                var mismatch = $"Batch {batchIndex + 1} returned {batchResult.Embeddings?.Count ?? 0} embeddings for {batchTexts.Count} texts";
                _logger.LogError("❌ [BATCH-EMBED] {Mismatch}", mismatch);
                await HandleEmbeddingFailureAsync(pdfId, userId, pdfDoc, db, quotaService, startTime, mismatch, cancellationToken).ConfigureAwait(false);
                return (false, null);
            }

            // Validate batch quality
            foreach (var embedding in batchResult.Embeddings)
            {
                if (IsInvalidVector(embedding))
                {
                    var error = $"Invalid embedding detected in batch {batchIndex + 1}";
                    _logger.LogError("❌ [BATCH-EMBED] {Error}", error);
                    await HandleEmbeddingFailureAsync(pdfId, userId, pdfDoc, db, quotaService, startTime, error, cancellationToken).ConfigureAwait(false);
                    return (false, null);
                }
            }

            allEmbeddings.AddRange(batchResult.Embeddings);

            _logger.LogInformation("✅ [BATCH-EMBED] Batch {Current}/{Total} completed: {Count} embeddings generated",
                batchIndex + 1, batchCount, batchResult.Embeddings.Count);

            // Update progress incrementally
            var chunksProcessed = Math.Min(skip + BATCH_SIZE, totalChunks);
            var progressPercent = (int)((double)chunksProcessed / totalChunks * totalPages);
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Embedding, progressPercent, totalPages, startTime, null, cancellationToken).ConfigureAwait(false);

            // Force garbage collection between batches to release memory
            if (batchIndex < batchCount - 1) // Don't GC on last batch
            {
#pragma warning disable S1215 // GC.Collect should not be called - Justified for batch processing to prevent OOM
                GC.Collect();
                GC.WaitForPendingFinalizers();
                GC.Collect();
#pragma warning restore S1215
            }
        }

        embeddingStopwatch.Stop();
        RecordPipelineMetricSafely("embedding", embeddingStopwatch.Elapsed.TotalMilliseconds);

        await UpdateProgressAsync(db, pdfId, ProcessingStep.Embedding, totalPages, totalPages, startTime, null, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("✅ [BATCH-EMBED] All batches completed: {TotalEmbeddings} total embeddings generated in {Duration}ms",
            allEmbeddings.Count, embeddingStopwatch.Elapsed.TotalMilliseconds);

        // Final validation: total count
        if (allEmbeddings.Count != allDocumentChunks.Count)
        {
            var mismatch = $"Total embeddings {allEmbeddings.Count} != total chunks {allDocumentChunks.Count}";
            _logger.LogError("❌ [BATCH-EMBED] {Mismatch}", mismatch);
            await HandleEmbeddingFailureAsync(pdfId, userId, pdfDoc, db, quotaService, startTime, mismatch, cancellationToken).ConfigureAwait(false);
            return (false, null);
        }

        return (true, allEmbeddings);
    }

    /// <summary>
    /// Handles embedding generation or validation failure with consistent error handling.
    /// </summary>
    private async Task HandleEmbeddingFailureAsync(
        string pdfId,
        Guid userId,
        PdfDocumentEntity pdfDoc,
        MeepleAiDbContext db,
        IPdfUploadQuotaService quotaService,
        DateTime startTime,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, errorMessage, cancellationToken).ConfigureAwait(false);
        await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);
        pdfDoc.ProcessingState = "Failed";
        pdfDoc.ProcessingError = errorMessage;
        pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Indexes document chunks in Qdrant vector store and PostgreSQL for hybrid search.
    /// </summary>
    private async Task IndexInVectorStoreAsync(
        string pdfId,
        Guid userId,
        PdfDocumentEntity pdfDoc,
        List<DocumentChunkInput> allDocumentChunks,
        List<float[]> embeddings,
        MeepleAiDbContext db,
        IServiceScope scope,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        var totalPages = pdfDoc.PageCount ?? 0;

        await UpdateProgressAsync(db, pdfId, ProcessingStep.Indexing, 0, totalPages, startTime, null, cancellationToken).ConfigureAwait(false);

        var indexingStopwatch = Stopwatch.StartNew();

        // Vector store (Qdrant) has been removed — skip vector indexing.
        indexingStopwatch.Stop();

        RecordPipelineMetricSafely("indexing", indexingStopwatch.Elapsed.TotalMilliseconds);

        // Update vector document with chunk count (no Qdrant indexing)
        await UpdateVectorDocumentAsync(pdfId, pdfDoc, allDocumentChunks.Count, db, cancellationToken).ConfigureAwait(false);

        // Save text chunks to PostgreSQL for hybrid search (FTS)
        await SaveTextChunksForHybridSearchAsync(pdfId, pdfDoc, allDocumentChunks, db, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates or creates VectorDocument record after successful indexing.
    /// </summary>
    private async Task UpdateVectorDocumentAsync(
        string pdfId,
        PdfDocumentEntity pdfDoc,
        int indexedCount,
        MeepleAiDbContext db,
        CancellationToken cancellationToken)
    {
        var pdfGuid = Guid.Parse(pdfId);
        var vectorDoc = await db.VectorDocuments.FirstOrDefaultAsync(v => v.PdfDocumentId == pdfGuid, cancellationToken).ConfigureAwait(false);

        if (vectorDoc == null)
        {
            vectorDoc = new VectorDocumentEntity
            {
                Id = Guid.NewGuid(),
                GameId = pdfDoc.GameId,
                SharedGameId = pdfDoc.SharedGameId, // Issue #5185: propagate SharedGameId from PDF
                PdfDocumentId = pdfGuid,
                IndexingStatus = "completed",
                ChunkCount = indexedCount,
                TotalCharacters = pdfDoc.ExtractedText?.Length ?? 0,
                IndexedAt = _timeProvider.GetUtcNow().UtcDateTime
            };
            db.VectorDocuments.Add(vectorDoc);
        }
        else
        {
            vectorDoc.IndexingStatus = "completed";
            vectorDoc.ChunkCount = indexedCount;
            vectorDoc.TotalCharacters = pdfDoc.ExtractedText?.Length ?? 0;
            vectorDoc.IndexedAt = _timeProvider.GetUtcNow().UtcDateTime;
        }

        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves text chunks to PostgreSQL for hybrid search with FTS.
    /// </summary>
    private async Task SaveTextChunksForHybridSearchAsync(
        string pdfId,
        PdfDocumentEntity pdfDoc,
        List<DocumentChunkInput> allDocumentChunks,
        MeepleAiDbContext db,
        CancellationToken cancellationToken)
    {
        var pdfGuid = Guid.Parse(pdfId);

        // Delete existing chunks for re-processing scenario
        var existingChunks = await db.TextChunks
            .Where(tc => tc.PdfDocumentId == pdfGuid)
            .ToListAsync(cancellationToken).ConfigureAwait(false);
        if (existingChunks.Count > 0)
        {
            db.TextChunks.RemoveRange(existingChunks);
        }

        // Create TextChunkEntity for each document chunk (for FTS)
        var textChunkEntities = allDocumentChunks
            .Select((chunk, index) => new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                GameId = pdfDoc.GameId,
                PdfDocumentId = pdfGuid,
                Content = chunk.Text,
                ChunkIndex = index,
                PageNumber = chunk.Page,
                CharacterCount = chunk.Text.Length,
                CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
            })
            .ToList();

        db.TextChunks.AddRange(textChunkEntities);
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        _logger.LogInformation("Saved {ChunkCount} text chunks to PostgreSQL for hybrid search (PDF {PdfId})",
            textChunkEntities.Count, pdfId);
    }

    /// <summary>
    /// Finalizes PDF processing with completion status and quota confirmation.
    /// </summary>
    private async Task FinalizeProcessingAsync(
        string pdfId,
        PdfDocumentEntity pdfDoc,
        Guid userId,
        MeepleAiDbContext db,
        IPdfUploadQuotaService quotaService,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        var totalPages = pdfDoc.PageCount ?? 0;
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Completed, totalPages, totalPages, startTime, null, cancellationToken).ConfigureAwait(false);

        pdfDoc.ProcessingState = "Ready";
        pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        // Publish PdfStateChangedEvent so downstream handlers fire:
        // AutoCreateAgentOnPdfReadyHandler (admin PDFs), PdfNotificationEventHandler, PdfStateChangedMetricsEventHandler.
        // Must be published after SaveChanges so handlers can query the updated entity.
        if (Guid.TryParse(pdfId, out var pdfGuidForEvent))
        {
            await _mediator.Publish(
                new PdfStateChangedEvent(pdfGuidForEvent, PdfProcessingState.Indexing, PdfProcessingState.Ready, userId),
                CancellationToken.None).ConfigureAwait(false);
        }

        var cacheKey = (pdfDoc.PrivateGameId ?? pdfDoc.GameId)?.ToString() ?? string.Empty;
        await InvalidateCacheSafelyAsync(cacheKey, "PDF processing", cancellationToken).ConfigureAwait(false);

        // Two-Phase Quota (#1743): Confirm quota (Phase 2)
        await quotaService.ConfirmQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);

        _logger.LogInformation("PDF processing completed for {PdfId}", pdfId);
    }

    /// <summary>
    /// Handles processing cancellation with cleanup.
    /// </summary>
    private async Task HandleProcessingCancellationAsync(
        string pdfId,
        Guid userId,
        MeepleAiDbContext db,
        IPdfUploadQuotaService quotaService,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("PDF processing cancelled for {PdfId}", pdfId);
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, "Processing cancelled by user", cancellationToken).ConfigureAwait(false);
        await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);

        if (Guid.TryParse(pdfId, out var cancelledPdfGuid))
        {
            var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { cancelledPdfGuid }, CancellationToken.None).ConfigureAwait(false);
            if (pdfDoc != null)
            {
                pdfDoc.ProcessingState = "Failed";
                pdfDoc.ProcessingError = "Processing cancelled by user";
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            }
        }
    }

    /// <summary>
    /// Handles processing errors with consistent logging and cleanup.
    /// </summary>
    private async Task HandleProcessingErrorAsync(
        string pdfId,
        Guid userId,
        MeepleAiDbContext db,
        IPdfUploadQuotaService quotaService,
        DateTime startTime,
        Exception ex,
        string errorMessage,
        CancellationToken cancellationToken = default
        )
    {
        _logger.LogError(ex, "Error during PDF processing for {PdfId}: {ErrorType}", pdfId, ex.GetType().Name);
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, errorMessage, cancellationToken).ConfigureAwait(false);
        await quotaService.ReleaseQuotaAsync(userId, pdfId, cancellationToken).ConfigureAwait(false);

        if (Guid.TryParse(pdfId, out var errorPdfGuid))
        {
            var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { errorPdfGuid }, cancellationToken).ConfigureAwait(false);
            if (pdfDoc != null)
            {
                pdfDoc.ProcessingState = "Failed";
                pdfDoc.ProcessingError = errorMessage;
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
        }
    }

    private async Task UpdateProgressAsync(
        MeepleAiDbContext db,
        string pdfId,
        ProcessingStep step,
        int pagesProcessed,
        int totalPages,
        DateTime startTime,
        string? errorMessage,
        CancellationToken cancellationToken)
    {
        try
        {
            if (!Guid.TryParse(pdfId, out var pdfGuid))
            {
                _logger.LogWarning("Invalid PDF ID format for progress update: {PdfId}", pdfId);
                return;
            }

            var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfGuid }, cancellationToken).ConfigureAwait(false);
            if (pdfDoc == null) return;

            var elapsed = _timeProvider.GetUtcNow().UtcDateTime - startTime;
            var percentComplete = ProcessingProgress.CalculatePercentComplete(step, pagesProcessed, totalPages);
            var estimatedRemaining = ProcessingProgress.EstimateTimeRemaining(percentComplete, elapsed);

            pdfDoc.ProcessingProgress = new ProcessingProgress
            {
                CurrentStep = step,
                PercentComplete = percentComplete,
                ElapsedTime = elapsed,
                EstimatedTimeRemaining = estimatedRemaining,
                PagesProcessed = pagesProcessed,
                TotalPages = totalPages,
                StartedAt = startTime,
                CompletedAt = step == ProcessingStep.Completed || step == ProcessingStep.Failed ? _timeProvider.GetUtcNow().UtcDateTime : null,
                ErrorMessage = errorMessage
            };

            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogDebug("Updated progress for PDF {PdfId}: {Step} {Percent}%", pdfId, step, percentComplete);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateException ex)
        {
            _logger.LogWarning(ex, "Database error updating progress for PDF {PdfId}", pdfId);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // CLEANUP PATTERN: Progress updates are non-critical telemetry;
        // failures must not interrupt PDF processing workflow.
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogWarning(ex, "Unexpected error updating progress for PDF {PdfId}", pdfId);
        }
    }

    private static bool IsInvalidVector(float[]? vector)
    {
        return vector == null
            || vector.Length == 0
            || Array.Exists(vector, v => float.IsNaN(v) || float.IsInfinity(v));
    }

    /// <summary>
    /// BGAI-043: Records PDF pipeline step metrics in fire-and-forget pattern
    /// </summary>
    private void RecordPipelineMetricSafely(string step, double durationMs, int? count = null)
    {
        _ = Task.Run(() =>
        {
            try
            {
                if (string.Equals(step, "pages_processed", StringComparison.Ordinal) && count.HasValue)
                {
                    MeepleAiMetrics.PdfPagesProcessed.Add(count.Value);
                }
                else if (string.Equals(step, "extraction_error", StringComparison.Ordinal))
                {
                    MeepleAiMetrics.PdfExtractionErrors.Add(1);
                }
                else
                {
                    MeepleAiMetrics.RecordPdfPipelineStep(step, durationMs, count);
                }
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record PDF pipeline metric for step {Step}", step);
            }
        });
    }
}
