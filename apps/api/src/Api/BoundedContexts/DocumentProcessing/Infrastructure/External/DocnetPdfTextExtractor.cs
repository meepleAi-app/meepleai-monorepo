using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.Services;
using Docnet.Core;
using Docnet.Core.Models;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Docnet.Core adapter for PDF text extraction with OCR fallback coordination
/// Implements thread-safe PDF processing with semaphore-based concurrency control
/// </summary>
internal class DocnetPdfTextExtractor : IPdfTextExtractor
{
    private readonly ILogger<DocnetPdfTextExtractor> _logger;
    private readonly PdfTextProcessingDomainService _domainService;
    private readonly IOcrService? _ocrService;

    // Semaphore to prevent concurrent access to Docnet.Core (not thread-safe)
    // Max 4 concurrent extractions to prevent native library crashes
    private static readonly SemaphoreSlim DocnetSemaphore = new(4, 4);

    public DocnetPdfTextExtractor(
        ILogger<DocnetPdfTextExtractor> logger,
        PdfTextProcessingDomainService domainService,
        IOcrService? ocrService = null)
    {
        _logger = logger;
        _domainService = domainService;
        _ocrService = ocrService;
    }

    public async Task<TextExtractionResult> ExtractTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken ct = default)
    {
        string? tempFilePath = null;

        try
        {
            // Step 1: Write stream to temp file (Docnet.Core requires file path)
            tempFilePath = await WriteTempFileAsync(pdfStream, ct).ConfigureAwait(false);

            // Step 2: Extract raw text with Docnet (infrastructure)
            await DocnetSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var (rawText, pageCount) = await Task.Run(() => ExtractRawText(tempFilePath), ct).ConfigureAwait(false);

                // Step 3: Normalize text (domain service)
                var normalizedText = PdfTextProcessingDomainService.NormalizeText(rawText);

                // Step 4: Assess quality (domain service)
                var quality = PdfTextProcessingDomainService.AssessQuality(normalizedText, pageCount);

                // Step 5: OCR fallback decision (domain service)
                var shouldOcr = enableOcrFallback
                    && _ocrService != null
                    && _domainService.ShouldTriggerOcr(normalizedText, pageCount);

                if (shouldOcr && _ocrService != null)
                {
                    _logger.LogInformation(
                        "Standard extraction quality too low (Quality: {Quality}). Falling back to OCR for temp file",
                        quality);

                    // Step 6: Trigger OCR (infrastructure - delegate to IOcrService)
                    var ocrResult = await _ocrService.ExtractTextFromPdfAsync(tempFilePath, ct).ConfigureAwait(false);

                    if (!ocrResult.Success)
                    {
                        _logger.LogWarning(
                            "OCR fallback failed: {Error}. Using standard extraction.",
                            ocrResult.ErrorMessage);

                        // Use standard extraction despite poor quality
                        return TextExtractionResult.CreateSuccess(
                            normalizedText,
                            pageCount,
                            normalizedText.Length,
                            ocrTriggered: false,
                            quality);
                    }

                    // Step 7: Normalize OCR text (domain service)
                    var normalizedOcrText = PdfTextProcessingDomainService.NormalizeText(ocrResult.ExtractedText);
                    var ocrQuality = PdfTextProcessingDomainService.AssessQuality(normalizedOcrText, ocrResult.PageCount);

                    _logger.LogInformation(
                        "OCR extraction completed. Pages: {PageCount}, Characters: {CharCount}, Quality: {Quality}",
                        ocrResult.PageCount, normalizedOcrText.Length, ocrQuality);

                    return TextExtractionResult.CreateSuccess(
                        normalizedOcrText,
                        ocrResult.PageCount,
                        normalizedOcrText.Length,
                        ocrTriggered: true,
                        ocrQuality);
                }

                // Standard extraction was good enough
                _logger.LogInformation(
                    "Extracted text from PDF. Pages: {PageCount}, Characters: {CharCount}, Quality: {Quality}",
                    pageCount, normalizedText.Length, quality);

                return TextExtractionResult.CreateSuccess(
                    normalizedText,
                    pageCount,
                    normalizedText.Length,
                    ocrTriggered: false,
                    quality);
            }
            finally
            {
                DocnetSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF: invalid operation");
            return TextExtractionResult.CreateFailure($"Invalid PDF operation: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF: invalid argument");
            return TextExtractionResult.CreateFailure($"Invalid PDF argument: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF: unsupported format");
            return TextExtractionResult.CreateFailure($"Unsupported PDF format: {ex.Message}");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to extract text from PDF: I/O error");
            return TextExtractionResult.CreateFailure($"Failed to read PDF file: {ex.Message}");
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // ADAPTER BOUNDARY PATTERN: PDF text extraction adapter boundary - must handle all errors gracefully
            // Rationale: This is an adapter for untrusted PDF files. PDFs can trigger various runtime exceptions
            // (Docnet.Core native crashes, encoding errors, corrupt structures). We must catch all exceptions
            // to return a user-friendly error result instead of crashing the service.
            // Context: Docnet.Core can throw unexpected exceptions from native PDF parsing libraries
            _logger.LogError(ex, "Failed to extract text from PDF");
            return TextExtractionResult.CreateFailure($"Failed to extract text from PDF: {ex.Message}");
        }
#pragma warning restore CA1031 // Do not catch general exception types
        finally
        {
            // Cleanup: Always delete temp file
            CleanupTempFile(tempFilePath);
        }
    }

    public async Task<PagedTextExtractionResult> ExtractPagedTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken ct = default)
    {
        string? tempFilePath = null;

        try
        {
            // Step 1: Write stream to temp file (Docnet.Core requires file path)
            tempFilePath = await WriteTempFileAsync(pdfStream, ct).ConfigureAwait(false);

            // Step 2: Extract paged text with Docnet (infrastructure)
            await DocnetSemaphore.WaitAsync(ct).ConfigureAwait(false);
            try
            {
                var pageChunks = await Task.Run(() => ExtractPagedRawText(tempFilePath), ct).ConfigureAwait(false);

                var totalPages = pageChunks.Count;
                var totalChars = pageChunks.Sum(pc => pc.CharCount);
                var nonEmptyPages = pageChunks.Count(pc => !pc.IsEmpty);

                _logger.LogInformation(
                    "Extracted paged text from PDF. Pages: {TotalPages}, Non-empty: {NonEmpty}, Total chars: {TotalChars}",
                    totalPages, nonEmptyPages, totalChars);

                // Note: OCR fallback for paged extraction not implemented in MVP
                // Would require per-page OCR coordination which is complex
                return PagedTextExtractionResult.CreateSuccess(
                    pageChunks,
                    totalPages,
                    totalChars,
                    ocrTriggered: false);
            }
            finally
            {
                DocnetSemaphore.Release();
            }
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (InvalidOperationException ex)
        {
            _logger.LogError(ex, "Failed to extract paged text from PDF: invalid operation");
            return PagedTextExtractionResult.CreateFailure($"Invalid PDF operation: {ex.Message}");
        }
        catch (ArgumentException ex)
        {
            _logger.LogError(ex, "Failed to extract paged text from PDF: invalid argument");
            return PagedTextExtractionResult.CreateFailure($"Invalid PDF argument: {ex.Message}");
        }
        catch (NotSupportedException ex)
        {
            _logger.LogError(ex, "Failed to extract paged text from PDF: unsupported format");
            return PagedTextExtractionResult.CreateFailure($"Unsupported PDF format: {ex.Message}");
        }
        catch (IOException ex)
        {
            _logger.LogError(ex, "Failed to extract paged text from PDF: I/O error");
            return PagedTextExtractionResult.CreateFailure($"Failed to read PDF file: {ex.Message}");
        }
#pragma warning disable CA1031 // Do not catch general exception types
        catch (Exception ex)
        {
            // ADAPTER BOUNDARY PATTERN: PDF paged extraction adapter boundary - must handle all errors gracefully
            _logger.LogError(ex, "Failed to extract paged text from PDF");
            return PagedTextExtractionResult.CreateFailure($"Failed to extract paged text from PDF: {ex.Message}");
        }
#pragma warning restore CA1031 // Do not catch general exception types
        finally
        {
            // Cleanup: Always delete temp file
            CleanupTempFile(tempFilePath);
        }
    }

    /// <summary>
    /// Extracts raw text from PDF using Docnet.Core
    /// CRITICAL: DocLib.Instance is a singleton - never dispose it!
    /// </summary>
    [SecurityCritical]
    private (string Text, int PageCount) ExtractRawText(string filePath)
    {
        var textBuilder = new StringBuilder();

        try
        {
            var library = DocLib.Instance; // Don't use 'using' - it's a singleton! (TEST-651)
            using var docReader = library.GetDocReader(filePath, new PageDimensions(1080, 1920));

            var pageCount = docReader.GetPageCount();

            for (int i = 0; i < pageCount; i++)
            {
                using var pageReader = docReader.GetPageReader(i);
                var pageText = pageReader.GetText();

                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    textBuilder.AppendLine(pageText);
                    textBuilder.AppendLine(); // Add separator between pages
                }
            }

            return (textBuilder.ToString(), pageCount);
        }
        catch (AccessViolationException ex)
        {
            // Native library crash - log and rethrow as managed exception
            _logger.LogError(ex, "Native library crash during PDF text extraction from {FilePath}", filePath);
            throw new InvalidOperationException(
                "PDF text extraction failed due to native library error. The PDF may be corrupted or unsupported.",
                ex);
        }
        catch (SEHException ex)
        {
            // Structured Exception Handling (SEH) from native code
            _logger.LogError(ex, "Native exception during PDF text extraction from {FilePath}", filePath);
            throw new InvalidOperationException(
                "PDF text extraction failed due to native library error. The PDF may be corrupted or unsupported.",
                ex);
        }
    }

    /// <summary>
    /// Extracts text from PDF page-by-page using Docnet.Core
    /// CRITICAL: DocLib.Instance is a singleton - never dispose it!
    /// </summary>
    private List<PageTextChunk> ExtractPagedRawText(string filePath)
    {
        var pageChunks = new List<PageTextChunk>();

        var library = DocLib.Instance; // Don't use 'using' - it's a singleton!
        using var docReader = library.GetDocReader(filePath, new PageDimensions(1080, 1920));

        var pageCount = docReader.GetPageCount();

        for (int i = 0; i < pageCount; i++)
        {
            using var pageReader = docReader.GetPageReader(i);
            var pageText = pageReader.GetText();

            // Normalize text for this page (domain service)
            var normalizedText = PdfTextProcessingDomainService.NormalizeText(pageText ?? string.Empty);

            // Create PageTextChunk (page numbers are 1-indexed for user display)
            var pageChunk = new PageTextChunk(
                PageNumber: i + 1,  // 1-indexed
                Text: normalizedText,
                CharStartIndex: 0,  // Always 0 for full-page extraction
                CharEndIndex: normalizedText.Length > 0 ? normalizedText.Length - 1 : 0
            );

            pageChunks.Add(pageChunk);
        }

        return pageChunks;
    }

    /// <summary>
    /// Writes PDF stream to temporary file for Docnet.Core processing
    /// </summary>
    private async Task<string> WriteTempFileAsync(Stream pdfStream, CancellationToken ct)
    {
        var tempPath = Path.Combine(Path.GetTempPath(), $"pdf_extract_{Guid.NewGuid()}.pdf");

        var fileStream = new FileStream(
            tempPath,
            FileMode.Create,
            FileAccess.Write,
            FileShare.None,
            bufferSize: 81920,
            useAsync: true);
        await using (fileStream.ConfigureAwait(false))
        {
            await pdfStream.CopyToAsync(fileStream, ct).ConfigureAwait(false);
            await fileStream.FlushAsync(ct).ConfigureAwait(false);

            return tempPath;
        }
    }

    /// <summary>
    /// Cleans up temporary file
    /// </summary>
    private void CleanupTempFile(string? tempFilePath)
    {
        if (string.IsNullOrWhiteSpace(tempFilePath))
        {
            return;
        }

        try
        {
            if (File.Exists(tempFilePath))
            {
                File.Delete(tempFilePath);
                _logger.LogDebug("Deleted temp file: {TempFilePath}", tempFilePath);
            }
        }
        catch (Exception ex)
        {
            // Log but don't fail - temp file cleanup is best-effort
            _logger.LogWarning(ex, "Failed to delete temp file: {TempFilePath}", tempFilePath);
        }
    }
}
