using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External.Models;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// HTTP client adapter for SmolDocling VLM PDF extraction service (Stage 2 fallback)
/// Implements IPdfTextExtractor with retry logic, circuit breaker, and comprehensive error handling
/// </summary>
/// <remarks>
/// SmolDocling uses Vision-Language Model for complex layouts (multi-column, tables, equations)
/// Features: Retry logic (3 attempts), circuit breaker, 60s timeout, structured logging
/// References: ADR-003 (3-Stage PDF Pipeline), Issue #947 (BGAI-007)
/// Stage 2: Fallback when Unstructured quality &lt;0.70
/// </remarks>
public class SmolDoclingPdfTextExtractor : IPdfTextExtractor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<SmolDoclingPdfTextExtractor> _logger;
    private readonly PdfTextProcessingDomainService _domainService;
    private readonly IConfiguration _configuration;

    // Default timeout for VLM processing (longer than Unstructured due to GPU inference)
    private const int DefaultTimeoutSeconds = 60;

    public SmolDoclingPdfTextExtractor(
        IHttpClientFactory httpClientFactory,
        ILogger<SmolDoclingPdfTextExtractor> logger,
        PdfTextProcessingDomainService domainService,
        IConfiguration configuration)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _domainService = domainService;
        _configuration = configuration;
    }

    public async Task<TextExtractionResult> ExtractTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var requestId = Guid.NewGuid().ToString();

        _logger.LogInformation(
            "[{RequestId}] Starting SmolDocling VLM extraction (Stage 2 fallback)",
            requestId);

        try
        {
            // Create HTTP client with circuit breaker and retry policies
            var client = _httpClientFactory.CreateClient("SmolDoclingService");

            // Configure timeout
            var timeout = _configuration.GetValue<int>(
                "PdfExtraction:SmolDocling:TimeoutSeconds",
                DefaultTimeoutSeconds);
            client.Timeout = TimeSpan.FromSeconds(timeout);

            // Prepare multipart form data
            using var content = new MultipartFormDataContent();

            // Copy stream to memory stream (needed for retry logic)
            using var memoryStream = new MemoryStream();
            await pdfStream.CopyToAsync(memoryStream, ct);
            memoryStream.Position = 0;

            using var streamContent = new StreamContent(memoryStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

            content.Add(streamContent, "file", "document.pdf");

            // Execute HTTP request (retry + circuit breaker handled by Polly in DI)
            _logger.LogDebug("[{RequestId}] Sending request to SmolDocling service", requestId);

            var response = await client.PostAsync("/api/v1/extract", content, ct);

            // Handle response
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "[{RequestId}] SmolDocling service returned {StatusCode}: {Error}",
                    requestId, response.StatusCode, errorContent);

                return HandleErrorResponse(response.StatusCode, errorContent, requestId);
            }

            // Parse successful response
            var result = await response.Content.ReadFromJsonAsync<SmolDoclingResponse>(
                cancellationToken: ct);

            if (result == null)
            {
                _logger.LogError("[{RequestId}] Failed to parse SmolDocling response", requestId);
                return TextExtractionResult.CreateFailure("Failed to parse service response");
            }

            // Map to domain result
            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "[{RequestId}] SmolDocling extraction completed in {DurationMs}ms - Quality: {Quality}, Pages: {Pages}",
                requestId, duration.TotalMilliseconds, result.QualityScore, result.PageCount);

            // Determine extraction quality enum from score
            var quality = DetermineExtractionQuality(result.QualityScore);

            // Use plain text (not markdown) for consistency with other extractors
            return TextExtractionResult.CreateSuccess(
                extractedText: _domainService.NormalizeText(result.Text),
                pageCount: result.PageCount,
                characterCount: result.Text.Length,
                ocrTriggered: false, // SmolDocling is VLM (vision-based, not OCR)
                quality: quality);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "[{RequestId}] HTTP request failed (SmolDocling service unavailable)",
                requestId);

            return TextExtractionResult.CreateFailure(
                $"SmolDocling service unavailable: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex,
                "[{RequestId}] Request timed out after {Timeout}s",
                requestId, DefaultTimeoutSeconds);

            return TextExtractionResult.CreateFailure(
                $"SmolDocling extraction timeout after {DefaultTimeoutSeconds}s");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == ct)
        {
            _logger.LogWarning(
                "[{RequestId}] Extraction cancelled by user",
                requestId);
            throw; // Propagate user cancellation
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex,
                "[{RequestId}] SmolDocling service timeout",
                requestId);

            return TextExtractionResult.CreateFailure("SmolDocling service timeout");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "[{RequestId}] Failed to parse SmolDocling response",
                requestId);

            return TextExtractionResult.CreateFailure(
                "Invalid response format from SmolDocling service");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "[{RequestId}] Unexpected error during SmolDocling extraction",
                requestId);

            return TextExtractionResult.CreateFailure(
                $"Unexpected error: {ex.Message}");
        }
    }

    public async Task<PagedTextExtractionResult> ExtractPagedTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken ct = default)
    {
        var startTime = DateTime.UtcNow;
        var requestId = Guid.NewGuid().ToString();

        _logger.LogInformation(
            "[{RequestId}] Starting paged SmolDocling extraction",
            requestId);

        try
        {
            var client = _httpClientFactory.CreateClient("SmolDoclingService");

            var timeout = _configuration.GetValue<int>(
                "PdfExtraction:SmolDocling:TimeoutSeconds",
                DefaultTimeoutSeconds);
            client.Timeout = TimeSpan.FromSeconds(timeout);

            using var content = new MultipartFormDataContent();

            using var memoryStream = new MemoryStream();
            await pdfStream.CopyToAsync(memoryStream, ct);
            memoryStream.Position = 0;

            using var streamContent = new StreamContent(memoryStream);
            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");

            content.Add(streamContent, "file", "document.pdf");

            var response = await client.PostAsync("/api/v1/extract", content, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogError(
                    "[{RequestId}] SmolDocling service error: {StatusCode}",
                    requestId, response.StatusCode);

                return PagedTextExtractionResult.CreateFailure(
                    $"Service error: {response.StatusCode}");
            }

            var result = await response.Content.ReadFromJsonAsync<SmolDoclingResponse>(
                cancellationToken: ct);

            if (result == null)
            {
                return PagedTextExtractionResult.CreateFailure("Failed to parse service response");
            }

            // Convert chunks to PageTextChunk format
            var pageChunks = ConvertToPageChunks(result.Chunks);

            var duration = DateTime.UtcNow - startTime;
            _logger.LogInformation(
                "[{RequestId}] Paged extraction completed in {DurationMs}ms - {ChunkCount} chunks",
                requestId, duration.TotalMilliseconds, pageChunks.Count);

            return PagedTextExtractionResult.CreateSuccess(
                pageChunks: pageChunks,
                totalPages: result.PageCount,
                totalCharacters: result.Text.Length,
                ocrTriggered: false);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "[{RequestId}] HTTP request failed", requestId);
            return PagedTextExtractionResult.CreateFailure($"Service unavailable: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.InnerException is TimeoutException)
        {
            _logger.LogError(ex, "[{RequestId}] Request timeout", requestId);
            return PagedTextExtractionResult.CreateFailure("Extraction timeout");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "[{RequestId}] Unexpected error", requestId);
            return PagedTextExtractionResult.CreateFailure($"Unexpected error: {ex.Message}");
        }
    }

    /// <summary>
    /// Converts SmolDocling chunks to PageTextChunk format
    /// </summary>
    private List<PageTextChunk> ConvertToPageChunks(List<SmolDoclingChunk> chunks)
    {
        var pageChunks = new List<PageTextChunk>();
        int charIndex = 0;

        foreach (var chunk in chunks.OrderBy(c => c.PageNumber))
        {
            var normalized = _domainService.NormalizeText(chunk.Text);
            var charCount = normalized.Length;

            pageChunks.Add(new PageTextChunk(
                PageNumber: chunk.PageNumber,
                Text: normalized,
                CharStartIndex: charIndex,
                CharEndIndex: charIndex + charCount
            ));

            charIndex += charCount + 2; // +2 for "\n\n" separator
        }

        return pageChunks;
    }

    /// <summary>
    /// Handles error responses from SmolDocling service
    /// </summary>
    private TextExtractionResult HandleErrorResponse(
        HttpStatusCode statusCode,
        string errorContent,
        string requestId)
    {
        string errorMessage = statusCode switch
        {
            HttpStatusCode.BadRequest => "Invalid PDF file or request parameters",
            HttpStatusCode.RequestEntityTooLarge => "PDF file too large (max 50MB)",
            HttpStatusCode.UnsupportedMediaType => "Unsupported file type (PDF required)",
            HttpStatusCode.UnprocessableEntity => "PDF could not be processed (corrupted or encrypted)",
            HttpStatusCode.ServiceUnavailable => "SmolDocling service temporarily unavailable (circuit breaker may be open)",
            HttpStatusCode.InternalServerError => "Internal service error during extraction",
            _ => $"Service error: {statusCode}"
        };

        // Try to parse structured error response
        try
        {
            var errorResponse = JsonSerializer.Deserialize<SmolDoclingErrorResponse>(errorContent);
            if (errorResponse?.Error != null)
            {
                _logger.LogWarning(
                    "[{RequestId}] Service error details: Code={Code}, Message={Message}",
                    requestId, errorResponse.Error.Code, errorResponse.Error.Message);

                errorMessage = $"{errorResponse.Error.Code}: {errorResponse.Error.Message}";
            }
        }
        catch
        {
            // Ignore parsing errors - use default message
        }

        return TextExtractionResult.CreateFailure(errorMessage);
    }

    /// <summary>
    /// Maps quality score (0.0-1.0) to ExtractionQuality enum
    /// </summary>
    /// <remarks>
    /// SmolDocling quality thresholds (VLM-based):
    /// - High: ≥0.75 (excellent VLM inference and layout understanding)
    /// - Medium: 0.60-0.75 (good quality, acceptable)
    /// - Low: 0.40-0.60 (marginal quality, may need review)
    /// - VeryLow: &lt;0.40 (poor quality, fallback to Docnet required)
    /// </remarks>
    private ExtractionQuality DetermineExtractionQuality(double qualityScore)
    {
        return qualityScore switch
        {
            >= 0.75 => ExtractionQuality.High,
            >= 0.60 => ExtractionQuality.Medium,
            >= 0.40 => ExtractionQuality.Low,
            _ => ExtractionQuality.VeryLow
        };
    }
}
