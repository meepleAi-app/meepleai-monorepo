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

        // Validate file input
        var validationResult = await ValidateFileInputAsync(file, gameId, cancellationToken).ConfigureAwait(false);
        if (!validationResult.IsValid)
        {
            return new PdfUploadResult(false, validationResult.ErrorMessage!, null);
        }

        var fileName = validationResult.SanitizedFileName!;

        // Check user quota and permissions
        var (quotaAllowed, quotaError, userTier) = await CheckUserQuotaAsync(userId, gameId, file, cancellationToken).ConfigureAwait(false);
        if (!quotaAllowed)
        {
            return new PdfUploadResult(false, quotaError!, null);
        }

        try
        {
            // Store file and create database record
            var (storageSuccess, storageResult, pdfDoc) = await StoreFileAndCreateRecordAsync(
                file, fileName, gameId, userId, cancellationToken).ConfigureAwait(false);

            if (!storageSuccess)
            {
                return new PdfUploadResult(false, storageResult.ErrorMessage ?? "Failed to store file", null);
            }

            // Reserve quota and start background processing
            return await ReserveQuotaAndStartProcessingAsync(
                userId, gameId, file, storageResult, pdfDoc!, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (PdfStorageException)
        {
            throw; // Re-throw storage exceptions as-is
        }
    }

    /// <summary>
    /// Validates PDF file structure by checking for required PDF headers and trailers.
    /// Prevents upload of corrupted or malformed files that would fail during processing.
    /// </summary>
    /// <param name="stream">The file stream to validate</param>
    /// <param name="fileName">The file name for logging</param>
    /// <returns>Tuple of (isValid, errorMessage)</returns>

    /// <summary>
    /// Validates file input (size, type, structure, filename).
    /// Returns validation result with sanitized filename.
    /// </summary>
    private async Task<(bool IsValid, string? ErrorMessage, string? SanitizedFileName)> ValidateFileInputAsync(
        IFormFile? file,
        string gameId,
        CancellationToken cancellationToken)
    {
        if (file == null || file.Length == 0)
        {
            RecordUploadMetricSafely("validation_failed_empty", null);
            return (false, "No file provided. Please select a PDF file to upload.", null);
        }

        if (file.Length > _maxFileSizeBytes)
        {
            var sizeMB = file.Length / 1024.0 / 1024.0;
            var maxMB = _maxFileSizeBytes / 1024 / 1024;
            RecordUploadMetricSafely("validation_failed_size", file.Length);
            return (false, $"File is too large ({sizeMB:F1}MB). Maximum size is {maxMB}MB. Try compressing the PDF or splitting into smaller files.", null);
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            RecordUploadMetricSafely("validation_failed_type", file.Length);
            return (false, $"Invalid file type ({file.ContentType}). Only PDF files are allowed. Please ensure your file has a .pdf extension.", null);
        }

        // Validate PDF file structure (Issue #1688)
        using (var validationStream = file.OpenReadStream())
        {
            var (isValid, validationError) = await ValidatePdfStructureAsync(validationStream, file.FileName).ConfigureAwait(false);
            if (!isValid)
            {
                RecordUploadMetricSafely("validation_failed_structure", file.Length);
                return (false, validationError!, null);
            }
        }

        // SEC-738: Sanitize filename
        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return (false, "Invalid file name. The file must have a valid name.", null);
        }

        try
        {
            fileName = PathSecurity.SanitizeFilename(fileName);
        }
        catch (ArgumentException ex)
        {
            return (false, $"Invalid file name: {ex.Message}", null);
        }

        return (true, null, fileName);
    }

    /// <summary>
    /// Checks user quota and permissions for PDF upload.
    /// Returns (allowed, errorMessage, userTier).
    /// </summary>
    private async Task<(bool Allowed, string? ErrorMessage, string? UserTier)> CheckUserQuotaAsync(
        Guid userId,
        string gameId,
        IFormFile file,
        CancellationToken cancellationToken)
    {
        try
        {
            // Verify game exists
            var game = await _db.Games
                .Where(g => g.Id.ToString() == gameId)
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (game == null)
            {
                return (false, "Game not found. Please select a valid game before uploading.", null);
            }

            // Check upload quota
            var user = await _db.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.Id, u.Tier, u.Role })
                .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

            if (user == null)
            {
                RecordUploadMetricSafely("user_not_found", file.Length);
                _logger.LogError("User {UserId} not found during PDF upload", userId);
                return (false, "User not found. Please ensure you are authenticated.", null);
            }

            var userTier = UserTier.Parse(user.Tier);
            var userRole = Role.Parse(user.Role);

            var quotaResult = await _quotaService.CheckQuotaAsync(
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
                    userTier.Value,
                    quotaResult.ErrorMessage);
                return (false, quotaResult.ErrorMessage!, userTier.Value);
            }

            _logger.LogDebug(
                "PDF upload quota check passed for user {UserId} ({Tier}): Daily {DailyUsed}/{DailyLimit}, Weekly {WeeklyUsed}/{WeeklyLimit}",
                userId,
                userTier.Value,
                quotaResult.DailyUploadsUsed,
                quotaResult.DailyLimit,
                quotaResult.WeeklyUploadsUsed,
                quotaResult.WeeklyLimit);

            return (true, null, userTier.Value);
        }
        catch (OperationCanceledException)
        {
            throw; // Propagate cancellation
        }
        catch (Exception ex) when (ex is ObjectDisposedException or Npgsql.NpgsqlException or DbUpdateException or InvalidOperationException)
        {
            RecordUploadMetricSafely("db_unavailable", file.Length);
            _logger.LogError(ex, "Database unavailable during PDF upload for game {GameId}", gameId);
            return (false, "Database unavailable. Please retry the upload.", null);
        }
    }

    /// <summary>
    /// Stores file in blob storage and creates database record.
    /// Returns (success, storageResult, pdfDoc).
    /// </summary>
    private async Task<(bool Success, BlobStorageResult Result, PdfDocumentEntity? PdfDoc)> StoreFileAndCreateRecordAsync(
        IFormFile file,
        string fileName,
        string gameId,
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Store file in blob storage
            BlobStorageResult storageResult;
            using (var stream = file.OpenReadStream())
            {
                storageResult = await _blobStorageService.StoreAsync(stream, fileName, gameId, cancellationToken).ConfigureAwait(false);
            }

            if (!storageResult.Success || string.IsNullOrWhiteSpace(storageResult.FileId))
            {
                RecordUploadMetricSafely("storage_failed", file.Length);
                return (false, storageResult, null);
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

            // Initialize progress tracking
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

            return (true, storageResult, pdfDoc);
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
#pragma warning disable CA1031
        catch (Exception ex)
        {
            RecordUploadMetricSafely("error_unexpected", file?.Length);
            _logger.LogError(ex, "Unexpected error during PDF upload for game {GameId}", gameId);
            throw new PdfStorageException($"Failed to upload PDF: {ex.Message}", ex);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Reserves quota and starts background PDF processing.
    /// </summary>
    private async Task<PdfUploadResult> ReserveQuotaAndStartProcessingAsync(
        Guid userId,
        string gameId,
        IFormFile file,
        BlobStorageResult storageResult,
        PdfDocumentEntity pdfDoc,
        CancellationToken cancellationToken)
    {
        var reservationResult = await _quotaService.ReserveQuotaAsync(userId, storageResult.FileId!, cancellationToken).ConfigureAwait(false);

        if (!reservationResult.Reserved)
        {
            await CleanupAfterQuotaFailureAsync(storageResult.FileId!, gameId, pdfDoc, cancellationToken).ConfigureAwait(false);
            RecordUploadMetricSafely("quota_reservation_failed", file.Length);
            return new PdfUploadResult(false, reservationResult.ErrorMessage!, null);
        }

        _logger.LogInformation(
            "Quota reserved for user {UserId}, PDF {PdfId}, expires at {ExpiresAt}",
            userId, storageResult.FileId, reservationResult.ExpiresAt);

        // Start background processing
        _backgroundTaskService.ExecuteWithCancellation(
            storageResult.FileId!,
            (ct) => ProcessPdfAsync(storageResult.FileId!, storageResult.FilePath!, userId, ct));

        await InvalidateCacheSafelyAsync(gameId, cancellationToken, "PDF upload").ConfigureAwait(false);

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

    /// <summary>
    /// Cleans up blob storage and database record after quota reservation failure.
    /// </summary>
    private async Task CleanupAfterQuotaFailureAsync(
        string fileId,
        string gameId,
        PdfDocumentEntity pdfDoc,
        CancellationToken cancellationToken)
    {
        try
        {
            await _blobStorageService.DeleteAsync(fileId, gameId, cancellationToken).ConfigureAwait(false);
            _db.PdfDocuments.Remove(pdfDoc);
            await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        }
        catch (Exception cleanupEx)
        {
            _logger.LogWarning(cleanupEx, "Failed to cleanup after quota reservation failure for PDF {PdfId}", fileId);
        }
    }

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
            var pdfDoc = await ValidateAndPrepareProcessingAsync(pdfId, userId, db, quotaService, ct).ConfigureAwait(false);
            if (pdfDoc == null) return; // Validation failed

            // Step 1: Extract text with page tracking (20-40%)
            var (extractionSuccess, fullText, extractResult) = await ExtractPdfContentAsync(
                pdfId, filePath, pdfDoc, db, scope, startTime, ct).ConfigureAwait(false);
            
            if (!extractionSuccess)
            {
                await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);
                return;
            }

            // Step 2: Chunk text with page tracking (40-60%)
            var allDocumentChunks = await ChunkExtractedTextAsync(
                pdfId, fullText!, extractResult!, db, scope, startTime, ct).ConfigureAwait(false);

            // Step 3: Generate embeddings (60-80%)
            var (embeddingsSuccess, embeddings) = await GenerateAndValidateEmbeddingsAsync(
                pdfId, userId, allDocumentChunks, pdfDoc, db, quotaService, scope, startTime, ct).ConfigureAwait(false);
            
            if (!embeddingsSuccess) return;

            // Step 4: Index in Qdrant (80-100%)
            await IndexInVectorStoreAsync(
                pdfId, userId, pdfDoc, allDocumentChunks, embeddings!,
                db, scope, startTime, ct).ConfigureAwait(false);

            // Complete processing
            await FinalizeProcessingAsync(pdfId, pdfDoc, userId, db, quotaService, startTime, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            await HandleProcessingCancellationAsync(pdfId, userId, db, quotaService, startTime, ct).ConfigureAwait(false);
        }
        catch (InvalidOperationException ex)
        {
            await HandleProcessingErrorAsync(pdfId, userId, db, quotaService, startTime, ex, "Invalid operation", ct).ConfigureAwait(false);
        }
        catch (DbUpdateException ex)
        {
            await HandleProcessingErrorAsync(pdfId, userId, db, quotaService, startTime, ex, "Database error occurred", ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            await HandleProcessingErrorAsync(pdfId, userId, db, quotaService, startTime, ex, ex.Message, ct).ConfigureAwait(false);
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Validates PDF ID and prepares document for processing with idempotency check.
    /// </summary>
    private async Task<PdfDocumentEntity?> ValidateAndPrepareProcessingAsync(
        string pdfId,
        Guid userId,
        MeepleAiDbContext db,
        IPdfUploadQuotaService quotaService,
        CancellationToken ct)
    {
        if (!Guid.TryParse(pdfId, out var pdfGuid))
        {
            _logger.LogError("Invalid PDF ID format {PdfId}", pdfId);
            await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);
            return null;
        }

        var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfGuid }, ct).ConfigureAwait(false);
        if (pdfDoc == null)
        {
            _logger.LogError("PDF document {PdfId} not found for processing", pdfId);
            await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);
            return null;
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

            return null;
        }

        // Mark as processing (optimistic locking)
        pdfDoc.ProcessingStatus = "processing";
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

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
        CancellationToken ct)
    {
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Extracting, 0, 0, startTime, null, ct).ConfigureAwait(false);

        var extractionStopwatch = Stopwatch.StartNew();
        var fileStream = new FileStream(filePath, FileMode.Open, FileAccess.Read, FileShare.Read);
        await using (fileStream.ConfigureAwait(false))
        {
            var extractResult = await _pdfTextExtractor.ExtractPagedTextAsync(fileStream, enableOcrFallback: true, ct).ConfigureAwait(false);
            extractionStopwatch.Stop();

            RecordPipelineMetricSafely("extraction", extractionStopwatch.Elapsed.TotalMilliseconds);

            if (!extractResult.Success)
            {
                RecordPipelineMetricSafely("extraction_error", 0);
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, extractResult.ErrorMessage, ct).ConfigureAwait(false);
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = extractResult.ErrorMessage;
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(ct).ConfigureAwait(false);
                return (false, null, null);
            }

            var fullText = string.Join("\n\n", extractResult.PageChunks
                .Where(pc => !pc.IsEmpty)
                .Select(pc => pc.Text));

            pdfDoc.ExtractedText = fullText;
            pdfDoc.PageCount = extractResult.TotalPages;
            pdfDoc.CharacterCount = extractResult.TotalCharacters;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);

            RecordPipelineMetricSafely("pages_processed", extractResult.TotalPages);

            // Extract structured content (tables, diagrams)
            await ExtractStructuredContentAsync(pdfId, filePath, pdfDoc, db, scope, ct).ConfigureAwait(false);

            await UpdateProgressAsync(db, pdfId, ProcessingStep.Extracting, extractResult.TotalPages, extractResult.TotalPages, startTime, null, ct).ConfigureAwait(false);

            return (true, fullText, extractResult);
        }
    }

    /// <summary>
    /// Extracts structured content (tables, diagrams, atomic rules) from PDF.
    /// </summary>
    private async Task ExtractStructuredContentAsync(
        string pdfId,
        string filePath,
        PdfDocumentEntity pdfDoc,
        MeepleAiDbContext db,
        IServiceScope scope,
        CancellationToken ct)
    {
        var tableExtractor = scope.ServiceProvider.GetService<IPdfTableExtractor>() ?? _tableExtractor;
        if (tableExtractor == null) return;

        var structuredResult = await tableExtractor.ExtractStructuredContentAsync(filePath, ct).ConfigureAwait(false);
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
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
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
        CancellationToken ct)
    {
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Chunking, 0, extractResult.TotalPages, startTime, null, ct).ConfigureAwait(false);
        
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
    /// Generates and validates embeddings for document chunks.
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
        CancellationToken ct)
    {
        var totalPages = pdfDoc.PageCount ?? 0;
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Embedding, 0, totalPages, startTime, null, ct).ConfigureAwait(false);
        
        // Generate embeddings
        var embeddingStopwatch = Stopwatch.StartNew();
        var embeddingService = scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
        var texts = allDocumentChunks.Select(c => c.Text).ToList();
        var embeddingResult = await embeddingService.GenerateEmbeddingsAsync(texts).ConfigureAwait(false);
        embeddingStopwatch.Stop();

        RecordPipelineMetricSafely("embedding", embeddingStopwatch.Elapsed.TotalMilliseconds);

        if (!embeddingResult.Success)
        {
            await HandleEmbeddingFailureAsync(pdfId, userId, pdfDoc, db, quotaService, startTime, 
                $"Embedding generation failed: {embeddingResult.ErrorMessage}", ct).ConfigureAwait(false);
            return (false, null);
        }

        await UpdateProgressAsync(db, pdfId, ProcessingStep.Embedding, totalPages, totalPages, startTime, null, ct).ConfigureAwait(false);

        var embeddings = embeddingResult.Embeddings ?? new List<float[]>();

        // Validate embedding count matches chunks
        var countValidation = await ValidateEmbeddingCountAsync(
            pdfId, userId, embeddings.Count, allDocumentChunks.Count, pdfDoc, db, quotaService, startTime, ct).ConfigureAwait(false);
        if (!countValidation) return (false, null);

        // Validate embedding quality (no NaN/Infinity vectors)
        var qualityValidation = await ValidateEmbeddingQualityAsync(
            pdfId, userId, embeddings, pdfDoc, db, quotaService, startTime, ct).ConfigureAwait(false);
        if (!qualityValidation) return (false, null);

        return (true, embeddings);
    }

    /// <summary>
    /// Validates that embedding count matches chunk count.
    /// </summary>
    private async Task<bool> ValidateEmbeddingCountAsync(
        string pdfId,
        Guid userId,
        int embeddingCount,
        int chunkCount,
        PdfDocumentEntity pdfDoc,
        MeepleAiDbContext db,
        IPdfUploadQuotaService quotaService,
        DateTime startTime,
        CancellationToken ct)
    {
        if (embeddingCount == chunkCount) return true;

        var mismatchMessage = $"Embedding service returned {embeddingCount} vectors for {chunkCount} chunks";
        _logger.LogWarning("Embedding count mismatch for PDF {PdfId}: {Message}", pdfId, mismatchMessage);
        await HandleEmbeddingFailureAsync(pdfId, userId, pdfDoc, db, quotaService, startTime, mismatchMessage, ct).ConfigureAwait(false);
        return false;
    }

    /// <summary>
    /// Validates embedding quality by checking for NaN/Infinity values.
    /// </summary>
    private async Task<bool> ValidateEmbeddingQualityAsync(
        string pdfId,
        Guid userId,
        List<float[]> embeddings,
        PdfDocumentEntity pdfDoc,
        MeepleAiDbContext db,
        IPdfUploadQuotaService quotaService,
        DateTime startTime,
        CancellationToken ct)
    {
        var invalidEmbeddingIndexes = new List<int>();
        for (var i = 0; i < embeddings.Count; i++)
        {
            if (IsInvalidVector(embeddings[i]))
            {
                invalidEmbeddingIndexes.Add(i);
            }
        }

        if (invalidEmbeddingIndexes.Count == 0) return true;

        var detail = string.Join(", ", invalidEmbeddingIndexes);
        var error = $"Embedding service returned invalid vectors for chunk indices: {detail}";
        _logger.LogWarning("Invalid embeddings detected for PDF {PdfId}: {Detail}", pdfId, detail);
        await HandleEmbeddingFailureAsync(pdfId, userId, pdfDoc, db, quotaService, startTime, error, ct).ConfigureAwait(false);
        return false;
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
        CancellationToken ct)
    {
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, errorMessage, ct).ConfigureAwait(false);
        await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);
        pdfDoc.ProcessingStatus = "failed";
        pdfDoc.ProcessingError = errorMessage;
        pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);
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
        CancellationToken ct)
    {
        var totalPages = pdfDoc.PageCount ?? 0;

        await UpdateProgressAsync(db, pdfId, ProcessingStep.Indexing, 0, totalPages, startTime, null, ct).ConfigureAwait(false);

        var indexingStopwatch = Stopwatch.StartNew();
        var qdrantService = scope.ServiceProvider.GetRequiredService<IQdrantService>();

        var documentChunks = allDocumentChunks
            .Select((chunk, index) => new DocumentChunk
            {
                Text = chunk.Text,
                Embedding = embeddings[index],
                Page = chunk.Page,
                CharStart = chunk.CharStart,
                CharEnd = chunk.CharEnd
            })
            .ToList();

        var indexResult = await qdrantService.IndexDocumentChunksAsync(pdfDoc.GameId.ToString(), pdfId, documentChunks).ConfigureAwait(false);
        indexingStopwatch.Stop();

        RecordPipelineMetricSafely("indexing", indexingStopwatch.Elapsed.TotalMilliseconds);

        if (!indexResult.Success)
        {
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, $"Qdrant indexing failed: {indexResult.ErrorMessage}", ct).ConfigureAwait(false);
            await scope.ServiceProvider.GetRequiredService<IPdfUploadQuotaService>().ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);
            pdfDoc.ProcessingStatus = "failed";
            pdfDoc.ProcessingError = indexResult.ErrorMessage;
            pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
            await db.SaveChangesAsync(ct).ConfigureAwait(false);
            return;
        }

        // Update vector document
        await UpdateVectorDocumentAsync(pdfId, pdfDoc, indexResult.IndexedCount, db, ct).ConfigureAwait(false);

        // Save text chunks to PostgreSQL for hybrid search (FTS)
        await SaveTextChunksForHybridSearchAsync(pdfId, pdfDoc, allDocumentChunks, db, ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Updates or creates VectorDocument record after successful indexing.
    /// </summary>
    private async Task UpdateVectorDocumentAsync(
        string pdfId,
        PdfDocumentEntity pdfDoc,
        int indexedCount,
        MeepleAiDbContext db,
        CancellationToken ct)
    {
        var pdfGuid = Guid.Parse(pdfId);
        var vectorDoc = await db.VectorDocuments.FirstOrDefaultAsync(v => v.PdfDocumentId == pdfGuid, ct).ConfigureAwait(false);
        
        if (vectorDoc == null)
        {
            vectorDoc = new VectorDocumentEntity
            {
                Id = Guid.NewGuid(),
                GameId = pdfDoc.GameId,
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

        await db.SaveChangesAsync(ct).ConfigureAwait(false);
    }

    /// <summary>
    /// Saves text chunks to PostgreSQL for hybrid search with FTS.
    /// </summary>
    private async Task SaveTextChunksForHybridSearchAsync(
        string pdfId,
        PdfDocumentEntity pdfDoc,
        List<DocumentChunkInput> allDocumentChunks,
        MeepleAiDbContext db,
        CancellationToken ct)
    {
        var pdfGuid = Guid.Parse(pdfId);

        // Delete existing chunks for re-processing scenario
        var existingChunks = await db.TextChunks
            .Where(tc => tc.PdfDocumentId == pdfGuid)
            .ToListAsync(ct).ConfigureAwait(false);
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
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

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
        CancellationToken ct)
    {
        var totalPages = pdfDoc.PageCount ?? 0;
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Completed, totalPages, totalPages, startTime, null, ct).ConfigureAwait(false);
        
        pdfDoc.ProcessingStatus = "completed";
        pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
        await db.SaveChangesAsync(ct).ConfigureAwait(false);

        await InvalidateCacheSafelyAsync(pdfDoc.GameId.ToString(), ct, "PDF processing").ConfigureAwait(false);

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
        CancellationToken ct)
    {
        _logger.LogInformation("PDF processing cancelled for {PdfId}", pdfId);
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, "Processing cancelled by user", ct).ConfigureAwait(false);
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
        CancellationToken ct = default)
    {
        _logger.LogError(ex, "Error during PDF processing for {PdfId}: {ErrorType}", pdfId, ex.GetType().Name);
        await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, errorMessage, CancellationToken.None).ConfigureAwait(false);
        await quotaService.ReleaseQuotaAsync(userId, pdfId, CancellationToken.None).ConfigureAwait(false);

        if (Guid.TryParse(pdfId, out var errorPdfGuid))
        {
            var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { errorPdfGuid }, CancellationToken.None).ConfigureAwait(false);
            if (pdfDoc != null)
            {
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = errorMessage;
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
                await db.SaveChangesAsync(CancellationToken.None).ConfigureAwait(false);
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