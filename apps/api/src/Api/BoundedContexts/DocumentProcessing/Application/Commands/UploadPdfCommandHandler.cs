using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.Constants;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Security;
using Api.Models;
using Api.Observability;
using Api.Services;
using Api.Services.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using System.Diagnostics;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

public class UploadPdfCommandHandler : ICommandHandler<UploadPdfCommand, PdfUploadResult>
{
    #region Fields & Constants

    private readonly MeepleAiDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<UploadPdfCommandHandler> _logger;
    private readonly IPdfTextExtractor _pdfTextExtractor;
    private readonly IPdfTableExtractor _tableExtractor;
    private readonly IBackgroundTaskService _backgroundTaskService;
    private readonly IAiResponseCacheService _cacheService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly IPdfUploadQuotaService _quotaService;
    private readonly TimeProvider _timeProvider;

    private const long MaxFileSizeBytes = FileConstants.MaxPdfFileSizeBytes;
    private static readonly HashSet<string> AllowedContentTypes = new() { "application/pdf" };

    #endregion

    #region Constructor

    public UploadPdfCommandHandler(
        MeepleAiDbContext db,
        IServiceScopeFactory scopeFactory,
        ILogger<UploadPdfCommandHandler> logger,
        IPdfTextExtractor pdfTextExtractor,
        IPdfTableExtractor tableExtractor,
        IBackgroundTaskService backgroundTaskService,
        IAiResponseCacheService cacheService,
        IBlobStorageService blobStorageService,
        IPdfUploadQuotaService quotaService,
        TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _scopeFactory = scopeFactory ?? throw new ArgumentNullException(nameof(scopeFactory));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _pdfTextExtractor = pdfTextExtractor ?? throw new ArgumentNullException(nameof(pdfTextExtractor));
        _tableExtractor = tableExtractor ?? throw new ArgumentNullException(nameof(tableExtractor));
        _backgroundTaskService = backgroundTaskService ?? throw new ArgumentNullException(nameof(backgroundTaskService));
        _cacheService = cacheService ?? throw new ArgumentNullException(nameof(cacheService));
        _blobStorageService = blobStorageService ?? throw new ArgumentNullException(nameof(blobStorageService));
        _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    #endregion

    #region Public Methods

    public async Task<PdfUploadResult> Handle(UploadPdfCommand command, CancellationToken cancellationToken)
    {
        var file = command.File;
        var gameId = command.GameId;
        var userId = command.UserId;

        // Validate file
        if (file == null || file.Length == 0)
        {
            RecordUploadMetricSafely("validation_failed_empty", null);
            return new PdfUploadResult(false, "No file provided. Please select a PDF file to upload.", null);
        }

        if (file.Length > MaxFileSizeBytes)
        {
            var sizeMB = file.Length / 1024.0 / 1024.0;
            var maxMB = MaxFileSizeBytes / 1024 / 1024;
            RecordUploadMetricSafely("validation_failed_size", file.Length);
            return new PdfUploadResult(false, $"File is too large ({sizeMB:F1}MB). Maximum size is {maxMB}MB. Try compressing the PDF or splitting into smaller files.", null);
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            RecordUploadMetricSafely("validation_failed_type", file.Length);
            return new PdfUploadResult(false, $"Invalid file type ({file.ContentType}). Only PDF files are allowed. Please ensure your file has a .pdf extension.", null);
        }

        // SEC-738: Extract and sanitize filename to prevent path injection (CWE-22, CWE-73)
        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new PdfUploadResult(false, "Invalid file name. The file must have a valid name.", null);
        }

        // Defense-in-depth: Validate filename at entry point before passing to storage service
        try
        {
            fileName = PathSecurity.SanitizeFilename(fileName);
        }
        catch (ArgumentException ex)
        {
            return new PdfUploadResult(false, $"Invalid file name: {ex.Message}", null);
        }

        // Verify game exists
        var game = await _db.Games
            .Where(g => g.Id.ToString() == gameId)
            .FirstOrDefaultAsync(cancellationToken);

        if (game == null)
        {
            return new PdfUploadResult(false, "Game not found. Please select a valid game before uploading.", null);
        }

        // Check upload quota (Admin and Editor bypass quota checks)
        var user = await _db.Users
            .Where(u => u.Id == userId)
            .FirstOrDefaultAsync(cancellationToken);

        if (user == null)
        {
            RecordUploadMetricSafely("user_not_found", file.Length);
            _logger.LogError("User {UserId} not found during PDF upload", userId);
            return new PdfUploadResult(false, "User not found. Please ensure you are authenticated.", null);
        }

        var quotaResult = await _quotaService.CheckQuotaAsync(
            user.Id,
            user.Tier,
            user.Role,
            cancellationToken);

        if (!quotaResult.Allowed)
        {
            RecordUploadMetricSafely("quota_exceeded", file.Length);
            _logger.LogWarning(
                "PDF upload denied for user {UserId} ({Tier}): {Reason}",
                userId,
                user.Tier,
                quotaResult.ErrorMessage);
            return new PdfUploadResult(false, quotaResult.ErrorMessage!, null);
        }

        _logger.LogDebug(
            "PDF upload quota check passed for user {UserId} ({Tier}): Daily {DailyUsed}/{DailyLimit}, Weekly {WeeklyUsed}/{WeeklyLimit}",
            userId,
            user.Tier,
            quotaResult.DailyUploadsUsed,
            quotaResult.DailyLimit,
            quotaResult.WeeklyUploadsUsed,
            quotaResult.WeeklyLimit);

        try
        {
            // Delegate file storage to BlobStorageService
            BlobStorageResult storageResult;
            using (var stream = file.OpenReadStream())
            {
                storageResult = await _blobStorageService.StoreAsync(stream, fileName, gameId, cancellationToken);
            }

            if (!storageResult.Success)
            {
                RecordUploadMetricSafely("storage_failed", file.Length);
                return new PdfUploadResult(false, storageResult.ErrorMessage ?? "Failed to store file", null);
            }

            _logger.LogInformation("Saved PDF file to {FilePath}", storageResult.FilePath);

            // Create database record
            var pdfDoc = new PdfDocumentEntity
            {
                Id = Guid.Parse(storageResult.FileId!),
                GameId = Guid.Parse(gameId),
                FileName = fileName,
                FilePath = storageResult.FilePath!,
                FileSizeBytes = storageResult.FileSizeBytes,
                ContentType = file.ContentType,
                UploadedByUserId = userId,
                UploadedAt = _timeProvider.GetUtcNow().UtcDateTime,
                ProcessingStatus = "pending"
            };

            _db.PdfDocuments.Add(pdfDoc);
            await _db.SaveChangesAsync(cancellationToken);

            _logger.LogInformation("Created PDF document record {PdfId} for game {GameId}", storageResult.FileId, gameId);

            // PDF-08: Initialize progress tracking
            pdfDoc.ProcessingProgress = new ProcessingProgress
            {
                CurrentStep = ProcessingStep.Uploading,
                PercentComplete = 20,
                ElapsedTime = TimeSpan.Zero,
                EstimatedTimeRemaining = null,
                PagesProcessed = 0,
                TotalPages = 0,
                StartedAt = _timeProvider.GetUtcNow().UtcDateTime,
                CompletedAt = null,
                ErrorMessage = null
            };
            await _db.SaveChangesAsync(cancellationToken);

            // Extract text asynchronously (PDF-02) with cancellation support (PDF-08)
            _backgroundTaskService.ExecuteWithCancellation(storageResult.FileId!, (ct) => ProcessPdfAsync(storageResult.FileId!, storageResult.FilePath!, ct));

            // Increment upload count after successful upload
            await _quotaService.IncrementUploadCountAsync(userId, cancellationToken);

            await InvalidateCacheSafelyAsync(gameId, cancellationToken, "PDF upload");

            // BGAI-043: Record successful upload
            RecordUploadMetricSafely("success", file.Length);

            return new PdfUploadResult(true, "PDF uploaded successfully", new PdfDocumentDto(
                Id: pdfDoc.Id,
                GameId: pdfDoc.GameId,
                FileName: pdfDoc.FileName,
                FilePath: pdfDoc.FilePath,
                FileSizeBytes: pdfDoc.FileSizeBytes,
                ProcessingStatus: pdfDoc.ProcessingStatus,
                UploadedAt: pdfDoc.UploadedAt,
                ProcessedAt: pdfDoc.ProcessedAt,
                PageCount: pdfDoc.PageCount
            ));
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException ex)
        {
            RecordUploadMetricSafely("error_io", file?.Length);
            _logger.LogError(ex, "File I/O error during PDF upload for game {GameId}", gameId);
            throw new PdfStorageException("Failed to save PDF file: I/O error occurred.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            RecordUploadMetricSafely("error_access", file?.Length);
            _logger.LogError(ex, "Access denied during PDF upload for game {GameId}", gameId);
            throw new PdfStorageException("Failed to save PDF file: Access denied to storage location.", ex);
        }
        catch (DbUpdateException ex)
        {
            RecordUploadMetricSafely("error_database", file?.Length);
            _logger.LogError(ex, "Database error during PDF upload for game {GameId}", gameId);
            throw new PdfStorageException("Failed to save PDF metadata: Database error occurred.", ex);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            RecordUploadMetricSafely("error_unexpected", file?.Length);
            _logger.LogError(ex, "Unexpected error during PDF upload for game {GameId}", gameId);
            throw new PdfStorageException($"Failed to upload PDF: {ex.Message}", ex);
        }
#pragma warning restore CA1031
    }

    #endregion

    #region Background Processing

    private async Task ProcessPdfAsync(string pdfId, string filePath, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var startTime = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfId }, ct);
            if (pdfDoc == null)
            {
                _logger.LogError("PDF document {PdfId} not found for processing", pdfId);
                return;
            }

            // Step 1: Extract text with page tracking (20-40%)
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Extracting, 0, 0, startTime, null, ct);

            var extractionStopwatch = Stopwatch.StartNew();
            await using var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            var extractResult = await _pdfTextExtractor.ExtractPagedTextAsync(fileStream, enableOcrFallback: true, ct);
            extractionStopwatch.Stop();

            // BGAI-043: Record extraction metrics
            RecordPipelineMetricSafely("extraction", extractionStopwatch.Elapsed.TotalMilliseconds);

            if (!extractResult.Success)
            {
                RecordPipelineMetricSafely("extraction_error", 0);
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, extractResult.ErrorMessage, ct);
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = extractResult.ErrorMessage;
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(ct);
                return;
            }

            // Combine all page chunks into full text
            var fullText = string.Join("\n\n", extractResult.PageChunks
                .Where(pc => !pc.IsEmpty)
                .Select(pc => pc.Text));

            pdfDoc.ExtractedText = fullText;
            pdfDoc.PageCount = extractResult.TotalPages;
            pdfDoc.CharacterCount = extractResult.TotalCharacters;
            await db.SaveChangesAsync(ct);

            // BGAI-043: Record pages processed
            RecordPipelineMetricSafely("pages_processed", extractResult.TotalPages);

            // Extract structured content (tables, diagrams)
            var tableExtractor = scope.ServiceProvider.GetService<IPdfTableExtractor>() ?? _tableExtractor;
            if (tableExtractor != null)
            {
                var structuredResult = await tableExtractor.ExtractStructuredContentAsync(filePath, ct);
                if (structuredResult.Success)
                {
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
                    await db.SaveChangesAsync(ct);
                }
            }

            var totalPages = extractResult.TotalPages;
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Extracting, totalPages, totalPages, startTime, null, ct);

            // Step 2: Chunk text with page tracking (40-60%)
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Chunking, 0, totalPages, startTime, null, ct);
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

            // BGAI-043: Record chunking metrics
            RecordPipelineMetricSafely("chunking", chunkingStopwatch.Elapsed.TotalMilliseconds, allDocumentChunks.Count);

            // Step 3: Generate embeddings (60-80%)
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Embedding, 0, totalPages, startTime, null, ct);
            var embeddingStopwatch = Stopwatch.StartNew();
            var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
            var texts = allDocumentChunks.Select(c => c.Text).ToList();
            var embeddingResult = await embeddingService.GenerateEmbeddingsAsync(texts);
            embeddingStopwatch.Stop();

            // BGAI-043: Record embedding metrics
            RecordPipelineMetricSafely("embedding", embeddingStopwatch.Elapsed.TotalMilliseconds);

            if (!embeddingResult.Success)
            {
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, $"Embedding generation failed: {embeddingResult.ErrorMessage}", ct);
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = embeddingResult.ErrorMessage;
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(ct);
                return;
            }

            await UpdateProgressAsync(db, pdfId, ProcessingStep.Embedding, totalPages, totalPages, startTime, null, ct);

            var embeddings = embeddingResult.Embeddings ?? new List<float[]>();

            if (embeddings.Count != allDocumentChunks.Count)
            {
                var mismatchMessage = $"Embedding service returned {embeddings.Count} vectors for {allDocumentChunks.Count} chunks";
                _logger.LogWarning("Embedding count mismatch for PDF {PdfId}: {Message}", pdfId, mismatchMessage);
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, mismatchMessage, ct);
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = mismatchMessage;
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(ct);
                return;
            }

            var invalidEmbeddingIndexes = new List<int>();
            for (var i = 0; i < embeddings.Count; i++)
            {
                var vector = embeddings[i];
                if (IsInvalidVector(vector))
                {
                    invalidEmbeddingIndexes.Add(i);
                }
            }

            if (invalidEmbeddingIndexes.Count > 0)
            {
                var detail = string.Join(", ", invalidEmbeddingIndexes);
                var error = $"Embedding service returned invalid vectors for chunk indices: {detail}";
                _logger.LogWarning("Invalid embeddings detected for PDF {PdfId}: {Detail}", pdfId, detail);
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, error, ct);
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = error;
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(ct);
                return;
            }

            // Step 4: Index in Qdrant (80-100%)
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Indexing, 0, totalPages, startTime, null, ct);
            var indexingStopwatch = Stopwatch.StartNew();
            var qdrantService = scope.ServiceProvider.GetRequiredService<IQdrantService>();

            var documentChunks = new List<DocumentChunk>();
            for (int i = 0; i < allDocumentChunks.Count; i++)
            {
                documentChunks.Add(new DocumentChunk
                {
                    Text = allDocumentChunks[i].Text,
                    Embedding = embeddings[i],
                    Page = allDocumentChunks[i].Page,
                    CharStart = allDocumentChunks[i].CharStart,
                    CharEnd = allDocumentChunks[i].CharEnd
                });
            }

            var pdfGuid = Guid.Parse(pdfId);
            var indexResult = await qdrantService.IndexDocumentChunksAsync(pdfDoc.GameId.ToString(), pdfId, documentChunks);
            indexingStopwatch.Stop();

            // BGAI-043: Record indexing metrics
            RecordPipelineMetricSafely("indexing", indexingStopwatch.Elapsed.TotalMilliseconds);

            if (!indexResult.Success)
            {
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, $"Qdrant indexing failed: {indexResult.ErrorMessage}", ct);
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = indexResult.ErrorMessage;
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(ct);
                return;
            }

            // Update vector document
            var vectorDoc = await db.VectorDocuments.FirstOrDefaultAsync(v => v.PdfDocumentId == pdfGuid, ct);
            if (vectorDoc == null)
            {
                vectorDoc = new VectorDocumentEntity
                {
                    Id = Guid.NewGuid(),
                    GameId = pdfDoc.GameId,
                    PdfDocumentId = pdfGuid,
                    IndexingStatus = "completed",
                    ChunkCount = indexResult.IndexedCount,
                    TotalCharacters = fullText.Length,
                    IndexedAt = _timeProvider.GetUtcNow().UtcDateTime
                };
                db.VectorDocuments.Add(vectorDoc);
            }
            else
            {
                vectorDoc.IndexingStatus = "completed";
                vectorDoc.ChunkCount = indexResult.IndexedCount;
                vectorDoc.TotalCharacters = fullText.Length;
                vectorDoc.IndexedAt = _timeProvider.GetUtcNow().UtcDateTime;
            }

            // Step 5: Complete (100%)
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Completed, totalPages, totalPages, startTime, null, ct);
            pdfDoc.ProcessingStatus = "completed";
            pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct);

            await InvalidateCacheSafelyAsync(pdfDoc.GameId.ToString(), ct, "PDF processing");

            _logger.LogInformation("PDF processing completed for {PdfId}: {ChunkCount} chunks indexed", pdfId, indexResult.IndexedCount);
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PDF processing cancelled for {PdfId}", pdfId);
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, "Processing cancelled by user", ct);

            var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfId }, CancellationToken.None);
            if (pdfDoc != null)
            {
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = "Processing cancelled by user";
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during PDF processing for {PdfId}", pdfId);
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, $"Invalid operation: {ex.Message}", CancellationToken.None);

            var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfId }, CancellationToken.None);
            if (pdfDoc != null)
            {
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = $"Invalid operation: {ex.Message}";
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during PDF processing for {PdfId}", pdfId);
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, "Database error occurred", CancellationToken.None);

            var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfId }, CancellationToken.None);
            if (pdfDoc != null)
            {
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = "Database error occurred";
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during PDF processing for {PdfId}", pdfId);
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, $"Unexpected error: {ex.Message}", CancellationToken.None);

            var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfId }, CancellationToken.None);
            if (pdfDoc != null)
            {
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = ex.Message;
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }
#pragma warning restore CA1031
    }

    #endregion

    #region Helper Methods

    private async Task UpdateProgressAsync(
        MeepleAiDbContext db,
        string pdfId,
        ProcessingStep step,
        int pagesProcessed,
        int totalPages,
        DateTime startTime,
        string? errorMessage,
        CancellationToken ct)
    {
        try
        {
            var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfId }, ct);
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

            await db.SaveChangesAsync(ct);
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error updating progress for PDF {PdfId}", pdfId);
        }
#pragma warning restore CA1031
    }

    private async Task InvalidateCacheSafelyAsync(string gameId, CancellationToken ct, string operation)
    {
        try
        {
            await _cacheService.InvalidateGameAsync(gameId, ct);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogWarning(ex, "Invalid operation invalidating AI cache for game {GameId} after {Operation}", gameId, operation);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error invalidating AI cache for game {GameId} after {Operation}", gameId, operation);
        }
#pragma warning restore CA1031
    }

    private static bool IsInvalidVector(float[]? vector)
    {
        return vector == null
            || vector.Length == 0
            || Array.Exists(vector, v => float.IsNaN(v) || float.IsInfinity(v));
    }

    /// <summary>
    /// BGAI-043: Records PDF upload metrics in fire-and-forget pattern
    /// </summary>
    private void RecordUploadMetricSafely(string status, long? fileSizeBytes)
    {
        _ = Task.Run(() =>
        {
            try
            {
                MeepleAiMetrics.RecordPdfUploadAttempt(status, fileSizeBytes);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record PDF upload metric for status {Status}", status);
            }
        });
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
                if (step == "pages_processed" && count.HasValue)
                {
                    MeepleAiMetrics.PdfPagesProcessed.Add(count.Value);
                }
                else if (step == "extraction_error")
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

    #endregion
}

// Helper class for document chunk input
internal class DocumentChunkInput
{
    public required string Text { get; init; }
    public int Page { get; init; }
    public int CharStart { get; init; }
    public int CharEnd { get; init; }
}
