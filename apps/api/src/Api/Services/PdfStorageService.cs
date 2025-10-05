using System.Linq;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class PdfStorageService
{
    private readonly MeepleAiDbContext _db;
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<PdfStorageService> _logger;
    private readonly PdfTextExtractionService _textExtractionService;
    private readonly PdfTableExtractionService _tableExtractionService;
    private readonly IBackgroundTaskService _backgroundTaskService;
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
        IBackgroundTaskService backgroundTaskService)
    {
        _db = db;
        _scopeFactory = scopeFactory;
        _logger = logger;
        _textExtractionService = textExtractionService;
        _tableExtractionService = tableExtractionService;
        _backgroundTaskService = backgroundTaskService;
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
            return new PdfUploadResult(false, "No file provided", null);
        }

        if (file.Length > MaxFileSizeBytes)
        {
            return new PdfUploadResult(false, $"File size exceeds maximum allowed size of {MaxFileSizeBytes / 1024 / 1024} MB", null);
        }

        if (!AllowedContentTypes.Contains(file.ContentType))
        {
            return new PdfUploadResult(false, $"Invalid file type. Only PDF files are allowed.", null);
        }

        var fileName = Path.GetFileName(file.FileName);
        if (string.IsNullOrWhiteSpace(fileName))
        {
            return new PdfUploadResult(false, "Invalid file name", null);
        }

        // Verify game exists
        var game = await _db.Games
            .Where(g => g.Id == gameId)
            .FirstOrDefaultAsync(ct);

        if (game == null)
        {
            return new PdfUploadResult(false, "Game not found or access denied", null);
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

            // Extract text asynchronously (PDF-02)
            _backgroundTaskService.Execute(() => ExtractTextAsync(fileId, filePath));

            return new PdfUploadResult(true, "PDF uploaded successfully", new PdfDocumentDto
            {
                Id = pdfDoc.Id,
                FileName = pdfDoc.FileName,
                FileSizeBytes = pdfDoc.FileSizeBytes,
                UploadedAt = pdfDoc.UploadedAt,
                UploadedByUserId = pdfDoc.UploadedByUserId
            });
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to upload PDF for game {GameId}", gameId);
            return new PdfUploadResult(false, "Failed to upload PDF: " + ex.Message, null);
        }
    }

    public async Task<List<PdfDocumentDto>> GetPdfsByGameAsync(string gameId, CancellationToken ct = default)
    {
        var pdfs = await _db.PdfDocuments
            .Where(p => p.GameId == gameId)
            .OrderByDescending(p => p.UploadedAt)
            .Select(p => new PdfDocumentDto
            {
                Id = p.Id,
                FileName = p.FileName,
                FileSizeBytes = p.FileSizeBytes,
                UploadedAt = p.UploadedAt,
                UploadedByUserId = p.UploadedByUserId
            })
            .ToListAsync(ct);

        return pdfs;
    }

    /// <summary>
    /// Extracts text from PDF asynchronously and updates database
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
                var tableExtractionService = scope.ServiceProvider.GetRequiredService<PdfTableExtractionService>();
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
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during text extraction for PDF {PdfId}", pdfId);

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
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Failed to update error status for PDF {PdfId}", pdfId);
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
        var chunkingService = scope.ServiceProvider.GetRequiredService<TextChunkingService>();
        var embeddingService = scope.ServiceProvider.GetRequiredService<EmbeddingService>();
        var qdrantService = scope.ServiceProvider.GetRequiredService<IQdrantService>();

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

            _logger.LogInformation(
                "Vector indexing completed for PDF {PdfId}: {ChunkCount} chunks indexed",
                pdfId, indexResult.IndexedCount);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Vector indexing failed for PDF {PdfId}", pdfId);

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
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Failed to update vector document error status for PDF {PdfId}", pdfId);
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
}

public record PdfUploadResult(bool Success, string Message, PdfDocumentDto? Document);

public record PdfDocumentDto
{
    public string Id { get; init; } = default!;
    public string FileName { get; init; } = default!;
    public long FileSizeBytes { get; init; }
    public DateTime UploadedAt { get; init; }
    public string UploadedByUserId { get; init; } = default!;
}