using Api.BoundedContexts.DocumentProcessing.Application.Services;
using Api.BoundedContexts.DocumentProcessing.Application.Services.Chunking;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.KnowledgeBase.Application.Services;
using Api.BoundedContexts.KnowledgeBase.Application.Services.Chunking;
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

            // #4443 R1: the claim in ValidateAndPrepareProcessingAsync already moved the document to
            // Extracting; the later steps go through the domain (#2284) from there.
            var pdfGuid = Guid.Parse(pdfId);

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
                pdfId, fullText!, extractResult!, pdfDoc, db, scope, startTime, cancellationToken).ConfigureAwait(false);
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
        // Only a cancellation OF THE TASK'S TOKEN is the user's (CancelPdfProcessingCommand → CancelTask). A
        // TaskCanceledException from an HttpClient timeout leaves that token alone: it falls through to the
        // generic catch below, is recorded as a transient failure and stays retriable (#4610).
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            await HandleProcessingCancellationAsync(pdfId, userId, db, scope, quotaService, startTime).ConfigureAwait(false);
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
        // #4443: NOT tracked. This read only tells whether the document exists; the pipeline works on
        // the snapshot reloaded after the claim below. A tracked copy taken here would keep the
        // pre-claim xmin in the change tracker — the claim is a raw UPDATE — and the next save that
        // reaches it (the progress update) would hit 0 rows and abort the pipeline as a
        // concurrency conflict, leaving the document in Extracting.
        var pdfDoc = await db.PdfDocuments
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == pdfGuid, cancellationToken)
            .ConfigureAwait(false);
        if (pdfDoc == null)
        {
            _logger.LogError("❌ [PDF-DEBUG-VALIDATE] PDF document {PdfId} NOT FOUND in database", pdfId);
            await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);
            return null;
        }

        _logger.LogInformation("✅ [PDF-DEBUG-VALIDATE] PDF found, current state: {State}", pdfDoc.ProcessingState);

        // #4443 R1: take the document atomically (Pending → Extracting) with the same claim the queue
        // worker, the admin process-pending and recovery at startup use. This used to check «Pending»
        // (#1742) and then write Uploading in a separate step: an owner that claimed the document in
        // between left the aggregate in Extracting, the transition threw, and this task marked Failed
        // a document someone else was processing. Only one claimant wins; the other does nothing.
        var claimService = scope.ServiceProvider.GetRequiredService<IPdfClaimService>();
        if (!await claimService.TryClaimPendingAsync(pdfGuid, cancellationToken).ConfigureAwait(false))
        {
            var currentState = await db.PdfDocuments.AsNoTracking()
                .Where(p => p.Id == pdfGuid)
                .Select(p => p.ProcessingState)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "⏭️ [PDF-DEBUG-VALIDATE] PDF {PdfId} not pending (state: {State}): owned elsewhere or already finished, skipping",
                pdfId, currentState);

            if (string.Equals(currentState, nameof(PdfProcessingState.Failed), StringComparison.Ordinal))
            {
                await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);
            }

            return null;
        }
        _logger.LogInformation("✅ [PDF-DEBUG-VALIDATE] PDF {PdfId} claimed, proceeding with processing", pdfId);

        // The claim is a raw UPDATE, so no aggregate raised a PdfStateChangedEvent. Publish the one
        // transition it made: the handlers that react to any change (the uploader's document-list
        // cache) must see the document leave Pending now, not when chunking starts minutes later.
        try
        {
            await scope.ServiceProvider.GetRequiredService<IMediator>().Publish(
                new PdfStateChangedEvent(pdfGuid, PdfProcessingState.Pending, PdfProcessingState.Extracting, pdfDoc.UploadedByUserId),
                cancellationToken).ConfigureAwait(false);
        }
#pragma warning disable CA1031 // A notification failure must not stop the processing the claim just started
        catch (Exception ex) when (ex is not OperationCanceledException)
        {
            _logger.LogWarning(ex, "Failed to publish the claim transition of PDF {PdfId}", pdfId);
        }
#pragma warning restore CA1031

        // Reload the EF snapshot the rest of ProcessPdfAsync uses: the claim is a raw UPDATE, so the
        // tracked pdfDoc above is stale w.r.t. ProcessingState.
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
        // Issue #3846: the resourceKey can NOT be rebuilt from the pdfId. This very handler stores the
        // object under `gameId ?? privateGameId` (see UploadPdfCommandHandler.StoreAndRegisterAsync),
        // while the seeded corpus uses the pdfId — and both blob backends resolve by exact
        // {category}/{resourceKey}/{fileId}_ prefix with no cross-folder search. Reading with the
        // pdfId-derived folder therefore found nothing under S3, and the filesystem fallback below then
        // opened the persisted FilePath, which under S3 is an object KEY, not a path: hence the
        // misleading "Could not find a part of the path '/app/pdfs/<gameId>/…'". FilePath is the only
        // record of where the object actually went — read both halves from it, as the download path
        // already does (#3568). The ?? fallbacks preserve legacy behaviour for unparsable paths.
        var bucketKey = PdfStorageKey.ForPdf(pdfDoc.Id);
        var resourceKey = PdfStorageKey.ResourceKeyFromPath(pdfDoc.FilePath) ?? bucketKey;
        var fileId = PdfStorageKey.FileIdFromPath(pdfDoc.FilePath) ?? pdfId;
        var fileStream = await _blobStorageService.RetrieveAsync(fileId, BlobCategory.Pdf, resourceKey, cancellationToken).ConfigureAwait(false);
        if (fileStream == null)
        {
            // Fallback to local filesystem for backward compatibility (dev without S3).
            _logger.LogWarning("[PDF-DEBUG] Blob storage returned null for {PdfId}, falling back to filesystem: {FilePath}", pdfId, filePath);

            // #3846: under S3 `filePath` is an object KEY, not a path — opening it blindly resolved it
            // against the working directory and reported "Could not find a part of the path
            // '/app/pdfs/<gameId>/…'", a location that never existed, under a NULL error category that
            // RetryFailedPdfsJob keeps retrying. Name the key instead, and mark it non-retriable.
            if (!File.Exists(filePath))
            {
                var missing = new PdfStorageObjectMissingException(fileId, resourceKey, filePath);
                _logger.LogError(missing,
                    "[PDF-DEBUG] PDF {PdfId} is not in storage at key pdf/{ResourceKey}/{FileId}",
                    pdfId, resourceKey, fileId);
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, missing.Message, cancellationToken).ConfigureAwait(false);
                await TransitionToFailedAsync(
                    scope, db, pdfDoc.Id, missing.Message,
                    Api.BoundedContexts.DocumentProcessing.Domain.Enums.ErrorCategory.StorageObjectMissing,
                    PdfProcessingState.Extracting, cancellationToken).ConfigureAwait(false);
                return (false, null, null);
            }

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
            // Issue #3281: keep StructuredElementsJson in-memory on the (AsNoTracking) pdfDoc so the
            // tracked re-write in FinalizeProcessingAsync can copy it after the Ready transition. This
            // assignment + the SaveChangesAsync below are a no-op on the DB for this column (see the
            // FinalizeProcessingAsync comment for why), but the in-memory value must be set here since
            // extractResult is not in scope at finalize time.
            pdfDoc.StructuredElementsJson = extractResult.StructuredElements is null
                ? null
                : System.Text.Json.JsonSerializer.Serialize(extractResult.StructuredElements);
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

        // #3846: the extractor takes a FILE. Under S3 `filePath` is a bucket key, so it used to fail
        // File.Exists and return a failure result that the line below drops without a word — tables,
        // diagrams and atomic rules silently missing on every remote-storage upload, with the PDF
        // still reaching Ready. Materialize the object first, and say something when it does fail.
        using var localFile = await PdfLocalFile
            .AcquireAsync(_blobStorageService, pdfDoc.Id, filePath, cancellationToken)
            .ConfigureAwait(false);

        if (localFile is null)
        {
            _logger.LogWarning(
                "[PDF-DEBUG] Structured content skipped for PDF {PdfId}: object not available in storage ({FilePath})",
                pdfDoc.Id, filePath);
            return;
        }

        var structuredResult = await tableExtractor.ExtractStructuredContentAsync(localFile.Path, cancellationToken).ConfigureAwait(false);
        if (!structuredResult.Success)
        {
            _logger.LogWarning(
                "[PDF-DEBUG] Structured content extraction failed for PDF {PdfId}: {Error}",
                pdfDoc.Id, structuredResult.ErrorMessage);
            return;
        }

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
        PdfDocumentEntity pdfDoc,
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

        // Issue #3281: heading-aware production when AdvancedChunkingService is available in scope.
        var advancedChunking = scope.ServiceProvider.GetService<IAdvancedChunkingService>();
        List<DocumentChunkInput> allDocumentChunks;
        if (advancedChunking != null)
        {
            var hierarchical = await HeadingAwareChunker.BuildAsync(
                extractResult.StructuredElements,
                fullText,
                pdfDoc.Id,
                pdfDoc.PrivateGameId ?? pdfDoc.SharedGameId,
                advancedChunking,
                cancellationToken).ConfigureAwait(false);
            allDocumentChunks = HeadingAwareChunkAdapter.ToChunkInputs(hierarchical);
        }
        else
        {
            allDocumentChunks = chunkingService.PrepareForEmbedding(fullText, chunkSize, chunkOverlap)
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
            var batchTexts = batchChunks.Select(c => HeadingAwareChunkAdapter.CapForEmbedding(c.Text)).ToList();

            _logger.LogInformation("📦 [BATCH-EMBED] Processing batch {Current}/{Total}: {ChunkCount} chunks",
                batchIndex + 1, batchCount, batchTexts.Count);

            // #4531: the token goes down to the call. Without it a cancelled upload kept waiting for the
            // embedding service to answer this batch, and noticed the cancellation only afterwards.
            var batchResult = await embeddingService.GenerateEmbeddingsAsync(
                batchTexts, pdfDoc.Language ?? "en", cancellationToken).ConfigureAwait(false);

            // EmbeddingService turns every provider exception, cancellation included, into a failed result. Without
            // this line a cancelled batch would be logged as «BATCH-EMBED FAILED» at Error level and handed to
            // HandleEmbeddingFailureAsync as a service failure, when it is the user's cancellation.
            cancellationToken.ThrowIfCancellationRequested();

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

        // The VectorDocument row has to exist FIRST so the chunks and the pgvector rows can hang off it, but
        // only as "processing": it becomes "completed", and VectorDocumentIndexedEvent raises
        // has_knowledge_base, LAST, once the saves below are done (#4609). Declaring the index before the
        // saves left a "completed" document with no vector rows whenever one of them failed or was cancelled.
        var pipeline = scope.ServiceProvider.GetRequiredService<IPdfIndexingPipeline>();
        var links = await ResolveIndexingLinksAsync(pdfId, pdfDoc, allDocumentChunks.Count, db, cancellationToken).ConfigureAwait(false);
        await pipeline.BeginIndexingAsync(
            links.PdfGuid, links.GameId, pdfDoc.SharedGameId, links.ChunkCount, links.TotalCharacters, links.Language,
            cancellationToken).ConfigureAwait(false);

        // #4395: both writes go through PdfChunkPersistence, shared with the chunked upload.
        // #4384: text chunks BEFORE the pgvector rows, which point at them — moving this after the
        // embeddings, or running the two in parallel, brings back rows with a null or dangling
        // source_chunk_id.
        await PdfChunkPersistence.SaveAsync(
            db, Guid.Parse(pdfId), pdfDoc, allDocumentChunks, embeddings,
            scope.ServiceProvider.GetRequiredService<IEmbeddingService>().GetModelName(),
            scope.ServiceProvider.GetService<Api.BoundedContexts.KnowledgeBase.Application.Services.IRoleClassifierService>(),
            _timeProvider, _logger, nameof(UploadPdfCommandHandler), cancellationToken).ConfigureAwait(false);

        await pipeline.IndexAsync(
            links.PdfGuid, links.GameId, pdfDoc.SharedGameId, links.ChunkCount, links.TotalCharacters, links.Language,
            cancellationToken).ConfigureAwait(false);

        indexingStopwatch.Stop();
        RecordPipelineMetricSafely("indexing", indexingStopwatch.Elapsed.TotalMilliseconds);
    }

    /// <summary>
    /// The arguments both <see cref="IPdfIndexingPipeline"/> steps take (#2244 / epic #2242 / #4609):
    /// <c>BeginIndexingAsync</c> first, so the VectorDocument row exists, and <c>IndexAsync</c> last, so
    /// VectorDocumentIndexedEvent fires structurally and shared_games.has_knowledge_base updates only for an
    /// index that was actually written. Resolved once.
    /// </summary>
    private readonly record struct IndexingLinks(Guid PdfGuid, Guid? GameId, int ChunkCount, int TotalCharacters, string Language);

    private static async Task<IndexingLinks> ResolveIndexingLinksAsync(
        string pdfId,
        PdfDocumentEntity pdfDoc,
        int indexedCount,
        MeepleAiDbContext db,
        CancellationToken cancellationToken)
    {
        // vector_documents.GameId is FK to games.Id (NOT shared_games.id) — see PdfGameIdResolver.
        var resolvedGameId = await PdfGameIdResolver.ResolveAsync(db, pdfDoc, cancellationToken)
            .ConfigureAwait(false);

        return new IndexingLinks(
            Guid.Parse(pdfId),
            resolvedGameId,
            indexedCount,
            pdfDoc.ExtractedText?.Length ?? 0,
            string.IsNullOrWhiteSpace(pdfDoc.Language) ? "en" : pdfDoc.Language);
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
        // #4395: whatever failed may have left rows in the change tracker (chunks, vector rows), and the
        // save below would submit them again, fail again, and leave the PDF in flight forever.
        db.ChangeTracker.Clear();

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

        // #4339: Completed goes AFTER the Ready transition. Written before it, the repository's
        // UpdateAsync nulled it until #4347, and the upload page, which waits for Completed on
        // GET /pdfs/{id}/progress, stayed at "Indexing" forever. The order still matters: the transition
        // can lose a concurrency conflict (the return above), and a Completed for a PDF that is not Ready
        // would be a lie. CancellationToken.None for the same reason as the persist below: the PDF is Ready.
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Completed, totalPages, totalPages, startTime, null, CancellationToken.None).ConfigureAwait(false);

        // Issue #3281 / pre-existing AsNoTracking bug: the pipeline's working pdfDoc is AsNoTracking,
        // so extraction output written during ExtractPdfContentAsync/ExtractStructuredContentAsync never
        // persists. Re-persist it here on a freshly TRACKED entity AFTER the final Ready transition
        // (mirrors ExtractPdfTextCommandHandler). Until #4347 the repository transitions also nulled
        // these columns; they no longer do. Needed for re-index parity: IndexPdf reads
        // StructuredElementsJson and requires non-null ExtractedText.
        // 🟡 Use CancellationToken.None (NOT cancellationToken): the PDF is already Ready+indexed at
        // this point, so this persist MUST complete regardless of pipeline cancellation. Mirrors
        // ConfirmQuotaAsync's post-Ready CancellationToken.None convention. If cancellation were
        // threaded here, an OperationCanceledException would escape this DbUpdateConcurrencyException-only
        // catch → ProcessPdfAsync's cancellation handler → TransitionToFailedAsync → MarkAsFailed on a
        // Ready aggregate → InvalidOperationException("Cannot transition from Ready state"), AND the
        // extraction output would be silently lost.
        var tracked = await db.PdfDocuments.AsTracking()
            .FirstOrDefaultAsync(p => p.Id == pdfGuid, CancellationToken.None).ConfigureAwait(false);
        if (tracked != null)
        {
            tracked.ExtractedText = pdfDoc.ExtractedText;
            tracked.StructuredElementsJson = pdfDoc.StructuredElementsJson;
            tracked.PageCount = pdfDoc.PageCount;
            tracked.CharacterCount = pdfDoc.CharacterCount;
            tracked.ExtractedTables = pdfDoc.ExtractedTables;
            tracked.ExtractedDiagrams = pdfDoc.ExtractedDiagrams;
            tracked.AtomicRules = pdfDoc.AtomicRules;
            tracked.TableCount = pdfDoc.TableCount;
            tracked.DiagramCount = pdfDoc.DiagramCount;
            tracked.AtomicRuleCount = pdfDoc.AtomicRuleCount;
            try
            {
                await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
            }
            catch (DbUpdateConcurrencyException ex)
            {
                _logger.LogWarning(ex,
                    "Concurrency conflict persisting extraction output for PDF {PdfId} — admin mutation wins",
                    pdfId);
            }
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
    /// <remarks>
    /// <para>The token that triggered this branch is already signalled, so none of the writes below can use it
    /// (#4531). The progress update did, and <see cref="UpdateProgressAsync"/> rethrows
    /// <see cref="OperationCanceledException"/>: it threw at once, out of this method, and neither the quota
    /// release nor the Failed transition ran. The document stayed in the step it was in (Embedding,
    /// Indexing…) and the reservation was not given back.</para>
    /// <para>The document ends <c>Failed</c> with <see cref="ErrorCategory.Cancelled"/>: <c>RetryFailedPdfsJob</c>
    /// does not retry it and <c>PdfNotificationEventHandler</c> does not tell the user their own cancellation
    /// failed (#4610).</para>
    /// </remarks>
    private async Task HandleProcessingCancellationAsync(
        string pdfId,
        Guid userId,
        MeepleAiDbContext db,
        IServiceScope scope,
        IPdfUploadQuotaService quotaService,
        DateTime startTime)
    {
        _logger.LogInformation("PDF processing cancelled for {PdfId}", pdfId);

        // The step that was cancelled can leave work behind that must not be saved now that the token no longer
        // stops the saves: rows in the change tracker (text chunks or pgvector rows of a save that never went
        // through), and the domain events queued in the collector for a save that never committed (the
        // collector is drained only after a successful save). Same rule as TransitionToFailedAsync (#4395).
        // The collector is cleared as well: otherwise the next save writes to the outbox a state change that
        // the database never saw.
        db.ChangeTracker.Clear();
        scope.ServiceProvider.GetRequiredService<Api.SharedKernel.Application.Services.IDomainEventCollector>().Clear();

        var hasPdfGuid = Guid.TryParse(pdfId, out var cancelledPdfGuid);

        // The cancellation can arrive after the Ready commit, in the tail of FinalizeProcessingAsync (cache
        // invalidation, quota confirmation). The document is then done and indexed: marking it Failed would
        // rewrite its progress, give back a quota it consumed, and MarkAsFailed throws on a Ready aggregate.
        if (hasPdfGuid)
        {
            var currentState = await db.PdfDocuments.AsNoTracking()
                .Where(p => p.Id == cancelledPdfGuid)
                .Select(p => p.ProcessingState)
                .FirstOrDefaultAsync(CancellationToken.None).ConfigureAwait(false);
            if (string.Equals(currentState, nameof(PdfProcessingState.Ready), StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "PDF {PdfId} was cancelled after it became Ready: nothing to clean up", pdfId);
                return;
            }
        }

        await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, "Processing cancelled by user", CancellationToken.None).ConfigureAwait(false);
        await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);

        // #2284 follow-up: TD2 — transition Failed via domain. failedAtState=null →
        // resolved to current pdfDomain.ProcessingState (whatever step the cancellation
        // hit). Use CancellationToken.None because the cancellation token that triggered
        // this branch is already signalled; we still want to persist the Failed state.
        if (hasPdfGuid)
        {
            await TransitionToFailedAsync(
                scope, db, cancelledPdfGuid, "Processing cancelled by user",
                Api.BoundedContexts.DocumentProcessing.Domain.Enums.ErrorCategory.Cancelled,
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
