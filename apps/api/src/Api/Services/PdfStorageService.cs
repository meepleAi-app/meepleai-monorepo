using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Microsoft.EntityFrameworkCore;

namespace Api.Services;

public class PdfStorageService
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<PdfStorageService> _logger;
    private readonly PdfTextExtractionService _textExtractionService;
    private readonly string _storageBasePath;
    private const long MaxFileSizeBytes = 50 * 1024 * 1024; // 50 MB
    private static readonly HashSet<string> AllowedContentTypes = new()
    {
        "application/pdf"
    };

    public PdfStorageService(
        MeepleAiDbContext db,
        IConfiguration config,
        ILogger<PdfStorageService> logger,
        PdfTextExtractionService textExtractionService)
    {
        _db = db;
        _logger = logger;
        _textExtractionService = textExtractionService;
        _storageBasePath = config["PDF_STORAGE_PATH"] ?? Path.Combine(Directory.GetCurrentDirectory(), "pdf_uploads");

        // Ensure storage directory exists
        if (!Directory.Exists(_storageBasePath))
        {
            Directory.CreateDirectory(_storageBasePath);
            _logger.LogInformation("Created PDF storage directory at {Path}", _storageBasePath);
        }
    }

    public async Task<PdfUploadResult> UploadPdfAsync(
        string tenantId,
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

        // Verify game exists and belongs to tenant
        var game = await _db.Games
            .Where(g => g.Id == gameId && g.TenantId == tenantId)
            .FirstOrDefaultAsync(ct);

        if (game == null)
        {
            return new PdfUploadResult(false, "Game not found or access denied", null);
        }

        try
        {
            // Generate unique file path
            var fileId = Guid.NewGuid().ToString("N");
            var tenantDir = Path.Combine(_storageBasePath, tenantId, gameId);
            Directory.CreateDirectory(tenantDir);

            var sanitizedFileName = SanitizeFileName(fileName);
            var filePath = Path.Combine(tenantDir, $"{fileId}_{sanitizedFileName}");

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
                TenantId = tenantId,
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
            _ = Task.Run(async () => await ExtractTextAsync(fileId, filePath), CancellationToken.None);

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

    public async Task<List<PdfDocumentDto>> GetPdfsByGameAsync(string tenantId, string gameId, CancellationToken ct = default)
    {
        var pdfs = await _db.PdfDocuments
            .Where(p => p.TenantId == tenantId && p.GameId == gameId)
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
        try
        {
            _logger.LogInformation("Starting text extraction for PDF {PdfId}", pdfId);

            // Update status to processing
            var pdfDoc = await _db.PdfDocuments.FindAsync(pdfId);
            if (pdfDoc == null)
            {
                _logger.LogError("PDF document {PdfId} not found for text extraction", pdfId);
                return;
            }

            pdfDoc.ProcessingStatus = "processing";
            await _db.SaveChangesAsync();

            // Extract text
            var result = await _textExtractionService.ExtractTextAsync(filePath);

            if (result.Success)
            {
                pdfDoc.ExtractedText = result.ExtractedText;
                pdfDoc.PageCount = result.PageCount;
                pdfDoc.CharacterCount = result.CharacterCount;
                pdfDoc.ProcessingStatus = "completed";
                pdfDoc.ProcessedAt = DateTime.UtcNow;

                _logger.LogInformation(
                    "Text extraction completed for PDF {PdfId}: {PageCount} pages, {CharCount} characters",
                    pdfId, result.PageCount, result.CharacterCount);
            }
            else
            {
                pdfDoc.ProcessingStatus = "failed";
                pdfDoc.ProcessingError = result.ErrorMessage;
                pdfDoc.ProcessedAt = DateTime.UtcNow;

                _logger.LogError("Text extraction failed for PDF {PdfId}: {Error}", pdfId, result.ErrorMessage);
            }

            await _db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error during text extraction for PDF {PdfId}", pdfId);

            try
            {
                var pdfDoc = await _db.PdfDocuments.FindAsync(pdfId);
                if (pdfDoc != null)
                {
                    pdfDoc.ProcessingStatus = "failed";
                    pdfDoc.ProcessingError = ex.Message;
                    pdfDoc.ProcessedAt = DateTime.UtcNow;
                    await _db.SaveChangesAsync();
                }
            }
            catch (Exception innerEx)
            {
                _logger.LogError(innerEx, "Failed to update error status for PDF {PdfId}", pdfId);
            }
        }
    }

    private static string SanitizeFileName(string fileName)
    {
        var invalidChars = Path.GetInvalidFileNameChars();
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