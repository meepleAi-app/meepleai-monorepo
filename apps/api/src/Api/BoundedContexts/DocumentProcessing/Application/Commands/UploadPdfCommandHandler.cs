using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
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
using Microsoft.Extensions.Options;
using Npgsql;
using System.Diagnostics;

#pragma warning disable MA0048 // File name must match type name - Contains Handler with related types
namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

public class UploadPdfCommandHandler : ICommandHandler<UploadPdfCommand, PdfUploadResult>
{
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
    private readonly long _maxFileSizeBytes;

    private static readonly HashSet<string> AllowedContentTypes = new(StringComparer.Ordinal) { "application/pdf" };
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
        IOptions<PdfProcessingOptions> pdfOptions,
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
        ArgumentNullException.ThrowIfNull(pdfOptions);
        _maxFileSizeBytes = pdfOptions.Value.MaxFileSizeBytes;
        _timeProvider = timeProvider ?? TimeProvider.System;
    }
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

        if (file.Length > _maxFileSizeBytes)
        {
            var sizeMB = file.Length / 1024.0 / 1024.0;
            var maxMB = _maxFileSizeBytes / 1024 / 1024;
            RecordUploadMetricSafely("validation_failed_size", file.Length);
            return new PdfUploadResult(false, $"File is too large ({sizeMB:F1}MB). Maximum size is {maxMB}MB. Try compressing the PDF or splitting into smaller files.", null);
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            RecordUploadMetricSafely("validation_failed_type", file.Length);
            return new PdfUploadResult(false, $"Invalid file type ({file.ContentType}). Only PDF files are allowed. Please ensure your file has a .pdf extension.", null);
        }

        // Validate PDF file structure (Issue #1688: Prevent corrupted/malformed PDFs)
        // IFormFile.OpenReadStream() creates a new stream each time it's called (ASP.NET Core contract)
        // Using 'using' is safe - blob storage will open a new stream later (line 199)
        using (var validationStream = file.OpenReadStream())
        {
            var (isValid, validationError) = await ValidatePdfStructureAsync(validationStream, file.FileName).ConfigureAwait(false);
            if (!isValid)
            {
                RecordUploadMetricSafely("validation_failed_structure", file.Length);
                return new PdfUploadResult(false, validationError!, null);
            }
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

        UserTier userTier;
        Role userRole;
        PdfUploadQuotaResult quotaResult;
        string? userTierForLog = null;
        try
        {
            // Verify game exists
            var game = await _db.Games
                .Where(g => g.Id.ToString() == gameId)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (game == null)
            {
                return new PdfUploadResult(false, "Game not found. Please select a valid game before uploading.", null);
            }

            // Check upload quota (Admin and Editor bypass quota checks)
            // PERF: Use AsNoTracking + Select for read-only query optimization
            var user = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Id, u.Tier, u.Role })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (user == null)
            {
                RecordUploadMetricSafely("user_not_found", file.Length);
                _logger.LogError("User {UserId} not found during PDF upload", userId);
                return new PdfUploadResult(false, "User not found. Please ensure you are authenticated.", null);
            }

            // Parse string properties to ValueObjects for quota service
            userTier = UserTier.Parse(user.Tier);
            userRole = Role.Parse(user.Role);
            userTierForLog = userTier.Value;

            quotaResult = await _quotaService.CheckQuotaAsync(
                user.Id,
                userTier,
                userRole,
                cancellationToken).ConfigureAwait(false);

            if (!quotaResult.Allowed)
            {
                RecordUploadMetricSafely("quota_exceeded", file.Length);
                _logger.LogWarning(
                    "PDF upload denied for user {UserId} ({Tier}): {Reason}",
                    userId,
                    userTierForLog,
                    quotaResult.ErrorMessage);
                return new PdfUploadResult(false, quotaResult.ErrorMessage!, null);
            }
        }
        catch (Exception ex) when (ex is ObjectDisposedException or Npgsql.NpgsqlException or DbUpdateException or InvalidOperationException)
        {
            RecordUploadMetricSafely("db_unavailable", file.Length);
            _logger.LogError(ex, "Database unavailable during PDF upload for game {GameId}", gameId);
            return new PdfUploadResult(false, "Database unavailable. Please retry the upload.", null);
        }

        _logger.LogDebug(
            "PDF upload quota check passed for user {UserId} ({Tier}): Daily {DailyUsed}/{DailyLimit}, Weekly {WeeklyUsed}/{WeeklyLimit}",
            userId,
            userTierForLog,
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
                storageResult = await _blobStorageService.StoreAsync(stream, fileName, gameId, cancellationToken).ConfigureAwait(false);
            }

            if (!storageResult.Success || string.IsNullOrWhiteSpace(storageResult.FileId))
            {
                RecordUploadMetricSafely("storage_failed", file.Length);
                var error = storageResult.ErrorMessage ?? "Failed to store file";
                return new PdfUploadResult(false, error, null);
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
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

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
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

            // Two-Phase Quota (#1743): Reserve quota (Phase 1)
            var reservationResult = await _quotaService.ReserveQuotaAsync(userId, storageResult.FileId!, cancellationToken).ConfigureAwait(false);

            if (!reservationResult.Reserved)
            {
                try
                {
                    await _blobStorageService.DeleteAsync(storageResult.FileId!, gameId, cancellationToken).ConfigureAwait(false);
                    _db.PdfDocuments.Remove(pdfDoc);
                    await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
                }
                catch (Exception cleanupEx)
                {
                    _logger.LogWarning(cleanupEx, "Failed to cleanup after quota reservation failure for PDF {PdfId}", storageResult.FileId);
                }

                RecordUploadMetricSafely("quota_reservation_failed", file.Length);
                return new PdfUploadResult(false, reservationResult.ErrorMessage!, null);
            }

            _logger.LogInformation(
                "Quota reserved for user {UserId}, PDF {PdfId}, expires at {ExpiresAt}",
                userId, storageResult.FileId, reservationResult.ExpiresAt);

            // Extract text asynchronously (PDF-02) with cancellation support (PDF-08)
            _backgroundTaskService.ExecuteWithCancellation(storageResult.FileId!, (ct) => ProcessPdfAsync(storageResult.FileId!, storageResult.FilePath!, userId, ct));

            await InvalidateCacheSafelyAsync(gameId, cancellationToken, "PDF upload").ConfigureAwait(false);

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

    /// <summary>
    /// Validates PDF file structure by checking for required PDF headers and trailers.
    /// Prevents upload of corrupted or malformed files that would fail during processing.
    /// </summary>
    /// <param name="stream">The file stream to validate</param>
    /// <param name="fileName">The file name for logging</param>
    /// <returns>Tuple of (isValid, errorMessage)</returns>
    private static async Task<(bool IsValid, string? ErrorMessage)> ValidatePdfStructureAsync(Stream stream, string _)
    {
        const int headerCheckBytes = 1024; // Read first 1KB to find PDF header
        const int trailerCheckBytes = 1024; // Read last 1KB to find PDF trailer

        try
        {
            // Check minimum file size (PDF must have at least header + trailer)
            if (stream.Length < 50)
            {
                return (false, "Invalid PDF file: File is too small to be a valid PDF (minimum 50 bytes required).");
            }

            // Read beginning of file for PDF header
            stream.Seek(0, SeekOrigin.Begin);
            var headerBuffer = new byte[Math.Min(headerCheckBytes, (int)stream.Length)];
            var headerBytesRead = await stream.ReadAsync(headerBuffer.AsMemory(0, headerBuffer.Length)).ConfigureAwait(false);

            // Check for PDF header signature (%PDF-1.x)
            var headerText = System.Text.Encoding.ASCII.GetString(headerBuffer, 0, Math.Min(10, headerBytesRead));
            if (!headerText.StartsWith("%PDF-", StringComparison.Ordinal))
            {
                return (false, $"Invalid PDF file: Missing PDF header signature. File appears to be corrupted or not a valid PDF.");
            }

            // Read end of file for PDF trailer
            var trailerStart = Math.Max(0, stream.Length - trailerCheckBytes);
            stream.Seek(trailerStart, SeekOrigin.Begin);
            var trailerBuffer = new byte[Math.Min(trailerCheckBytes, (int)(stream.Length - trailerStart))];
            var trailerBytesRead = await stream.ReadAsync(trailerBuffer.AsMemory(0, trailerBuffer.Length)).ConfigureAwait(false);

            // Check for PDF EOF marker (%%EOF)
            var trailerText = System.Text.Encoding.ASCII.GetString(trailerBuffer, 0, trailerBytesRead);
            if (!trailerText.Contains("%%EOF", StringComparison.Ordinal))
            {
                return (false, $"Invalid PDF file: Missing PDF end-of-file marker (%%EOF). File appears to be incomplete or malformed.");
            }

            // Reset stream position for subsequent operations
            stream.Seek(0, SeekOrigin.Begin);

            return (true, null);
        }
        catch (Exception ex)
        {
            // Reset stream position even on error
            try { stream.Seek(0, SeekOrigin.Begin); } catch { /* Ignore seek errors */ }
            return (false, $"Failed to validate PDF structure: {ex.Message}");
        }
    }
    private async Task ProcessPdfAsync(string pdfId, string filePath, Guid userId, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var quotaService = scope.ServiceProvider.GetRequiredService<IPdfUploadQuotaService>();
        var startTime = _timeProvider.GetUtcNow().UtcDateTime;

        try
        {
            if (!Guid.TryParse(pdfId, out var pdfGuid))
            {
                _logger.LogError("Invalid PDF ID format {PdfId}", pdfId);
                await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfGuid }, ct).ConfigureAwait(false);
            if (pdfDoc == null)
            {
                _logger.LogError("PDF document {PdfId} not found for processing", pdfId);
                await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            // IDEMPOTENCY CHECK (#1742): Skip if already processing/processed
            if (!string.Equals(pdfDoc.ProcessingStatus, "pending", StringComparison.Ordinal))
            {
                _logger.LogInformation(
                    "PDF {PdfId} already processed (status: {Status}), skipping duplicate background task",
                    pdfId, pdfDoc.ProcessingStatus);

                if (string.Equals(pdfDoc.ProcessingStatus, "failed", StringComparison.Ordinal))
                {
                    await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);
                }

                return;
            }

            // Mark as processing (optimistic locking)
            pdfDoc.ProcessingStatus = "processing";
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            // Step 1: Extract text with page tracking (20-40%)
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Extracting, 0, 0, startTime, null, ct).ConfigureAwait(false);

            var extractionStopwatch = Stopwatch.StartNew();
            var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
            await using (fileStream.ConfigureAwait(false))
            {
                var extractResult = await _pdfTextExtractor.ExtractPagedTextAsync(fileStream, enableOcrFallback: true, ct).ConfigureAwait(false);
                extractionStopwatch.Stop();

                // BGAI-043: Record extraction metrics
                RecordPipelineMetricSafely("extraction", extractionStopwatch.Elapsed.TotalMilliseconds);

                if (!extractResult.Success)
                {
                    RecordPipelineMetricSafely("extraction_error", 0);
                    await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, extractResult.ErrorMessage, ct).ConfigureAwait(false);

                    // Two-Phase Quota (#1743): Release quota on extraction failure
                    await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);

                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = extractResult.ErrorMessage;
                    pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    return;
                }

                // Combine all page chunks into full text
                var fullText = string.Join("\n\n", extractResult.PageChunks
                    .Where(pc => !pc.IsEmpty)
                    .Select(pc => pc.Text));

                pdfDoc.ExtractedText = fullText;
                pdfDoc.PageCount = extractResult.TotalPages;
                pdfDoc.CharacterCount = extractResult.TotalCharacters;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                // BGAI-043: Record pages processed
                RecordPipelineMetricSafely("pages_processed", extractResult.TotalPages);

                // Extract structured content (tables, diagrams)
                var tableExtractor = scope.ServiceProvider.GetService<IPdfTableExtractor>() ?? _tableExtractor;
                if (tableExtractor != null)
                {
                    var structuredResult = await tableExtractor.ExtractStructuredContentAsync(filePath, ct).ConfigureAwait(false);
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
                        await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    }
                }

                var totalPages = extractResult.TotalPages;
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Extracting, totalPages, totalPages, startTime, null, ct).ConfigureAwait(false);

                // Step 2: Chunk text with page tracking (40-60%)
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Chunking, 0, totalPages, startTime, null, ct).ConfigureAwait(false);
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
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Embedding, 0, totalPages, startTime, null, ct).ConfigureAwait(false);
                var embeddingStopwatch = Stopwatch.StartNew();
                var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
                var texts = allDocumentChunks.Select(c => c.Text).ToList();
                var embeddingResult = await embeddingService.GenerateEmbeddingsAsync(texts).ConfigureAwait(false);
                embeddingStopwatch.Stop();

                // BGAI-043: Record embedding metrics
                RecordPipelineMetricSafely("embedding", embeddingStopwatch.Elapsed.TotalMilliseconds);

                if (!embeddingResult.Success)
                {
                    await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, $"Embedding generation failed: {embeddingResult.ErrorMessage}", ct).ConfigureAwait(false);

                    // Two-Phase Quota (#1743): Release quota on embedding failure
                    await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);

                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = embeddingResult.ErrorMessage;
                    pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    return;
                }

                await UpdateProgressAsync(db, pdfId, ProcessingStep.Embedding, totalPages, totalPages, startTime, null, ct).ConfigureAwait(false);

                var embeddings = embeddingResult.Embeddings ?? new List<float[]>();

                if (embeddings.Count != allDocumentChunks.Count)
                {
                    var mismatchMessage = $"Embedding service returned {embeddings.Count} vectors for {allDocumentChunks.Count} chunks";
                    _logger.LogWarning("Embedding count mismatch for PDF {PdfId}: {Message}", pdfId, mismatchMessage);
                    await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, mismatchMessage, ct).ConfigureAwait(false);

                    // Two-Phase Quota (#1743): Release quota on embedding mismatch
                    await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);

                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = mismatchMessage;
                    pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
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
                    await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, error, ct).ConfigureAwait(false);

                    // Two-Phase Quota (#1743): Release quota on invalid embeddings
                    await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);

                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = error;
                    pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    return;
                }

                // Step 4: Index in Qdrant (80-100%)
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Indexing, 0, totalPages, startTime, null, ct).ConfigureAwait(false);
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

                // pdfGuid is already parsed at the start of this method
                var indexResult = await qdrantService.IndexDocumentChunksAsync(pdfDoc.GameId.ToString(), pdfId, documentChunks).ConfigureAwait(false);
                indexingStopwatch.Stop();

                // BGAI-043: Record indexing metrics
                RecordPipelineMetricSafely("indexing", indexingStopwatch.Elapsed.TotalMilliseconds);

                if (!indexResult.Success)
                {
                    await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, $"Qdrant indexing failed: {indexResult.ErrorMessage}", ct).ConfigureAwait(false);

                    // Two-Phase Quota (#1743): Release quota on indexing failure
                    await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);

                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = indexResult.ErrorMessage;
                    pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await db.SaveChangesAsync(ct).ConfigureAwait(false);
                    return;
                }

                // Update vector document
                var vectorDoc = await db.VectorDocuments.FirstOrDefaultAsync(v => v.PdfDocumentId == pdfGuid, ct).ConfigureAwait(false);
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

                // Step 4b: Save text chunks to PostgreSQL for hybrid search (FTS)
                // Delete existing chunks for re-processing scenario
                var existingChunks = await db.TextChunks
                    .Where(tc => tc.PdfDocumentId == pdfGuid)
                    .ToListAsync(ct).ConfigureAwait(false);
                if (existingChunks.Count > 0)
                {
                    db.TextChunks.RemoveRange(existingChunks);
                }

                // Create TextChunkEntity for each document chunk (for FTS)
                var textChunkEntities = new List<TextChunkEntity>();
                for (int i = 0; i < allDocumentChunks.Count; i++)
                {
                    textChunkEntities.Add(new TextChunkEntity
                    {
                        Id = Guid.NewGuid(),
                        GameId = pdfDoc.GameId,
                        PdfDocumentId = pdfGuid,
                        Content = allDocumentChunks[i].Text,
                        ChunkIndex = i,
                        PageNumber = allDocumentChunks[i].Page,
                        CharacterCount = allDocumentChunks[i].Text.Length,
                        CreatedAt = _timeProvider.GetUtcNow().UtcDateTime
                    });
                }
                db.TextChunks.AddRange(textChunkEntities);
                _logger.LogInformation("Saved {ChunkCount} text chunks to PostgreSQL for hybrid search (PDF {PdfId})",
                    textChunkEntities.Count, pdfId);

                // Step 5: Complete (100%)
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Completed, totalPages, totalPages, startTime, null, ct).ConfigureAwait(false);
                pdfDoc.ProcessingStatus = "completed";
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);

                await InvalidateCacheSafelyAsync(pdfDoc.GameId.ToString(), ct, "PDF processing").ConfigureAwait(false);

                // Two-Phase Quota (#1743): Confirm quota (Phase 2)
                await quotaService.ConfirmQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);

                _logger.LogInformation("PDF processing completed for {PdfId}: {ChunkCount} chunks indexed", pdfId, indexResult.IndexedCount);
            }
        }
        catch (OperationCanceledException)
        {
            _logger.LogInformation("PDF processing cancelled for {PdfId}", pdfId);
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, "Processing cancelled by user", ct).ConfigureAwait(false);

            // Two-Phase Quota (#1743): Release quota on cancellation
            await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);

            if (Guid.TryParse(pdfId, out var cancelledPdfGuid))
            {
                var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { cancelledPdfGuid }, CancellationToken.None).ConfigureAwait(false);
                if (pdfDoc != null)
                {
                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = "Processing cancelled by user";
                    pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during PDF processing for {PdfId}", pdfId);
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, $"Invalid operation: {ex.Message}", CancellationToken.None).ConfigureAwait(false);

            // Two-Phase Quota (#1743): Release quota on invalid operation
            await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);

            if (Guid.TryParse(pdfId, out var invalidOpPdfGuid))
            {
                var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { invalidOpPdfGuid }, CancellationToken.None).ConfigureAwait(false);
                if (pdfDoc != null)
                {
                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = $"Invalid operation: {ex.Message}";
                    pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during PDF processing for {PdfId}", pdfId);
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, "Database error occurred", CancellationToken.None).ConfigureAwait(false);

            // Two-Phase Quota (#1743): Release quota on database error
            await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);

            if (Guid.TryParse(pdfId, out var dbErrorPdfGuid))
            {
                var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { dbErrorPdfGuid }, CancellationToken.None).ConfigureAwait(false);
                if (pdfDoc != null)
                {
                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = "Database error occurred";
                    pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during PDF processing for {PdfId}", pdfId);
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, $"Unexpected error: {ex.Message}", CancellationToken.None).ConfigureAwait(false);

            // Two-Phase Quota (#1743): Release quota on unexpected error
            await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);

            if (Guid.TryParse(pdfId, out var unexpectedErrorPdfGuid))
            {
                var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { unexpectedErrorPdfGuid }, CancellationToken.None).ConfigureAwait(false);
                if (pdfDoc != null)
                {
                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = ex.Message;
                    pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                    await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
                }
            }
        }
#pragma warning restore CA1031
    }
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
            if (!Guid.TryParse(pdfId, out var pdfGuid))
            {
                _logger.LogWarning("Invalid PDF ID format for progress update: {PdfId}", pdfId);
                return;
            }

            var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfGuid }, ct).ConfigureAwait(false);
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

            await db.SaveChangesAsync(ct).ConfigureAwait(false);
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
            await _cacheService.InvalidateGameAsync(gameId, ct).ConfigureAwait(false);
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

// Helper class for document chunk input
internal class DocumentChunkInput
{
    public required string Text { get; init; }
    public int Page { get; init; }
    public int CharStart { get; init; }
    public int CharEnd { get; init; }
}
