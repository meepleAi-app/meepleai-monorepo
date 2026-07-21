using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Models;
using Api.Observability;
using Api.Services;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
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
            var pdfDoc = await ValidateAndPrepareProcessingAsync(pdfId, userId, db, scope, quotaService, cancellationToken).ConfigureAwait(false);
            if (pdfDoc == null)
            {
                _logger.LogWarning("⚠️ [PDF-DEBUG] ValidateAndPrepareProcessingAsync returned null for {PdfId} - EARLY EXIT", pdfId);
                return; // Validation failed
            }
            _logger.LogInformation("✅ [PDF-DEBUG] Validation passed for {PdfId}, State: {State}", pdfId, pdfDoc.ProcessingState);

            // #2284 follow-up: transition Uploading → Extracting via domain so the state
            // machine + structural event raising stays consistent across the pipeline.
            // (Replaces the bridge-save hack from PR C which set ProcessingState=Indexing
            // directly via EF in FinalizeProcessingAsync.)
            var pdfGuid = Guid.Parse(pdfId);
            await TransitionStateAsync(scope, db, pdfGuid, PdfProcessingState.Extracting, cancellationToken).ConfigureAwait(false);

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

            await TransitionStateAsync(scope, db, pdfGuid, PdfProcessingState.Chunking, cancellationToken).ConfigureAwait(false);

            // Step 2: Chunk text with page tracking (40-60%)
            _logger.LogInformation("✂️ [PDF-DEBUG] Step 2: Starting ChunkExtractedTextAsync for {PdfId}", pdfId);
            var allDocumentChunks = await ChunkExtractedTextAsync(
                pdfId, fullText!, extractResult!, pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId,
                db, scope, startTime, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("✅ [PDF-DEBUG] Chunking SUCCESS: {ChunkCount} chunks created", allDocumentChunks.Count);

            await TransitionStateAsync(scope, db, pdfGuid, PdfProcessingState.Embedding, cancellationToken).ConfigureAwait(false);

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

            await TransitionStateAsync(scope, db, pdfGuid, PdfProcessingState.Indexing, cancellationToken).ConfigureAwait(false);

            // Step 4: Index in pgvector (80-100%)
            _logger.LogInformation("🔍 [PDF-DEBUG] Step 4: Starting IndexInVectorStoreAsync for {PdfId}", pdfId);
            await IndexInVectorStoreAsync(
                pdfId, userId, pdfDoc, allDocumentChunks, embeddings!,
                db, scope, startTime, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("✅ [PDF-DEBUG] Indexing completed for {PdfId}", pdfId);

            // Complete processing
            _logger.LogInformation("🎉 [PDF-DEBUG] Step 5: Finalizing processing for {PdfId}", pdfId);
            // Resolve mediator from the local async scope. The handler-injected `_mediator`
            // belongs to the original HTTP request scope, which has long since been disposed
            // by the time this background task reaches finalize for a multi-MB PDF.
            var scopedMediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            // #2284 PR C: resolve IPdfDocumentRepository from scope (ADR-063 pattern) so
            // FinalizeProcessingAsync can load the domain aggregate + TransitionTo(Ready)
            // structurally instead of the legacy direct EF mutation + manual mediator publish.
            var pdfRepo = scope.ServiceProvider.GetRequiredService<Api.BoundedContexts.DocumentProcessing.Domain.Repositories.IPdfDocumentRepository>();
            await FinalizeProcessingAsync(pdfId, pdfDoc, userId, db, quotaService, scopedMediator, pdfRepo, startTime, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation("✅ [PDF-DEBUG] ProcessPdfAsync COMPLETE for {PdfId}", pdfId);
        }
        catch (OperationCanceledException)
        {
            await HandleProcessingCancellationAsync(pdfId, userId, db, scope, quotaService, startTime, cancellationToken).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            await HandleProcessingErrorAsync(pdfId, userId, db, scope, quotaService, startTime, ex, "Invalid operation", cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            await HandleProcessingErrorAsync(pdfId, userId, db, scope, quotaService, startTime, ex, "Database error occurred", cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // Do not catch general exception types
#pragma warning disable S125 // Sections of code should not be commented out
        // BACKGROUND SERVICE: PDF processing runs async; must catch all exceptions
        // to properly update document status, release quota, and prevent background task crash
#pragma warning restore S125
        catch (Exception ex)
#pragma warning restore CA1031
        {
            await HandleProcessingErrorAsync(pdfId, userId, db, scope, quotaService, startTime, ex, ex.Message, cancellationToken).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Validates PDF ID and prepares document for processing with idempotency check.
    /// </summary>
    private async Task<PdfDocumentEntity?> ValidateAndPrepareProcessingAsync(
        string pdfId,
        Guid userId,
        MeepleAiDbContext db,
        IServiceScope scope,
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
        // AsTracking required: DbContext default is NoTracking (PERF-06), and FindAsync
        // does not override per-DbContext default behavior — entity would otherwise be
        // returned untracked and all mutations to it during the pipeline would be silently
        // dropped at SaveChangesAsync.
        var pdfDoc = await db.PdfDocuments
            .AsTracking()
            .FirstOrDefaultAsync(p => p.Id == pdfGuid, cancellationToken)
            .ConfigureAwait(false);
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

        // Mark as processing — transition through the domain so PdfStateChangedEvent
        // (Pending → Uploading) is raised structurally instead of via direct EF mutation.
        // #2284 follow-up: closes TD3.
        _logger.LogInformation("🔄 [PDF-DEBUG-VALIDATE] Updating status from 'pending' to 'uploading' via domain");
        try
        {
            await TransitionStateAsync(scope, db, pdfGuid, PdfProcessingState.Uploading, cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException)
        {
            // TransitionStateAsync already recorded the metric + logged before throwing.
            return null; // Null signals to caller that preparation failed; pipeline will skip
        }
        _logger.LogInformation("✅ [PDF-DEBUG-VALIDATE] Status updated, proceeding with processing");

        // Reload the EF snapshot the rest of ProcessPdfAsync uses — TransitionStateAsync
        // persisted the new state via the repository (which detaches the existing tracked
        // entity), so the local pdfDoc reference is stale w.r.t. ProcessingState.
        pdfDoc = await db.PdfDocuments.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pdfGuid, cancellationToken).ConfigureAwait(false);
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
        // Task 4: bucket key decoupled from gameId — uses pdf.Id (see PdfStorageKey + rebucket scripts)
        var bucketKey = PdfStorageKey.ForPdf(pdfDoc.Id);
        var fileStream = await _blobStorageService.RetrieveAsync(pdfId, BlobCategory.Pdf, bucketKey, cancellationToken).ConfigureAwait(false);
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
                // #2284 follow-up: TD2 — transition Failed via domain (raises
                // PdfStateChangedEvent(Extracting → Failed) + PdfFailedEvent).
                await TransitionToFailedAsync(
                    scope, db, Guid.Parse(pdfId), extractResult.ErrorMessage ?? "Extraction failed",
                    Api.BoundedContexts.DocumentProcessing.Domain.Enums.ErrorCategory.Parsing,
                    PdfProcessingState.Extracting, cancellationToken).ConfigureAwait(false);
                return (false, null, null);
            }

            var fullText = string.Join("\n\n", extractResult.PageChunks
                .Where(pc => !pc.IsEmpty)
                .Select(pc => pc.Text));

            pdfDoc.ExtractedText = fullText;
            pdfDoc.StructuredElementsJson = StructuredElementsPayload.Serialize(extractResult.StructuredElements);
            pdfDoc.PageCount = extractResult.TotalPages;
            pdfDoc.CharacterCount = extractResult.TotalCharacters;
            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                MeepleAiMetrics.RecordPdfConcurrencyConflict(
                    nameof(UploadPdfCommandHandler),
                    MeepleAiMetrics.PdfConcurrencyCategories.B);
                _logger.LogWarning(ex,
                    "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                    pdfId, nameof(UploadPdfCommandHandler));
                return (false, null, null);
            }

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
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(UploadPdfCommandHandler),
                MeepleAiMetrics.PdfConcurrencyCategories.B);
            _logger.LogWarning(ex,
                "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                pdfDoc.Id, nameof(UploadPdfCommandHandler));
        }
    }

    /// <summary>
    /// Chunks extracted PDF text into document chunks for embedding.
    /// </summary>
    private async Task<List<DocumentChunkInput>> ChunkExtractedTextAsync(
        string pdfId,
        string fullText,
        PagedTextExtractionResult extractResult,
        Guid? gameId,
        MeepleAiDbContext db,
        IServiceScope scope,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Chunking, 0, extractResult.TotalPages, startTime, null, cancellationToken).ConfigureAwait(false);

        var chunkingStopwatch = Stopwatch.StartNew();
        var headingAwareChunker = scope.ServiceProvider.GetService<IHeadingAwareChunker>();

        List<DocumentChunkInput> allDocumentChunks;
        if (headingAwareChunker != null)
        {
            allDocumentChunks = await headingAwareChunker.ChunkAsync(
                Guid.Parse(pdfId), gameId, extractResult.StructuredElements, fullText, cancellationToken).ConfigureAwait(false);
        }
        else
        {
            var chunkingService = scope.ServiceProvider.GetRequiredService<ITextChunkingService>();
            const int chunkSize = 512;
            const int chunkOverlap = 50;
            allDocumentChunks = chunkingService.PrepareForEmbedding(fullText, chunkSize, chunkOverlap)
                ?.Where(chunk => chunk != null && !string.IsNullOrWhiteSpace(chunk.Text))
                .Select(chunk => new DocumentChunkInput { Text = chunk.Text, Page = chunk.Page, CharStart = chunk.CharStart, CharEnd = chunk.CharEnd })
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

            var batchResult = await embeddingService.GenerateEmbeddingsAsync(
                batchTexts, pdfDoc.Language ?? "en").ConfigureAwait(false);

            if (!batchResult.Success)
            {
                _logger.LogError("❌ [BATCH-EMBED] Batch {Current}/{Total} FAILED: {Error}",
                    batchIndex + 1, batchCount, batchResult.ErrorMessage);
                await HandleEmbeddingFailureAsync(pdfId, userId, pdfDoc, db, scope, quotaService, startTime,
                    $"Embedding generation failed at batch {batchIndex + 1}/{batchCount}: {batchResult.ErrorMessage}", cancellationToken).ConfigureAwait(false);
                return (false, null);
            }

            if (batchResult.Embeddings == null || batchResult.Embeddings.Count != batchTexts.Count)
            {
                var mismatch = $"Batch {batchIndex + 1} returned {batchResult.Embeddings?.Count ?? 0} embeddings for {batchTexts.Count} texts";
                _logger.LogError("❌ [BATCH-EMBED] {Mismatch}", mismatch);
                await HandleEmbeddingFailureAsync(pdfId, userId, pdfDoc, db, scope, quotaService, startTime, mismatch, cancellationToken).ConfigureAwait(false);
                return (false, null);
            }

            // Validate batch quality
            foreach (var embedding in batchResult.Embeddings)
            {
                if (IsInvalidVector(embedding))
                {
                    var error = $"Invalid embedding detected in batch {batchIndex + 1}";
                    _logger.LogError("❌ [BATCH-EMBED] {Error}", error);
                    await HandleEmbeddingFailureAsync(pdfId, userId, pdfDoc, db, scope, quotaService, startTime, error, cancellationToken).ConfigureAwait(false);
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
            await HandleEmbeddingFailureAsync(pdfId, userId, pdfDoc, db, scope, quotaService, startTime, mismatch, cancellationToken).ConfigureAwait(false);
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
        IServiceScope scope,
        IPdfUploadQuotaService quotaService,
        DateTime startTime,
        string errorMessage,
        CancellationToken cancellationToken)
    {
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, errorMessage, cancellationToken).ConfigureAwait(false);
        await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);
        // #2284 follow-up: TD2 — transition Failed via domain. ErrorCategory.Service
        // because embedding failure typically signals the embedding microservice is
        // unreachable or returning malformed responses.
        await TransitionToFailedAsync(
            scope, db, Guid.Parse(pdfId), errorMessage,
            Api.BoundedContexts.DocumentProcessing.Domain.Enums.ErrorCategory.Service,
            PdfProcessingState.Embedding, cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Indexes document chunks in pgvector and PostgreSQL for hybrid search.
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

        // Create/update VectorDocument row FIRST so SaveEmbeddingsToPgVectorAsync can find it by PdfDocumentId
        await UpdateVectorDocumentAsync(pdfId, pdfDoc, allDocumentChunks.Count, db, scope, cancellationToken).ConfigureAwait(false);

        // Save text chunks to PostgreSQL for hybrid search (FTS) — non-blocking, can proceed independently
        await SaveTextChunksForHybridSearchAsync(pdfId, pdfDoc, allDocumentChunks, db, scope, cancellationToken).ConfigureAwait(false);

        // Persist embeddings to pgvector — critical path; VectorDocument row is guaranteed to exist above
        var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
        var modelName = embeddingService.GetModelName();
        await SaveEmbeddingsToPgVectorAsync(pdfId, pdfDoc, allDocumentChunks, embeddings, db, modelName, cancellationToken).ConfigureAwait(false);

        indexingStopwatch.Stop();
        RecordPipelineMetricSafely("indexing", indexingStopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// Updates or creates VectorDocument record after successful indexing.
    /// Delegates to <see cref="IPdfIndexingPipeline"/> (#2244 / epic #2242) so the
    /// VectorDocumentIndexedEvent fires structurally and shared_games.has_knowledge_base
    /// projection updates — replaces the direct EF write that bypassed the domain event.
    /// </summary>
    private async Task UpdateVectorDocumentAsync(
        string pdfId,
        PdfDocumentEntity pdfDoc,
        int indexedCount,
        MeepleAiDbContext db,
        IServiceScope scope,
        CancellationToken cancellationToken)
    {
        var pdfGuid = Guid.Parse(pdfId);

        // vector_documents.GameId is FK to games.Id (NOT shared_games.id) — see PdfGameIdResolver.
        var resolvedGameId = await PdfGameIdResolver.ResolveAsync(db, pdfDoc, cancellationToken)
            .ConfigureAwait(false);

        var pipeline = scope.ServiceProvider.GetRequiredService<IPdfIndexingPipeline>();
        await pipeline.IndexAsync(
            pdfDocumentId: pdfGuid,
            gameId: resolvedGameId,
            sharedGameId: pdfDoc.SharedGameId,
            chunkCount: indexedCount,
            totalCharacters: pdfDoc.ExtractedText?.Length ?? 0,
            language: string.IsNullOrWhiteSpace(pdfDoc.Language) ? "en" : pdfDoc.Language,
            cancellationToken: cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves text chunks to PostgreSQL for hybrid search with FTS.
    /// </summary>
    private async Task SaveTextChunksForHybridSearchAsync(
        string pdfId,
        PdfDocumentEntity pdfDoc,
        List<DocumentChunkInput> allDocumentChunks,
        MeepleAiDbContext db,
        IServiceScope scope,
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
        // text_chunks.GameId is FK to games.Id (NOT shared_games.id) — see PdfGameIdResolver.
        var textChunkGameId = await PdfGameIdResolver.ResolveAsync(db, pdfDoc, cancellationToken)
            .ConfigureAwait(false);
        var textChunkEntities = allDocumentChunks
            .Select((chunk, index) => new TextChunkEntity
            {
                Id = Guid.NewGuid(),
                GameId = textChunkGameId,
                SharedGameId = pdfDoc.SharedGameId,
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
        // text_chunks.role_tags is populated on insert. Resolved from the local
        // async scope (background task lifetime != original HTTP request scope).
        var roleClassifier = scope.ServiceProvider
            .GetService<Api.BoundedContexts.KnowledgeBase.Application.Services.IRoleClassifierService>();
        await TextChunkRoleClassifier.AssignRoleTagsAsync(
            roleClassifier, textChunkEntities, allDocumentChunks, _logger, cancellationToken)
            .ConfigureAwait(false);

        db.TextChunks.AddRange(textChunkEntities);
        try
        {
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(UploadPdfCommandHandler),
                MeepleAiMetrics.PdfConcurrencyCategories.B);
            _logger.LogWarning(ex,
                "Concurrency conflict on TextChunks for PDF {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                pdfId, nameof(UploadPdfCommandHandler));
            return;
        }

        _logger.LogInformation("Saved {ChunkCount} text chunks to PostgreSQL for hybrid search (PDF {PdfId})",
            textChunkEntities.Count, pdfId);
    }

    /// <summary>
    /// Persists embeddings to the pgvector_embeddings table for semantic search.
    /// Replaces the removed pgvector indexing path.
    /// </summary>
    private async Task SaveEmbeddingsToPgVectorAsync(
        string pdfId,
        PdfDocumentEntity pdfDoc,
        List<DocumentChunkInput> chunks,
        List<float[]> embeddings,
        MeepleAiDbContext db,
        string modelName,
        CancellationToken cancellationToken)
    {
        var pdfGuid = Guid.Parse(pdfId);
        var gameId = pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId ?? Guid.Empty;
        var language = pdfDoc.Language ?? "en";

        // Find VectorDocument for this PDF (read-only here — no AsTracking needed)
        var vectorDoc = await db.VectorDocuments
            .AsTracking()
            .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfGuid, cancellationToken)
            .ConfigureAwait(false);

        if (vectorDoc == null)
        {
            throw new InvalidOperationException(
                $"VectorDocument not found for PDF {pdfId} — cannot persist pgvector embeddings");
        }

        // Remove existing embeddings (re-index scenario)
        var existing = await db.PgVectorEmbeddings
            .Where(e => e.VectorDocumentId == vectorDoc.Id)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
        if (existing.Count > 0)
        {
            db.PgVectorEmbeddings.RemoveRange(existing);
            _logger.LogInformation("🗑️ [PG-VECTOR] Removed {Count} existing embeddings for {PdfId}", existing.Count, pdfId);
        }

        // Save in batches of 500
        const int saveBatchSize = 500;
        var batchCount = (int)Math.Ceiling((double)chunks.Count / saveBatchSize);

        for (var batchIdx = 0; batchIdx < batchCount; batchIdx++)
        {
            var skip = batchIdx * saveBatchSize;
            var batchChunks = chunks.Skip(skip).Take(saveBatchSize).ToList();
            var batchEmbeddings = embeddings.Skip(skip).Take(saveBatchSize).ToList();

            var entities = batchChunks.Select((chunk, i) => new PgVectorEmbeddingEntity
            {
                Id = Guid.NewGuid(),
                VectorDocumentId = vectorDoc.Id,
                GameId = gameId,
                TextContent = chunk.Text,
                Vector = new Pgvector.Vector(batchEmbeddings[i]),
                Model = modelName,
                ChunkIndex = skip + i,
                PageNumber = Math.Max(1, chunk.Page),
                Lang = language,
                CreatedAt = DateTimeOffset.UtcNow
            }).ToList();

            await db.PgVectorEmbeddings.AddRangeAsync(entities, cancellationToken).ConfigureAwait(false);
            try
            {
                await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                MeepleAiMetrics.RecordPdfConcurrencyConflict(
                    nameof(UploadPdfCommandHandler),
                    MeepleAiMetrics.PdfConcurrencyCategories.B);
                _logger.LogWarning(ex,
                    "Concurrency conflict on PgVectorEmbeddings for PDF {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                    pdfId, nameof(UploadPdfCommandHandler));
                return;
            }

            _logger.LogInformation("💾 [PG-VECTOR] Saved batch {Idx}/{Total}: {Count} embeddings for PDF {PdfId}",
                batchIdx + 1, batchCount, entities.Count, pdfId);
        }

        _logger.LogInformation("✅ [PG-VECTOR] Completed pgvector indexing for PDF {PdfId}: {Total} embeddings saved",
            pdfId, chunks.Count);
    }

    /// <summary>
    /// Transitions the PdfDocument aggregate to the target state via the domain
    /// (PdfDocument.TransitionTo) so PdfStateChangedEvent is raised structurally and
    /// dispatched through MediatR via the IDomainEventCollector + DbContext SaveChanges flow.
    ///
    /// #2284 follow-up: replaces the bridge-save hack from PR C. Called once per pipeline
    /// step (Extracting/Chunking/Embedding/Indexing) so the state machine progresses through
    /// every valid transition. The earlier shortcut of jumping Uploading → Indexing via direct
    /// EF mutation tripped the state-machine guard ValidateStateTransition (PdfDocument.cs:463-468).
    /// </summary>
    private async Task TransitionStateAsync(
        IServiceScope scope,
        MeepleAiDbContext db,
        Guid pdfGuid,
        PdfProcessingState targetState,
        CancellationToken cancellationToken)
    {
        var pdfRepo = scope.ServiceProvider.GetRequiredService<Api.BoundedContexts.DocumentProcessing.Domain.Repositories.IPdfDocumentRepository>();
        var pdfDomain = await pdfRepo.GetByIdAsync(pdfGuid, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"PdfDocument {pdfGuid} not found while transitioning to {targetState}");

        try
        {
            pdfDomain.TransitionTo(targetState);
            await pdfRepo.UpdateAsync(pdfDomain, cancellationToken).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(UploadPdfCommandHandler),
                MeepleAiMetrics.PdfConcurrencyCategories.B);
            _logger.LogWarning(ex,
                "Concurrency conflict transitioning PdfDocument {PdfId} to {State} (Category B) — admin mutation wins, pipeline aborts",
                pdfGuid, targetState);
            throw; // Mid-pipeline concurrency IS a regression — let outer catch handle as Failed
        }
    }

    /// <summary>
    /// Marks the document as Failed via the domain aggregate (PdfDocument.MarkAsFailed)
    /// so PdfStateChangedEvent(&lt;prev&gt; → Failed) + PdfFailedEvent are raised
    /// structurally and dispatched through MediatR. Replaces the legacy pattern of
    /// `pdfDoc.ProcessingState = "Failed"; pdfDoc.ProcessingError = ...;
    /// pdfDoc.ProcessedAt = ...; db.SaveChangesAsync()` which raised no events.
    ///
    /// #2284 follow-up: closes TD2 across 4 error sites (line ~245, ~531, ~916, ~962
    /// pre-refactor) so failure transitions are observable by downstream metric +
    /// notification handlers, not silent.
    ///
    /// DbUpdateConcurrencyException is logged + swallowed (best-effort) because the
    /// caller already returns to the error path — re-throwing would mask the original
    /// failure reason.
    /// </summary>
    private async Task TransitionToFailedAsync(
        IServiceScope scope,
        MeepleAiDbContext db,
        Guid pdfGuid,
        string errorMessage,
        Api.BoundedContexts.DocumentProcessing.Domain.Enums.ErrorCategory category,
        PdfProcessingState? failedAtState,
        CancellationToken cancellationToken)
    {
        var pdfRepo = scope.ServiceProvider.GetRequiredService<Api.BoundedContexts.DocumentProcessing.Domain.Repositories.IPdfDocumentRepository>();
        var pdfDomain = await pdfRepo.GetByIdAsync(pdfGuid, cancellationToken).ConfigureAwait(false);
        if (pdfDomain is null)
        {
            // Pdf may have been deleted by an admin mutation; nothing structurally to
            // raise. The caller will still return its error path.
            _logger.LogWarning(
                "PdfDocument {PdfId} not found while transitioning to Failed (best-effort)",
                pdfGuid);
            return;
        }

        // failedAtState == null signals "I don't know which step failed — use the
        // aggregate's current state". Used by generic catch blocks (cancellation,
        // unexpected exception) where the upstream code can't pinpoint the step.
        var resolvedFailedAtState = failedAtState ?? pdfDomain.ProcessingState;

        try
        {
            pdfDomain.MarkAsFailed(errorMessage, category, resolvedFailedAtState);
            await pdfRepo.UpdateAsync(pdfDomain, cancellationToken).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(UploadPdfCommandHandler),
                MeepleAiMetrics.PdfConcurrencyCategories.B);
            _logger.LogWarning(ex,
                "Concurrency conflict transitioning PdfDocument {PdfId} to Failed (Category B) — admin mutation wins, error path still completes",
                pdfGuid);
            // Swallow: the caller already records the upstream failure reason; the
            // missing event is a tolerable observability gap relative to throwing here.
        }
    }

    /// <summary>
    /// Finalizes PDF processing with completion status and quota confirmation.
    /// #2284 PR C: drives the final state transition through PdfDocument.TransitionTo(Ready)
    /// via IPdfDocumentRepository so PdfStateChangedEvent + KbDocIndexedEvent are raised
    /// structurally (via IDomainEventCollector → DbContext SaveChanges dispatcher) instead
    /// of the legacy direct EF mutation + manual scopedMediator.Publish.
    ///
    /// #2284 follow-up: bridge-save (Uploading → Indexing via direct EF) removed because the
    /// pipeline now transitions through every state via TransitionStateAsync. When this method
    /// runs the DB state is Indexing — TransitionTo(Ready) is a valid domain transition.
    /// </summary>
    private async Task FinalizeProcessingAsync(
        string pdfId,
        PdfDocumentEntity pdfDoc,
        Guid userId,
        MeepleAiDbContext db,
        IPdfUploadQuotaService quotaService,
        IMediator scopedMediator,
        Api.BoundedContexts.DocumentProcessing.Domain.Repositories.IPdfDocumentRepository pdfRepo,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        var totalPages = pdfDoc.PageCount ?? 0;
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Completed, totalPages, totalPages, startTime, null, cancellationToken).ConfigureAwait(false);

        var pdfGuid = Guid.Parse(pdfId);

        // Load aggregate, call TransitionTo(Ready) + MarkProcessed(now) so
        // PdfStateChangedEvent + KbDocIndexedEvent are raised structurally and
        // ProcessedAt is set via the domain (not via direct EF mutation). Persist
        // via repository so IDomainEventCollector picks the events up and the
        // DbContext SaveChanges dispatcher publishes them through MediatR.
        //
        // #2284 follow-up: closes TD1 — the legacy `pdfDoc.ProcessedAt = _timeProvider...`
        // EF mutation that survived PR #2297 is replaced by pdfDomain.MarkProcessed(now).
        var pdfDomain = await pdfRepo.GetByIdAsync(pdfGuid, cancellationToken).ConfigureAwait(false)
            ?? throw new InvalidOperationException(
                $"PdfDocument {pdfGuid} not found while finalizing processing");

        try
        {
            pdfDomain.TransitionTo(PdfProcessingState.Ready);
            pdfDomain.MarkProcessed(_timeProvider.GetUtcNow().UtcDateTime);
            await pdfRepo.UpdateAsync(pdfDomain, cancellationToken).ConfigureAwait(false);
            await db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(UploadPdfCommandHandler),
                MeepleAiMetrics.PdfConcurrencyCategories.B);
            _logger.LogWarning(ex,
                "Concurrency conflict on PdfDocument {PdfId} (TransitionTo Ready) in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                pdfId, nameof(UploadPdfCommandHandler));
            return;
        }

        // #2284 PR C: tactical scopedMediator.Publish(PdfStateChangedEvent) deleted.
        // PdfDocument.TransitionTo(Ready) raises BOTH PdfStateChangedEvent AND
        // KbDocIndexedEvent structurally; the repository's UpdateAsync collects them via
        // CollectDomainEvents and the SaveChanges dispatcher publishes them through MediatR.
        // Downstream handlers (AutoCreateAgentOnPdfReadyHandler, PdfNotificationEventHandler,
        // PdfStateChangedMetricsEventHandler, activity-rail KbDocIndexedEventHandler) fire
        // via the same MediatR pipeline as before — no behavioural change at the handler
        // level, just the source of the event is now the domain aggregate.

        var cacheKey = (pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId)?.ToString() ?? string.Empty;
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
        IServiceScope scope,
        IPdfUploadQuotaService quotaService,
        DateTime startTime,
        CancellationToken cancellationToken)
    {
        _logger.LogInformation("PDF processing cancelled for {PdfId}", pdfId);
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, "Processing cancelled by user", cancellationToken).ConfigureAwait(false);
        await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);

        // #2284 follow-up: TD2 — transition Failed via domain. failedAtState=null →
        // resolved to current pdfDomain.ProcessingState (whatever step the cancellation
        // hit). Use CancellationToken.None because the cancellation token that triggered
        // this branch is already signalled; we still want to persist the Failed state.
        if (Guid.TryParse(pdfId, out var cancelledPdfGuid))
        {
            await TransitionToFailedAsync(
                scope, db, cancelledPdfGuid, "Processing cancelled by user",
                Api.BoundedContexts.DocumentProcessing.Domain.Enums.ErrorCategory.Unknown,
                failedAtState: null, CancellationToken.None).ConfigureAwait(false);
        }
    }

    /// <summary>
    /// Handles processing errors with consistent logging and cleanup.
    /// </summary>
    private async Task HandleProcessingErrorAsync(
        string pdfId,
        Guid userId,
        MeepleAiDbContext db,
        IServiceScope scope,
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

        // #2284 follow-up: TD2 — transition Failed via domain. failedAtState=null →
        // resolved to current pdfDomain.ProcessingState (the catch is generic, so the
        // upstream step that threw is whatever pdfDomain currently observes).
        if (Guid.TryParse(pdfId, out var errorPdfGuid))
        {
            await TransitionToFailedAsync(
                scope, db, errorPdfGuid, errorMessage,
                Api.BoundedContexts.DocumentProcessing.Domain.Enums.ErrorCategory.Unknown,
                failedAtState: null, cancellationToken).ConfigureAwait(false);
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

            // AsTracking required (DbContext default is NoTracking — PERF-06).
            var pdfDoc = await db.PdfDocuments
                .AsTracking()
                .FirstOrDefaultAsync(p => p.Id == pdfGuid, cancellationToken)
                .ConfigureAwait(false);
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
        catch (DbUpdateConcurrencyException ex)
        {
            MeepleAiMetrics.RecordPdfConcurrencyConflict(
                nameof(UploadPdfCommandHandler),
                MeepleAiMetrics.PdfConcurrencyCategories.B);
            _logger.LogWarning(ex,
                "Concurrency conflict on PdfDocument {PdfId} in {Handler} (Category B) — admin mutation wins, pipeline will re-read on next tick",
                pdfId, nameof(UploadPdfCommandHandler));
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
