using System.Linq;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Api.Services.Exceptions;

namespace Api.Services;

public class PdfStorageService
{
    private readonly MeepleAiDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PdfStorageService> _logger;
    private readonly PdfTextExtractionService _textExtractionService;
    private readonly PdfTableExtractionService _tableExtractionService;
    private readonly IBackgroundTaskService _backgroundTaskService;
    private readonly IAiResponseCacheService _cacheService;
    private readonly ITextChunkingService? _textChunkingServiceOverride;
    private readonly IEmbeddingService? _embeddingServiceOverride;
    private readonly IQdrantService? _qdrantServiceOverride;
    private readonly string _storageBasePath;
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private static readonly HashSet<string> AllowedContentTypes = new()
    {
        "application/pdf"
    };

    public PdfStorageService(
        MeepleAiDbContext db,
        IServiceScopeFactory scopeFactory,
        IConfiguration config,
        ILogger<PdfStorageService> logger,
        PdfTextExtractionService textExtractionService,
        PdfTableExtractionService tableExtractionService,
        IBackgroundTaskService backgroundTaskService,
        IAiResponseCacheService cacheService,
        ITextChunkingService? textChunkingService = null,
        IEmbeddingService? embeddingService = null,
        IQdrantService? qdrantService = null)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _textExtractionService = textExtractionService;
        _tableExtractionService = tableExtractionService;
        _backgroundTaskService = backgroundTaskService;
        _cacheService = cacheService;
        _textChunkingServiceOverride = textChunkingService;
        _embeddingServiceOverride = embeddingService;
        _qdrantServiceOverride = qdrantService;
        _storageBasePath = config["PDF_STORAGE_PATH"] ?? Path.Combine(Directory.GetCurrentDirectory(), "pdf_uploads");

        // Ensure storage directory exists
        if (!Directory.Exists(_storageBasePath))
        {
            Directory.CreateDirectory(_storageBasePath);
            _logger.LogInformation("Created PDF storage directory at {Path}", _storageBasePath);
        }
    }

    public async Task<PdfUploadResult> UploadPdfAsync(
        string gameId,
        string userId,
        IFormFile file,
        CancellationToken ct = default)
    {
        // Validate file
        if (file == null || file.Length == 0)
        {
            return new PdfUploadResult(false, "No file provided. Please select a PDF file to upload.", null);
        }

        if (file.Length > MaxFileSizeBytes)
        {
            var sizeMB = file.Length / 1024.0 / 1024.0;
            var maxMB = MaxFileSizeBytes / 1024 / 1024;
            return new PdfUploadResult(false, $"File is too large ({sizeMB:F1}MB). Maximum size is {maxMB}MB. Try compressing the PDF or splitting into smaller files.", null);
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return new PdfUploadResult(false, $"Invalid file type ({file.ContentType}). Only PDF files are allowed. Please ensure your file has a .pdf extension.", null);
        }

        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new PdfUploadResult(false, "Invalid file name. The file must have a valid name.", null);
        }

        // Verify game exists
        var game = await _db.Games
            .Where(g => g.Id == gameId)
            .FirstOrDefaultAsync(ct);

        if (game == null)
        {
            return new PdfUploadResult(false, "Game not found. Please select a valid game before uploading.", null);
        }

        try
        {
            // Generate unique file path
            var fileId = Guid.NewGuid().ToString("N");
            var gameDir = Path.Combine(_storageBasePath, gameId);
            Directory.CreateDirectory(gameDir);

            var sanitizedFileName = SanitizeFileName(fileName);
            var filePath = Path.Combine(gameDir, $"{fileId}_{sanitizedFileName}");

            // Save file to disk
            using (var stream = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None))
            {
                await file.CopyToAsync(stream, ct);
            }

            _logger.LogInformation("Saved PDF file to {FilePath}", filePath);

            // Create database record
            var pdfDoc = new PdfDocumentEntity
            {
                Id = fileId,
                GameId = gameId,
                FileName = sanitizedFileName,
                FilePath = filePath,
                FileSizeBytes = file.Length,
                ContentType = file.ContentType,
                UploadedByUserId = userId,
                UploadedAt = DateTime.UtcNow,
                ProcessingStatus = "pending"
            };

            _db.PdfDocuments.Add(pdfDoc);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Created PDF document record {PdfId} for game {GameId}", fileId, gameId);

            // PDF-08: Initialize progress tracking
            pdfDoc.ProcessingProgress = new ProcessingProgress
            {
                CurrentStep = ProcessingStep.Uploading,
                PercentComplete = 20, // Upload completed
                ElapsedTime = TimeSpan.Zero,
                EstimatedTimeRemaining = null,
                PagesProcessed = 0,
                TotalPages = 0,
                StartedAt = DateTime.UtcNow,
                CompletedAt = null,
                ErrorMessage = null
            };
            await _db.SaveChangesAsync(ct);

            // Extract text asynchronously (PDF-02) with cancellation support (PDF-08)
            _backgroundTaskService.ExecuteWithCancellation(fileId, (cancellationToken) => ProcessPdfAsync(fileId, filePath, cancellationToken));

            await InvalidateCacheSafelyAsync(gameId, ct, "PDF upload");

            return new PdfUploadResult(true, "PDF uploaded successfully", new PdfDocumentDto
            {
                Id = pdfDoc.Id,
                FileName = pdfDoc.FileName,
                FileSizeBytes = pdfDoc.FileSizeBytes,
                UploadedAt = pdfDoc.UploadedAt,
                UploadedByUserId = pdfDoc.UploadedByUserId
            });
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "File I/O error during PDF upload for game {GameId}", gameId);
            throw new PdfStorageException("Failed to save PDF file: I/O error occurred.", ex);
        }
        catch (UnauthorizedAccessException ex)
        {
            _logger.LogError(ex, "Access denied during PDF upload for game {GameId}", gameId);
            throw new PdfStorageException("Failed to save PDF file: Access denied to storage location.", ex);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during PDF upload for game {GameId}", gameId);
            throw new PdfStorageException("Failed to save PDF metadata: Database error occurred.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during PDF upload for game {GameId}", gameId);
            throw new PdfStorageException($"Failed to upload PDF: {ex.Message}", ex);
        }
    }

    public async Task<List<PdfDocumentDto>> GetPdfsByGameAsync(string gameId, CancellationToken ct = default)
    {
        var pdfs = await _db.PdfDocuments
            .AsNoTracking() // PERF-05: Read-only query for listing PDFs
            .Where(p => p.GameId == gameId)
            .OrderByDescending(p => p.UploadedAt)
            .Select(p => new PdfDocumentDto
            {
                Id = p.Id,
                FileName = p.FileName,
                FileSizeBytes = p.FileSizeBytes,
                UploadedAt = p.UploadedAt,
                UploadedByUserId = p.UploadedByUserId,
                Language = p.Language // AI-09: Include language metadata
            })
            .ToListAsync(ct);

        return pdfs;
    }

    /// <summary>
    /// Deletes a PDF document and all associated data
    /// </summary>
    /// <param name="pdfId">ID of the PDF to delete</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Result indicating success or failure with error message</returns>
    public async Task<PdfDeleteResult> DeletePdfAsync(string pdfId, CancellationToken ct = default)
    {
        try
        {
            var pdfDoc = await _db.PdfDocuments
                .FirstOrDefaultAsync(p => p.Id == pdfId, ct);

            if (pdfDoc == null)
            {
                return new PdfDeleteResult(false, "PDF not found", null);
            }

            var gameId = pdfDoc.GameId;
            var filePath = pdfDoc.FilePath;

            // Delete associated vector document if exists
            var vectorDoc = await _db.VectorDocuments
                .FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId, ct);

            if (vectorDoc != null)
            {
                _db.VectorDocuments.Remove(vectorDoc);
                _logger.LogInformation("Removed vector document for PDF {PdfId}", pdfId);

                // Delete vectors from Qdrant
                try
                {
                    IQdrantService? qdrantService = _qdrantServiceOverride;

                    if (qdrantService == null)
                    {
                        using var scope = _scopeFactory.CreateScope();
                        qdrantService = scope.ServiceProvider.GetService<IQdrantService>();
                    }

                    if (qdrantService != null)
                    {
                        var deleteResult = await qdrantService.DeleteDocumentAsync(pdfId, ct);
                        if (!deleteResult)
                        {
                            _logger.LogWarning("Failed to delete vectors from Qdrant for PDF {PdfId}",
                                pdfId);
                        }
                    }
                }
                catch (OperationCanceledException)
                {
                    throw;
                }
                catch (InvalidOperationException ex)
                {
                    _logger.LogWarning(ex, "Invalid operation deleting vectors from Qdrant for PDF {PdfId}", pdfId);
                }
                catch (Exception ex)
                {
                    // CLEANUP PATTERN: Qdrant deletion failures must not prevent PDF deletion
                    // Rationale: PDF deletion is a critical user operation. If Qdrant is unavailable,
                    // we still delete the DB records and file to maintain data consistency from the
                    // user's perspective. Orphaned Qdrant vectors can be cleaned up later via maintenance
                    // jobs. Blocking deletion would create a poor user experience during Qdrant outages.
                    // Context: Qdrant failures are typically external (service down, network timeout)
                    _logger.LogWarning(ex, "Unexpected error deleting vectors from Qdrant for PDF {PdfId}", pdfId);
                }
            }

            // Delete PDF document record
            _db.PdfDocuments.Remove(pdfDoc);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Deleted PDF document record {PdfId}", pdfId);

            // Delete physical file
            try
            {
                if (!string.IsNullOrEmpty(filePath) && File.Exists(filePath))
                {
                    File.Delete(filePath);
                    _logger.LogInformation("Deleted physical file at {FilePath}", filePath);
                }
            }
            catch (IOException ex)
            {
                _logger.LogWarning(ex, "I/O error deleting physical file for PDF {PdfId} at {FilePath}", pdfId, filePath);
                // Don't fail the operation if file deletion fails
            }
            catch (UnauthorizedAccessException ex)
            {
                _logger.LogWarning(ex, "Access denied deleting physical file for PDF {PdfId} at {FilePath}", pdfId, filePath);
                // Don't fail the operation if file deletion fails
            }
            catch (Exception ex)
            {
                // CLEANUP PATTERN: File deletion failures must not prevent PDF deletion completion
                // Rationale: Database records are already deleted successfully. File deletion is
                // a cleanup operation - failing the entire PDF deletion because we cannot remove the
                // physical file would create inconsistency (DB says deleted, file exists). Orphaned
                // files can be cleaned up later via filesystem maintenance. User sees successful deletion.
                // Context: File failures are typically permission/locking issues (antivirus, backup)
                _logger.LogWarning(ex, "Unexpected error deleting physical file for PDF {PdfId} at {FilePath}", pdfId, filePath);
            }

            // Invalidate cache
            await InvalidateCacheSafelyAsync(gameId, ct, "PDF deletion");

            return new PdfDeleteResult(true, "PDF deleted successfully", gameId);
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (DbUpdateConcurrencyException ex)
        {
            _logger.LogError(ex, "Concurrency conflict deleting PDF {PdfId}", pdfId);
            throw new PdfStorageException("Failed to delete PDF: The PDF was modified by another operation.", ex);
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error deleting PDF {PdfId}", pdfId);
            throw new PdfStorageException("Failed to delete PDF metadata: Database error occurred.", ex);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting PDF {PdfId}", pdfId);
            throw new PdfStorageException($"Failed to delete PDF: {ex.Message}", ex);
        }
    }

    /// <summary>
    /// PDF-08: Main processing pipeline with progress tracking
    /// </summary>
    private async Task ProcessPdfAsync(string pdfId, string filePath, CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var startTime = DateTime.UtcNow;

        try
        {
            var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfId }, ct);
            if (pdfDoc == null)
            {
                _logger.LogError("PDF document {PdfId} not found for processing", pdfId);
                return;
            }

            // Step 1: Extract text with page tracking (AI-08) (20-40%)
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Extracting, 0, 0, startTime, null, ct);
            var extractResult = await _textExtractionService.ExtractPagedTextAsync(filePath, ct);

            if (!extractResult.Success)
            {
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, extractResult.Error, ct);
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = extractResult.Error;
                pdfDoc.ProcessedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return;
            }

            // Combine all page chunks into full text for backward compatibility
            var fullText = string.Join("\n\n", extractResult.PageChunks
                .Where(pc => !pc.IsEmpty)
                .Select(pc => pc.Text));

            pdfDoc.ExtractedText = fullText;
            pdfDoc.PageCount = extractResult.TotalPageCount;
            pdfDoc.CharacterCount = fullText.Length;
            await db.SaveChangesAsync(ct);

            // Extract structured content (tables, diagrams)
            var tableExtractionService = scope.ServiceProvider.GetService<PdfTableExtractionService>() ?? _tableExtractionService;
            if (tableExtractionService != null)
            {
                var structuredResult = await tableExtractionService.ExtractStructuredContentAsync(filePath);
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

            var totalPages = extractResult.TotalPageCount;
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Extracting, totalPages, totalPages, startTime, null, ct);

            // Step 2: Chunk text with page tracking (AI-08) (40-60%)
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Chunking, 0, totalPages, startTime, null, ct);
            var chunkingService = _textChunkingServiceOverride ?? scope.ServiceProvider.GetRequiredService<ITextChunkingService>();

            // Process each page separately to preserve page numbers
            var allDocumentChunks = new List<DocumentChunkInput>();

            foreach (var pageChunk in extractResult.PageChunks.Where(pc => !pc.IsEmpty))
            {
                var pageTextChunks = chunkingService.ChunkText(pageChunk.Text, 512, 50);

                foreach (var textChunk in pageTextChunks)
                {
                    allDocumentChunks.Add(new DocumentChunkInput
                    {
                        Text = textChunk.Text,
                        Page = pageChunk.PageNumber,  // Preserve accurate page number (AI-08 fix)
                        CharStart = textChunk.CharStart,
                        CharEnd = textChunk.CharEnd
                    });
                }
            }

            if (allDocumentChunks.Count == 0)
            {
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, "No chunks generated from text", ct);
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = "No chunks generated from text";
                pdfDoc.ProcessedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return;
            }

            _logger.LogInformation("Generated {ChunkCount} chunks for PDF {PdfId} across {PageCount} pages",
                allDocumentChunks.Count, pdfId, extractResult.PageChunks.Count(pc => !pc.IsEmpty));
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Chunking, totalPages, totalPages, startTime, null, ct);

            // Step 3: Generate embeddings (60-80%)
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Embedding, 0, totalPages, startTime, null, ct);
            var embeddingService = _embeddingServiceOverride ?? scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
            var texts = allDocumentChunks.Select(c => c.Text).ToList();
            var embeddingResult = await embeddingService.GenerateEmbeddingsAsync(texts);

            if (!embeddingResult.Success)
            {
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, $"Embedding generation failed: {embeddingResult.ErrorMessage}", ct);
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = embeddingResult.ErrorMessage;
                pdfDoc.ProcessedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return;
            }

            await UpdateProgressAsync(db, pdfId, ProcessingStep.Embedding, totalPages, totalPages, startTime, null, ct);

            // Step 4: Index in Qdrant with accurate page numbers (AI-08) (80-100%)
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Indexing, 0, totalPages, startTime, null, ct);
            var qdrantService = _qdrantServiceOverride ?? scope.ServiceProvider.GetRequiredService<IQdrantService>();

            var documentChunks = new List<DocumentChunk>();
            for (int i = 0; i < allDocumentChunks.Count; i++)
            {
                documentChunks.Add(new DocumentChunk
                {
                    Text = allDocumentChunks[i].Text,
                    Embedding = embeddingResult.Embeddings[i],
                    Page = allDocumentChunks[i].Page,  // Accurate page number from AI-08
                    CharStart = allDocumentChunks[i].CharStart,
                    CharEnd = allDocumentChunks[i].CharEnd
                });
            }

            var indexResult = await qdrantService.IndexDocumentChunksAsync(pdfDoc.GameId, pdfId, documentChunks);

            if (!indexResult.Success)
            {
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, $"Qdrant indexing failed: {indexResult.ErrorMessage}", ct);
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = indexResult.ErrorMessage;
                pdfDoc.ProcessedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(ct);
                return;
            }

            // Update vector document
            var vectorDoc = await db.VectorDocuments.FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId, ct);
            if (vectorDoc == null)
            {
                vectorDoc = new VectorDocumentEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    GameId = pdfDoc.GameId,
                    PdfDocumentId = pdfId,
                    IndexingStatus = "completed",
                    ChunkCount = indexResult.IndexedCount,
                    TotalCharacters = fullText.Length,  // Use fullText computed earlier (AI-08)
                    IndexedAt = DateTime.UtcNow
                };
                db.VectorDocuments.Add(vectorDoc);
            }
            else
            {
                vectorDoc.IndexingStatus = "completed";
                vectorDoc.ChunkCount = indexResult.IndexedCount;
                vectorDoc.TotalCharacters = fullText.Length;  // Use fullText computed earlier (AI-08)
                vectorDoc.IndexedAt = DateTime.UtcNow;
            }

            // Step 5: Complete (100%)
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Completed, totalPages, totalPages, startTime, null, ct);
            pdfDoc.ProcessingStatus = "completed";
            pdfDoc.ProcessedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);

            await InvalidateCacheSafelyAsync(pdfDoc.GameId, ct, "PDF processing");

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
                pdfDoc.ProcessedAt = DateTime.UtcNow;
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
                pdfDoc.ProcessedAt = DateTime.UtcNow;
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
                pdfDoc.ProcessedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during PDF processing for {PdfId}", pdfId);
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, $"Unexpected error: {ex.Message}", CancellationToken.None);

            var pdfDoc = await db.PdfDocuments.FindAsync(new object[] { pdfId }, CancellationToken.None);
            if (pdfDoc != null)
            {
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = ex.Message;
                pdfDoc.ProcessedAt = DateTime.UtcNow;
                await db.SaveChangesAsync(CancellationToken.None);
            }
        }
    }

    /// <summary>
    /// PDF-08: Updates processing progress in database
    /// </summary>
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

            var elapsed = DateTime.UtcNow - startTime;
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
                CompletedAt = step == ProcessingStep.Completed || step == ProcessingStep.Failed ? DateTime.UtcNow : null,
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error updating progress for PDF {PdfId}", pdfId);
        }
    }

    /// <summary>
    /// Extracts text from PDF asynchronously and updates database (Legacy method, kept for backward compatibility)
    /// </summary>
    private async Task ExtractTextAsync(string pdfId, string filePath)
    {
        // Create new scope for background task to avoid disposed DbContext
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

        try
        {
            _logger.LogInformation("Starting text extraction for PDF {PdfId}", pdfId);

            // Update status to processing
            var pdfDoc = await db.PdfDocuments.FindAsync(pdfId);
            if (pdfDoc == null)
            {
                _logger.LogError("PDF document {PdfId} not found for text extraction", pdfId);
                return;
            }

            pdfDoc.ProcessingStatus = "processing";
            await db.SaveChangesAsync();

            // Extract text
            var result = await _textExtractionService.ExtractTextAsync(filePath);

            if (result.Success)
            {
                pdfDoc.ExtractedText = result.ExtractedText;
                pdfDoc.PageCount = result.PageCount;
                pdfDoc.CharacterCount = result.CharacterCount;

                // PDF-03: Extract structured data (tables and diagrams)
                var tableExtractionService = scope.ServiceProvider.GetService<PdfTableExtractionService>()
                    ?? _tableExtractionService;
                if (tableExtractionService == null)
                {
                    _logger.LogWarning("No table extraction service available for PDF {PdfId}", pdfId);
                }
                else
                {
                    var structuredResult = await tableExtractionService.ExtractStructuredContentAsync(filePath);

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

                        _logger.LogInformation(
                            "Structured extraction completed for PDF {PdfId}: {TableCount} tables, {DiagramCount} diagrams, {RuleCount} atomic rules",
                            pdfId, structuredResult.TableCount, structuredResult.DiagramCount, structuredResult.AtomicRuleCount);
                    }
                }

                pdfDoc.ProcessingStatus = "completed";
                pdfDoc.ProcessedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Text extraction completed for PDF {PdfId}: {PageCount} pages, {CharCount} characters",
                    pdfId, result.PageCount, result.CharacterCount);

                await db.SaveChangesAsync();

                // AI-01: Trigger vector indexing in background
                _backgroundTaskService.Execute(() => IndexVectorsAsync(pdfDoc.GameId, pdfId, result.ExtractedText));
            }
            else
            {
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = result.ErrorMessage;
                pdfDoc.ProcessedAt = DateTime.UtcNow;

                _logger.LogError("Text extraction failed for PDF {PdfId}: {Error}", pdfId, result.ErrorMessage);

                await db.SaveChangesAsync();
            }
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during text extraction for PDF {PdfId}", pdfId);

            try
            {
                var pdfDoc = await db.PdfDocuments.FindAsync(pdfId);
                if (pdfDoc != null)
                {
                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = $"Invalid operation: {ex.Message}";
                    pdfDoc.ProcessedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
            catch (DbUpdateException innerEx)
            {
                _logger.LogError(innerEx, "Database error updating error status for PDF {PdfId}", pdfId);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Unexpected error updating error status for PDF {PdfId}", pdfId);
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during text extraction for PDF {PdfId}", pdfId);

            try
            {
                var pdfDoc = await db.PdfDocuments.FindAsync(pdfId);
                if (pdfDoc != null)
                {
                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = "Database error occurred";
                    pdfDoc.ProcessedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
            catch (DbUpdateException innerEx)
            {
                _logger.LogError(innerEx, "Database error updating error status for PDF {PdfId}", pdfId);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Unexpected error updating error status for PDF {PdfId}", pdfId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during text extraction for PDF {PdfId}", pdfId);

            try
            {
                var pdfDoc = await db.PdfDocuments.FindAsync(pdfId);
                if (pdfDoc != null)
                {
                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = ex.Message;
                    pdfDoc.ProcessedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
            catch (DbUpdateException innerEx)
            {
                _logger.LogError(innerEx, "Database error updating error status for PDF {PdfId}", pdfId);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Unexpected error updating error status for PDF {PdfId}", pdfId);
            }
        }
    }

    /// <summary>
    /// AI-01: Index PDF text as vectors in Qdrant
    /// </summary>
    private async Task IndexVectorsAsync(string gameId, string pdfId, string extractedText)
    {
        // Create new scope for background task to avoid disposed DbContext
        using var scope = _scopeFactory.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var chunkingService = _textChunkingServiceOverride
            ?? scope.ServiceProvider.GetRequiredService<ITextChunkingService>();
        var embeddingService = _embeddingServiceOverride
            ?? scope.ServiceProvider.GetRequiredService<IEmbeddingService>();
        var qdrantService = _qdrantServiceOverride
            ?? scope.ServiceProvider.GetRequiredService<IQdrantService>();

        try
        {
            _logger.LogInformation("Starting vector indexing for PDF {PdfId}", pdfId);

            var pdfDoc = await db.PdfDocuments.FirstOrDefaultAsync(p => p.Id == pdfId);
            if (pdfDoc == null)
            {
                _logger.LogWarning("PDF document {PdfId} not found when attempting to index vectors", pdfId);
                return;
            }

            // Create or update vector document record
            var vectorDoc = await db.VectorDocuments.FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId);
            if (vectorDoc == null)
            {
                vectorDoc = new VectorDocumentEntity
                {
                    Id = Guid.NewGuid().ToString("N"),
                    GameId = gameId,
                    PdfDocumentId = pdfId,
                    IndexingStatus = "processing"
                };
                db.VectorDocuments.Add(vectorDoc);
            }
            else
            {
                vectorDoc.IndexingStatus = "processing";
            }
            await db.SaveChangesAsync();

            // Step 1: Chunk the text
            var textChunks = chunkingService.PrepareForEmbedding(extractedText);
            if (textChunks.Count == 0)
            {
                throw new InvalidOperationException("No chunks generated from text");
            }

            _logger.LogInformation("Generated {ChunkCount} chunks for PDF {PdfId}", textChunks.Count, pdfId);

            // Step 2: Generate embeddings for all chunks
            var texts = textChunks.Select(c => c.Text).ToList();
            var embeddingResult = await embeddingService.GenerateEmbeddingsAsync(texts);

            if (!embeddingResult.Success)
            {
                throw new InvalidOperationException($"Embedding generation failed: {embeddingResult.ErrorMessage}");
            }

            // Step 3: Combine chunks with embeddings
            var documentChunks = new List<DocumentChunk>();
            for (int i = 0; i < textChunks.Count; i++)
            {
                documentChunks.Add(new DocumentChunk
                {
                    Text = textChunks[i].Text,
                    Embedding = embeddingResult.Embeddings[i],
                    Page = textChunks[i].Page,
                    CharStart = textChunks[i].CharStart,
                    CharEnd = textChunks[i].CharEnd
                });
            }

            // Step 4: Index in Qdrant
            var indexResult = await qdrantService.IndexDocumentChunksAsync(gameId, pdfId, documentChunks);

            if (!indexResult.Success)
            {
                throw new InvalidOperationException($"Qdrant indexing failed: {indexResult.ErrorMessage}");
            }

            // Step 5: Update vector document status
            vectorDoc.ChunkCount = indexResult.IndexedCount;
            vectorDoc.TotalCharacters = extractedText.Length;
            vectorDoc.IndexingStatus = "completed";
            vectorDoc.IndexedAt = DateTime.UtcNow;
            await db.SaveChangesAsync();

            await InvalidateCacheSafelyAsync(gameId, CancellationToken.None, "vector indexing");

            _logger.LogInformation(
                "Vector indexing completed for PDF {PdfId}: {ChunkCount} chunks indexed",
                pdfId, indexResult.IndexedCount);
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Invalid operation during vector indexing for PDF {PdfId}", pdfId);

            try
            {
                var vectorDoc = await db.VectorDocuments.FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId);
                if (vectorDoc != null)
                {
                    vectorDoc.IndexingStatus = "failed";
                    vectorDoc.IndexingError = $"Invalid operation: {ex.Message}";
                    vectorDoc.IndexedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
            catch (DbUpdateException innerEx)
            {
                _logger.LogError(innerEx, "Database error updating vector document error status for PDF {PdfId}", pdfId);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Unexpected error updating vector document error status for PDF {PdfId}", pdfId);
            }
        }
        catch (DbUpdateException ex)
        {
            _logger.LogError(ex, "Database error during vector indexing for PDF {PdfId}", pdfId);

            try
            {
                var vectorDoc = await db.VectorDocuments.FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId);
                if (vectorDoc != null)
                {
                    vectorDoc.IndexingStatus = "failed";
                    vectorDoc.IndexingError = "Database error occurred";
                    vectorDoc.IndexedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
            catch (DbUpdateException innerEx)
            {
                _logger.LogError(innerEx, "Database error updating vector document error status for PDF {PdfId}", pdfId);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Unexpected error updating vector document error status for PDF {PdfId}", pdfId);
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during vector indexing for PDF {PdfId}", pdfId);

            try
            {
                var vectorDoc = await db.VectorDocuments.FirstOrDefaultAsync(v => v.PdfDocumentId == pdfId);
                if (vectorDoc != null)
                {
                    vectorDoc.IndexingStatus = "failed";
                    vectorDoc.IndexingError = ex.Message;
                    vectorDoc.IndexedAt = DateTime.UtcNow;
                    await db.SaveChangesAsync();
                }
            }
            catch (DbUpdateException innerEx)
            {
                _logger.LogError(innerEx, "Database error updating vector document error status for PDF {PdfId}", pdfId);
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Unexpected error updating vector document error status for PDF {PdfId}", pdfId);
            }
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        // Get OS-specific invalid chars and add additional problematic chars
        var invalidChars = Path.GetInvalidFileNameChars()
            .Concat(new[] { '<', '>', '?', '*', '|', '"', ':' })
            .Distinct()
            .ToArray();

        var sanitized = string.Join("_", fileName.Split(invalidChars, StringSplitOptions.RemoveEmptyEntries));
        return sanitized.Length > 200 ? sanitized.Substring(0, 200) : sanitized;
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
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error invalidating AI cache for game {GameId} after {Operation}", gameId, operation);
        }
    }
}

public record PdfUploadResult(bool Success, string Message, PdfDocumentDto? Document);

public record PdfDeleteResult(bool Success, string Message, string? GameId);

public record PdfDocumentDto
{
    public string Id { get; init; } = default!;
    public string FileName { get; init; } = default!;
    public long FileSizeBytes { get; init; }
    public DateTime UploadedAt { get; init; }
    public string UploadedByUserId { get; init; } = default!;
    public string Language { get; init; } = "en"; // AI-09: Language code (ISO 639-1)
}