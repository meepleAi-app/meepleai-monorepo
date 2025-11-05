using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Models;
using Api.Services.Pdf;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Api.Services.Exceptions;

namespace Api.Services;

/// <summary>
/// Coordinator service for PDF storage, validation, and processing
/// Delegates to specialized services for metadata extraction and blob storage
/// </summary>
public class PdfStorageService
{
    private readonly MeepleAiDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PdfStorageService> _logger;
    private readonly PdfTextExtractionService _textExtractionService;
    private readonly PdfTableExtractionService _tableExtractionService;
    private readonly IBackgroundTaskService _backgroundTaskService;
    private readonly IAiResponseCacheService _cacheService;
    private readonly IBlobStorageService _blobStorageService;
    private readonly ITextChunkingService? _textChunkingServiceOverride;
    private readonly IEmbeddingService? _embeddingServiceOverride;
    private readonly IQdrantService? _qdrantServiceOverride;
    private readonly TimeProvider _timeProvider;
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private static readonly HashSet<string> AllowedContentTypes = new()
    {
        "application/pdf"
    };

    public PdfStorageService(
        MeepleAiDbContext db,
        IServiceScopeFactory scopeFactory,
        ILogger<PdfStorageService> logger,
        PdfTextExtractionService textExtractionService,
        PdfTableExtractionService tableExtractionService,
        IBackgroundTaskService backgroundTaskService,
        IAiResponseCacheService cacheService,
        IBlobStorageService blobStorageService,
        ITextChunkingService? textChunkingService = null,
        IEmbeddingService? embeddingService = null,
        IQdrantService? qdrantService = null,
        TimeProvider? timeProvider = null)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _textExtractionService = textExtractionService;
        _tableExtractionService = tableExtractionService;
        _backgroundTaskService = backgroundTaskService;
        _cacheService = cacheService;
        _blobStorageService = blobStorageService;
        _textChunkingServiceOverride = textChunkingService;
        _embeddingServiceOverride = embeddingService;
        _qdrantServiceOverride = qdrantService;
        _timeProvider = timeProvider ?? TimeProvider.System;
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
            // Delegate file storage to BlobStorageService
            BlobStorageResult storageResult;
            using (var stream = file.OpenReadStream())
            {
                storageResult = await _blobStorageService.StoreAsync(stream, fileName, gameId, ct);
            }

            if (!storageResult.Success)
            {
                return new PdfUploadResult(false, storageResult.ErrorMessage ?? "Failed to store file", null);
            }

            _logger.LogInformation("Saved PDF file to {FilePath}", storageResult.FilePath);

            // Create database record
            var pdfDoc = new PdfDocumentEntity
            {
                Id = storageResult.FileId!,
                GameId = gameId,
                FileName = fileName,
                FilePath = storageResult.FilePath!,
                FileSizeBytes = storageResult.FileSizeBytes,
                ContentType = file.ContentType,
                UploadedByUserId = userId,
                UploadedAt = _timeProvider.GetUtcNow().UtcDateTime,
                ProcessingStatus = "pending"
            };

            _db.PdfDocuments.Add(pdfDoc);
            await _db.SaveChangesAsync(ct);

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
            await _db.SaveChangesAsync(ct);

            // Extract text asynchronously (PDF-02) with cancellation support (PDF-08)
            // Note: FileId and FilePath are guaranteed non-null after successful storage
            _backgroundTaskService.ExecuteWithCancellation(storageResult.FileId!, (cancellationToken) => ProcessPdfAsync(storageResult.FileId!, storageResult.FilePath!, cancellationToken));

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
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Service boundary - wraps unexpected exceptions in domain-specific PdfStorageException
        // Already handling specific exception types (IO, UnauthorizedAccess, DbUpdate); this catches remaining failures
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during PDF upload for game {GameId}", gameId);
            throw new PdfStorageException($"Failed to upload PDF: {ex.Message}", ex);
        }
#pragma warning restore CA1031
    }

    public async Task<List<PdfDocumentDto>> GetPdfsByGameAsync(string gameId, CancellationToken ct = default)
    {
        var pdfs = await _db.PdfDocuments
            .AsNoTracking()
            .Where(p => p.GameId == gameId)
            .OrderByDescending(p => p.UploadedAt)
            .Select(p => new PdfDocumentDto
            {
                Id = p.Id,
                FileName = p.FileName,
                FileSizeBytes = p.FileSizeBytes,
                UploadedAt = p.UploadedAt,
                UploadedByUserId = p.UploadedByUserId,
                Language = p.Language
            })
            .ToListAsync(ct);

        return pdfs;
    }

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
#pragma warning disable CA1031 // Do not catch general exception types
                // Justification: Cleanup operation - must continue PDF deletion even if Qdrant cleanup fails
                // Vector deletion is non-critical and should not block the main deletion flow
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Unexpected error deleting vectors from Qdrant for PDF {PdfId}", pdfId);
                }
#pragma warning restore CA1031
            }

            // Delete PDF document record
            _db.PdfDocuments.Remove(pdfDoc);
            await _db.SaveChangesAsync(ct);

            _logger.LogInformation("Deleted PDF document record {PdfId}", pdfId);

            // Delegate physical file deletion to BlobStorageService
            try
            {
                await _blobStorageService.DeleteAsync(pdfId, gameId, ct);
            }
#pragma warning disable CA1031 // Do not catch general exception types
            // Justification: Cleanup operation - physical file deletion failure should not fail entire operation
            // Database record is already deleted; physical file cleanup is best-effort
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error deleting physical file for PDF {PdfId}", pdfId);
            }
#pragma warning restore CA1031

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
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Service boundary - wraps unexpected exceptions in domain-specific PdfStorageException
        // Already handling specific exception types (Cancellation, DbConcurrency, DbUpdate); this catches remaining failures
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error deleting PDF {PdfId}", pdfId);
            throw new PdfStorageException($"Failed to delete PDF: {ex.Message}", ex);
        }
#pragma warning restore CA1031
    }

    // Keep all the remaining methods (ProcessPdfAsync, UpdateProgressAsync, ExtractTextAsync, IndexVectorsAsync, InvalidateCacheSafelyAsync)
    // These are unchanged from the original implementation

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

            // Step 1: Extract text with page tracking (AI-08) (20-40%)
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Extracting, 0, 0, startTime, null, ct);
            var extractResult = await _textExtractionService.ExtractPagedTextAsync(filePath, ct);

            if (!extractResult.Success)
            {
                await UpdateProgressAsync(db, pdfId, ProcessingStep.Failed, 0, 0, startTime, extractResult.Error, ct);
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = extractResult.Error;
                pdfDoc.ProcessedAt = _timeProvider.GetUtcNow().UtcDateTime;
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
                if (vector == null || vector.Length == 0 || Array.Exists(vector, v => float.IsNaN(v) || float.IsInfinity(v)))
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


            // Step 4: Index in Qdrant with accurate page numbers (AI-08) (80-100%)
            await UpdateProgressAsync(db, pdfId, ProcessingStep.Indexing, 0, totalPages, startTime, null, ct);
            var qdrantService = _qdrantServiceOverride ?? scope.ServiceProvider.GetRequiredService<IQdrantService>();

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

            var indexResult = await qdrantService.IndexDocumentChunksAsync(pdfDoc.GameId, pdfId, documentChunks);

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
        // Justification: Background task error handling - must catch all exceptions to mark PDF processing as failed
        // Already handling specific exception types (Cancellation, InvalidOperation, DbUpdate); this catches remaining failures
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
        // Justification: Non-critical progress tracking - failures should not interrupt PDF processing
        // Progress updates are informational; processing should continue even if tracking fails
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
        // Justification: Non-critical cache invalidation - failures should not interrupt PDF operations
        // Cache invalidation is a best-effort operation; main workflow should succeed regardless
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Unexpected error invalidating AI cache for game {GameId} after {Operation}", gameId, operation);
        }
#pragma warning restore CA1031
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
    public string Language { get; init; } = "en";
}



