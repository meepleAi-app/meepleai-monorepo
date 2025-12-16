using Docnet.Core;
using Docnet.Core.Models;
using System.Drawing;
using System.Drawing.Imaging;
using System.Runtime.ExceptionServices;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security;
using System.Text;
using Tesseract;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// OCR service implementation using Tesseract 5
/// </summary>
[SupportedOSPlatform("windows")]
internal class TesseractOcrAdapter : IOcrService, IDisposable
{
    private readonly ILogger<TesseractOcrAdapter> _logger;
    private readonly string _tessdataPath;
    private readonly string _language;
    private TesseractEngine? _engine;
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    public TesseractOcrAdapter(
        ILogger<TesseractOcrAdapter> logger,
        IConfiguration configuration)
    {
        _logger = logger;

        // Tessdata path - look in application directory
        var appDir = AppDomain.CurrentDomain.BaseDirectory;
        _tessdataPath = Path.Combine(appDir, "tessdata");

        // Language configuration (default: English)
        _language = configuration.GetValue<string>("PdfExtraction:Ocr:DefaultLanguage") ?? "eng";

        // Limit concurrent OCR operations to avoid memory issues
        var maxConcurrent = configuration.GetValue<int>("PdfExtraction:Ocr:MaxConcurrentOperations", 2);
        _semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);

        _logger.LogInformation(
            "TesseractOcrAdapter initialized with tessdata path: {TessdataPath}, language: {Language}",
            _tessdataPath, _language);
    }

    /// <summary>
    /// Gets or creates the Tesseract engine (lazy initialization)
    /// </summary>
    private TesseractEngine GetEngine()
    {
        if (_engine != null)
        {
            return _engine;
        }

        if (!Directory.Exists(_tessdataPath))
        {
            throw new InvalidOperationException(
                $"Tessdata directory not found at: {_tessdataPath}. " +
                "Ensure tessdata files are copied to output directory.");
        }

        var trainedDataFile = Path.Combine(_tessdataPath, $"{_language}.traineddata");
        if (!File.Exists(trainedDataFile))
        {
            throw new InvalidOperationException(
                $"Trained data file not found: {trainedDataFile}. " +
                $"Please ensure {_language}.traineddata exists in tessdata directory.");
        }

        _logger.LogDebug("Creating Tesseract engine with language: {Language}", _language);

        _engine = new TesseractEngine(_tessdataPath, _language, EngineMode.Default);

        return _engine;
    }

    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    public async Task<OcrResult> ExtractTextFromPageAsync(
        string pdfPath,
        int pageIndex,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            return OcrResult.CreateFailure("PDF path is required");
        }

        if (!File.Exists(pdfPath))
        {
            return OcrResult.CreateFailure($"PDF file not found: {pdfPath}");
        }

        await _semaphore.WaitAsync(ct).ConfigureAwait(false);

        try
        {
            var (text, confidence) = await Task.Run(() => ExtractTextFromPageInternal(pdfPath, pageIndex), ct).ConfigureAwait(false);

            _logger.LogInformation(
                "OCR completed for page {PageIndex} of {PdfPath}. Confidence: {Confidence:F2}, Characters: {CharCount}",
                pageIndex, pdfPath, confidence, text.Length);

            return OcrResult.CreateSuccess(text, confidence, pageCount: 1);
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Infrastructure adapter - Tesseract OCR can throw various exceptions
        // from native library; wrap all failures into domain result object
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "OCR failed for page {PageIndex} of {PdfPath}", pageIndex, pdfPath);
            return OcrResult.CreateFailure($"OCR failed: {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<OcrResult> ExtractTextFromPdfAsync(
        string pdfPath,
        CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
        {
            return OcrResult.CreateFailure("PDF path is required");
        }

        if (!File.Exists(pdfPath))
        {
            return OcrResult.CreateFailure($"PDF file not found: {pdfPath}");
        }

        try
        {
            // First, get page count
            int pageCount;
            // NOTE: DocLib.Instance is a singleton - DO NOT dispose it
            var library = DocLib.Instance;
            using (var docReader = library.GetDocReader(pdfPath, new PageDimensions(1080, 1920)))
            {
                pageCount = docReader.GetPageCount();
            }

            _logger.LogInformation("Starting OCR for PDF: {PdfPath}, Pages: {PageCount}", pdfPath, pageCount);

            var textBuilder = new StringBuilder();
            var confidences = new List<float>();

            // Process each page
            for (int i = 0; i < pageCount; i++)
            {
                ct.ThrowIfCancellationRequested();

                var pageResult = await ExtractTextFromPageAsync(pdfPath, i, ct).ConfigureAwait(false);

                if (!pageResult.Success)
                {
                    _logger.LogWarning(
                        "OCR failed for page {PageIndex} of {PdfPath}: {Error}",
                        i, pdfPath, pageResult.ErrorMessage);
                    continue;
                }

                if (!string.IsNullOrWhiteSpace(pageResult.ExtractedText))
                {
                    textBuilder.AppendLine(pageResult.ExtractedText);
                    textBuilder.AppendLine(); // Page separator
                }

                confidences.Add(pageResult.MeanConfidence);
            }

            var extractedText = textBuilder.ToString().Trim();
            var meanConfidence = confidences.Count > 0 ? confidences.Average() : 0f;

            _logger.LogInformation(
                "OCR completed for PDF: {PdfPath}. Pages: {PageCount}, Mean confidence: {Confidence:F2}, Total characters: {CharCount}",
                pdfPath, pageCount, meanConfidence, extractedText.Length);

            return OcrResult.CreateSuccess(extractedText, meanConfidence, pageCount);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "OCR operation cancelled for PDF: {PdfPath}", pdfPath);
            throw;
        }
#pragma warning disable CA1031 // Do not catch general exception types
        // Justification: Infrastructure adapter - Tesseract multi-page OCR can throw various exceptions;
        // wrap all failures into domain result object
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "OCR failed for PDF: {PdfPath}", pdfPath);
            return OcrResult.CreateFailure($"OCR failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Internal method to extract text from a single page using Tesseract
    /// </summary>
    /// <remarks>
    /// SYSLIB0032: HandleProcessCorruptedStateExceptions is obsolete in .NET 9+
    /// Removed as corrupted state exceptions cannot be recovered from anyway.
    /// </remarks>
    [SecurityCritical]
    [System.Runtime.Versioning.SupportedOSPlatform("windows")]
    private (string Text, float Confidence) ExtractTextFromPageInternal(string pdfPath, int pageIndex)
    {
        var engine = GetEngine();

        try
        {
            // Render PDF page to image using Docnet
            // NOTE: DocLib.Instance is a singleton - DO NOT dispose it
            var library = DocLib.Instance;
            using var docReader = library.GetDocReader(pdfPath, new PageDimensions(1080, 1920));

            if (pageIndex >= docReader.GetPageCount())
            {
                throw new ArgumentException($"Page index {pageIndex} is out of range. PDF has {docReader.GetPageCount()} pages.", nameof(pageIndex));
            }

            using var pageReader = docReader.GetPageReader(pageIndex);

            // Get page dimensions
            var width = pageReader.GetPageWidth();
            var height = pageReader.GetPageHeight();

            // Render page to raw bytes
            var rawBytes = pageReader.GetImage();

            // Convert to Pix format for Tesseract
            // Docnet provides BGRA format, we need to convert to compatible format
            using var pix = ConvertToPix(rawBytes, width, height);

            // Perform OCR
            using var page = engine.Process(pix);

            var text = page.GetText();
            var confidence = page.GetMeanConfidence();

            return (text ?? string.Empty, confidence);
        }
        catch (AccessViolationException ex)
        {
            // Native library crash - log and rethrow as managed exception
            _logger.LogError(ex, "Native library crash during PDF processing for page {PageIndex} of {PdfPath}", pageIndex, pdfPath);
            throw new InvalidOperationException($"PDF processing failed due to native library error. The PDF may be corrupted or unsupported.", ex);
        }
        catch (SEHException ex)
        {
            // Structured Exception Handling (SEH) from native code
            _logger.LogError(ex, "Native exception during PDF processing for page {PageIndex} of {PdfPath}", pageIndex, pdfPath);
            throw new InvalidOperationException($"PDF processing failed due to native library error. The PDF may be corrupted or unsupported.", ex);
        }
    }

    /// <summary>
    /// Converts raw BGRA image bytes to Tesseract Pix format
    /// </summary>
    private Pix ConvertToPix(byte[] bgraBytes, int width, int height)
    {
        // Docnet returns BGRA format (4 bytes per pixel)
        // Convert to Bitmap, then to Pix using PixConverter

        // Create Bitmap from BGRA data
        using var bitmap = new Bitmap(width, height, PixelFormat.Format32bppArgb);

        // Lock bitmap for direct memory access
        var bitmapData = bitmap.LockBits(
            new Rectangle(0, 0, width, height),
            ImageLockMode.WriteOnly,
            PixelFormat.Format32bppArgb);

        try
        {
            // Copy BGRA bytes to bitmap
            // Both Docnet BGRA and Bitmap Format32bppArgb use BGRA byte order
            System.Runtime.InteropServices.Marshal.Copy(
                bgraBytes, 0, bitmapData.Scan0, bgraBytes.Length);
        }
        finally
        {
            bitmap.UnlockBits(bitmapData);
        }

        // Convert Bitmap to Pix using Tesseract's PixConverter
        return PixConverter.ToPix(bitmap);
    }

    public void Dispose()
    {
        Dispose(true);
        GC.SuppressFinalize(this);
    }

    protected virtual void Dispose(bool disposing)
    {
        if (_disposed)
        {
            return;
        }

        if (disposing)
        {
            _engine?.Dispose();
            _semaphore?.Dispose();
            _logger.LogDebug("TesseractOcrAdapter disposed");
        }

        _disposed = true;
    }
}
