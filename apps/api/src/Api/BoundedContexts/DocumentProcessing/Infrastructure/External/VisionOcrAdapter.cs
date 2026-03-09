using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.Infrastructure;
using Docnet.Core;
using Docnet.Core.Models;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.Formats.Jpeg;
using SixLabors.ImageSharp.PixelFormats;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Cross-platform OCR service using OpenRouter Vision API.
/// Renders PDF pages to JPEG via Docnet + ImageSharp, then sends to a vision model
/// (e.g. google/gemini-2.0-flash-lite) for text extraction.
/// Replaces TesseractOcrAdapter on Linux/ARM where System.Drawing is unavailable.
/// </summary>
internal sealed class VisionOcrAdapter : IOcrService, IDisposable
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<VisionOcrAdapter> _logger;
    private readonly string _apiKey;
    private readonly string _visionModel;
    private readonly SemaphoreSlim _semaphore;
    private bool _disposed;

    private static readonly Uri OpenRouterChatEndpoint = new("https://openrouter.ai/api/v1/chat/completions");

    private const string OcrPrompt =
        "Extract ALL text from this image verbatim. " +
        "Preserve the original layout, paragraphs, and formatting as closely as possible. " +
        "If the image contains a table, reproduce it in a readable text format. " +
        "Output ONLY the extracted text, no commentary.";

    public VisionOcrAdapter(
        IHttpClientFactory httpClientFactory,
        IConfiguration configuration,
        ILogger<VisionOcrAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;

        _apiKey = SecretsHelper.GetSecretOrValue(
            configuration, "OPENROUTER_API_KEY", logger, required: true)
            ?? throw new InvalidOperationException("OPENROUTER_API_KEY is required for VisionOcrAdapter");

        _visionModel = configuration.GetValue<string>("PdfProcessing:Ocr:VisionModel")
            ?? "google/gemini-2.0-flash-lite";

        var maxConcurrent = configuration.GetValue<int>("PdfProcessing:Ocr:MaxConcurrentOperations", 2);
        _semaphore = new SemaphoreSlim(maxConcurrent, maxConcurrent);

        _logger.LogInformation(
            "VisionOcrAdapter initialized with model: {VisionModel}",
            _visionModel);
    }

    public async Task<OcrResult> ExtractTextFromPageAsync(
        string pdfPath,
        int pageIndex,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            return OcrResult.CreateFailure("PDF path is required");

        if (!File.Exists(pdfPath))
            return OcrResult.CreateFailure($"PDF file not found: {pdfPath}");

        await _semaphore.WaitAsync(cancellationToken).ConfigureAwait(false);

        try
        {
            var base64Jpeg = RenderPageToJpegBase64(pdfPath, pageIndex);
            var text = await CallVisionApiAsync(base64Jpeg, cancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "Vision OCR completed for page {PageIndex} of {PdfPath}. Characters: {CharCount}",
                pageIndex, pdfPath, text.Length);

            // Vision API doesn't return a numeric confidence; use 0.85 as a reasonable default
            return OcrResult.CreateSuccess(text, confidence: 0.85f, pageCount: 1);
        }
#pragma warning disable CA1031
        // ADAPTER PATTERN: Vision API can fail for various reasons (network, model, image quality).
        // Wrap all failures into domain result object.
        catch (Exception ex) when (ex is not OperationCanceledException)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Vision OCR failed for page {PageIndex} of {PdfPath}", pageIndex, pdfPath);
            return OcrResult.CreateFailure($"Vision OCR failed: {ex.Message}");
        }
        finally
        {
            _semaphore.Release();
        }
    }

    public async Task<OcrResult> ExtractTextFromPdfAsync(
        string pdfPath,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(pdfPath))
            return OcrResult.CreateFailure("PDF path is required");

        if (!File.Exists(pdfPath))
            return OcrResult.CreateFailure($"PDF file not found: {pdfPath}");

        try
        {
            var pageCount = GetPdfPageCount(pdfPath);

            _logger.LogInformation("Starting Vision OCR for PDF: {PdfPath}, Pages: {PageCount}", pdfPath, pageCount);

            var textBuilder = new StringBuilder();
            var confidences = new List<float>();

            for (int i = 0; i < pageCount; i++)
            {
                cancellationToken.ThrowIfCancellationRequested();

                var pageResult = await ExtractTextFromPageAsync(pdfPath, i, cancellationToken).ConfigureAwait(false);

                if (!pageResult.Success)
                {
                    _logger.LogWarning(
                        "Vision OCR failed for page {PageIndex} of {PdfPath}: {Error}",
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
                "Vision OCR completed for PDF: {PdfPath}. Pages: {PageCount}, Mean confidence: {Confidence:F2}, Total characters: {CharCount}",
                pdfPath, pageCount, meanConfidence, extractedText.Length);

            return OcrResult.CreateSuccess(extractedText, meanConfidence, pageCount);
        }
        catch (OperationCanceledException ex)
        {
            _logger.LogWarning(ex, "Vision OCR operation cancelled for PDF: {PdfPath}", pdfPath);
            throw;
        }
#pragma warning disable CA1031
        // ADAPTER PATTERN: Wrap all failures for multi-page PDF OCR.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            _logger.LogError(ex, "Vision OCR failed for PDF: {PdfPath}", pdfPath);
            return OcrResult.CreateFailure($"Vision OCR failed: {ex.Message}");
        }
    }

    /// <summary>
    /// Renders a single PDF page to a JPEG base64 string using Docnet + ImageSharp.
    /// Cross-platform: no System.Drawing dependency.
    /// </summary>
    private static string RenderPageToJpegBase64(string pdfPath, int pageIndex)
    {
        // NOTE: DocLib.Instance is a singleton - DO NOT dispose it
        var library = DocLib.Instance;
        using var docReader = library.GetDocReader(pdfPath, new PageDimensions(1080, 1920));

        if (pageIndex >= docReader.GetPageCount())
        {
            throw new ArgumentException(
                $"Page index {pageIndex} is out of range. PDF has {docReader.GetPageCount()} pages.",
                nameof(pageIndex));
        }

        using var pageReader = docReader.GetPageReader(pageIndex);
        var width = pageReader.GetPageWidth();
        var height = pageReader.GetPageHeight();
        var rawBytes = pageReader.GetImage(); // BGRA format

        // Convert BGRA raw bytes → JPEG using ImageSharp (cross-platform)
        using var image = Image.LoadPixelData<Bgra32>(rawBytes, width, height);
        using var ms = new MemoryStream();
        image.Save(ms, new JpegEncoder { Quality = 80 });

        return Convert.ToBase64String(ms.ToArray());
    }

    private static int GetPdfPageCount(string pdfPath)
    {
        var library = DocLib.Instance;
        using var docReader = library.GetDocReader(pdfPath, new PageDimensions(1080, 1920));
        return docReader.GetPageCount();
    }

    /// <summary>
    /// Calls OpenRouter Vision API with a base64-encoded image.
    /// </summary>
    private async Task<string> CallVisionApiAsync(string base64Jpeg, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient("OpenRouter");

        var requestBody = new
        {
            model = _visionModel,
            messages = new[]
            {
                new
                {
                    role = "user",
                    content = new object[]
                    {
                        new { type = "text", text = OcrPrompt },
                        new
                        {
                            type = "image_url",
                            image_url = new { url = $"data:image/jpeg;base64,{base64Jpeg}" }
                        }
                    }
                }
            },
            max_tokens = 4096,
            temperature = 0.0
        };

        var json = JsonSerializer.Serialize(requestBody, VisionJsonContext.Default.Options);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");

        using var request = new HttpRequestMessage(HttpMethod.Post, OpenRouterChatEndpoint)
        {
            Content = content
        };
        request.Headers.Add("Authorization", $"Bearer {_apiKey}");

        using var response = await client.SendAsync(request, cancellationToken).ConfigureAwait(false);
        response.EnsureSuccessStatusCode();

        var responseJson = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        using var doc = JsonDocument.Parse(responseJson);

        var choices = doc.RootElement.GetProperty("choices");
        if (choices.GetArrayLength() == 0)
        {
            throw new InvalidOperationException("Vision API returned no choices");
        }

        var messageContent = choices[0].GetProperty("message").GetProperty("content").GetString();
        return messageContent ?? string.Empty;
    }

    public void Dispose()
    {
        if (!_disposed)
        {
            _semaphore.Dispose();
            _disposed = true;
        }
    }
}

/// <summary>
/// JSON serialization context for Vision API requests (source-gen ready).
/// </summary>
[JsonSerializable(typeof(object))]
internal partial class VisionJsonContext : JsonSerializerContext
{
}
