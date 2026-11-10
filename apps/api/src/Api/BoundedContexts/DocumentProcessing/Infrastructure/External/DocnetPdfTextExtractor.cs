using System.Runtime.InteropServices;
using System.Security;
using System.Text;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.Services;
using Docnet.Core;
using Docnet.Core.Models;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Docnet.Core adapter for PDF text extraction. It reads the text layer of the PDF and nothing else: <b>no OCR</b>
/// (#4437). A scanned page comes back empty; what the pipelines do about that is in <c>NearlyEmptyPages</c> and the
/// failure text of a PDF with no text at all. Scans belong to an extractor that OCRs (Unstructured, #4436).
/// Implements thread-safe PDF processing with semaphore-based concurrency control
/// </summary>
internal class DocnetPdfTextExtractor : IPdfTextExtractor
{
    private readonly ILogger<DocnetPdfTextExtractor> _logger;

    // Semaphore to prevent concurrent access to Docnet.Core (not thread-safe)
    // Max 4 concurrent extractions to prevent native library crashes
    private static readonly SemaphoreSlim DocnetSemaphore = new(4, 4);

    public DocnetPdfTextExtractor(ILogger<DocnetPdfTextExtractor> logger)
    {
        _logger = logger;
    }

    public async Task<TextExtractionResult> ExtractTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken cancellationToken = default)
    {
        string? tempFilePath = null;

        try
        {
            // Step 1: Write stream to temp file (Docnet.Core requires file path)
            tempFilePath = await WriteTempFileAsync(pdfStream, cancellationToken).ConfigureAwait(false);

            // Step 2: Extract raw text with Docnet (infrastructure)
            await DocnetSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var (rawText, pageCount) = await Task.Run(() => ExtractRawText(tempFilePath), cancellationToken).ConfigureAwait(false);

                // Step 3: Normalize text (domain service)
                var normalizedText = PdfTextProcessingDomainService.NormalizeText(rawText);

                // Step 4: Assess quality (domain service)
                var quality = PdfTextProcessingDomainService.AssessQuality(normalizedText, pageCount);

                // enableOcrFallback belongs to the interface and is ignored: Docnet never OCRs (#4437).
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
        CancellationToken cancellationToken = default)
    {
        string? tempFilePath = null;

        try
        {
            // Step 1: Write stream to temp file (Docnet.Core requires file path)
            tempFilePath = await WriteTempFileAsync(pdfStream, cancellationToken).ConfigureAwait(false);

            // Step 2: Extract paged text with Docnet (infrastructure)
            await DocnetSemaphore.WaitAsync(cancellationToken).ConfigureAwait(false);
            try
            {
                var pageChunks = await Task.Run(() => ExtractPagedRawText(tempFilePath), cancellationToken).ConfigureAwait(false);

                var totalPages = pageChunks.Count;
                var totalChars = pageChunks.Sum(pc => pc.CharCount);
                var nonEmptyPages = pageChunks.Count(pc => !pc.IsEmpty);

                _logger.LogInformation(
                    "Extracted paged text from PDF. Pages: {TotalPages}, Non-empty: {NonEmpty}, Total chars: {TotalChars}",
                    totalPages, nonEmptyPages, totalChars);

                // No OCR on this path either (#4437): it never had one, and it is the path the pipelines use. The pages that
                // come back without text are reported by the callers (NearlyEmptyPages), not repaired here.
                return PagedTextExtractionResult.CreateSuccess(
                    pageChunks,
                    totalPages,
                    totalChars,
                    ocrTriggered: false) with { TextLayerOnly = true };
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
    private async Task<string> WriteTempFileAsync(Stream pdfStream, CancellationToken cancellationToken)
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
            await pdfStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
            await fileStream.FlushAsync(cancellationToken).ConfigureAwait(false);

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

