using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;

namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Unstructured library adapter for PDF text extraction (Stage 1 of 3-stage pipeline)
/// Calls Python FastAPI microservice running Unstructured library
/// </summary>
public class UnstructuredPdfTextExtractor : IPdfTextExtractor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UnstructuredPdfTextExtractor> _logger;
    private readonly PdfTextProcessingDomainService _domainService;
    private readonly JsonSerializerOptions _jsonOptions;

    public UnstructuredPdfTextExtractor(
        IHttpClientFactory httpClientFactory,
        ILogger<UnstructuredPdfTextExtractor> logger,
        PdfTextProcessingDomainService domainService)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _domainService = domainService;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<TextExtractionResult> ExtractTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken ct = default)
    {
        string? requestId = Guid.NewGuid().ToString("N");
        var client = _httpClientFactory.CreateClient("UnstructuredService");
        var configuredTimeout = client.Timeout;

        try
        {
            _logger.LogInformation(
                "Starting Unstructured extraction (Stage 1). RequestId: {RequestId}",
                requestId);

            // Step 1: Prepare HTTP client (factory provides configured, pooled instance)

            // Step 2: Prepare multipart form data (CODE-01: Dispose all IDisposable content)
            using var content = new MultipartFormDataContent();
            using var streamContent = new StreamContent(pdfStream);
            using var strategyContent = new StringContent("fast");  // CODE-01: Dispose StringContent
            using var languageContent = new StringContent("ita");   // CODE-01: Dispose StringContent

            streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
            content.Add(streamContent, "file", "document.pdf");
            content.Add(strategyContent, "strategy");  // Use fast strategy for <2s target
            content.Add(languageContent, "language");

            // Step 3: Call Python service (CODE-01: Dispose HttpResponseMessage)
            using var response = await client.PostAsync("/api/v1/extract", content, ct).ConfigureAwait(false);

            // Step 4: Handle errors
            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
                _logger.LogError(
                    "Unstructured service returned error: {StatusCode}, Body: {ErrorContent}",
                    response.StatusCode,
                    errorContent);

                return TextExtractionResult.CreateFailure(
                    $"Unstructured extraction failed with status {response.StatusCode}: {errorContent}");
            }

            // Step 5: Parse response
            var jsonContent = await response.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            var extractionResponse = JsonSerializer.Deserialize<UnstructuredExtractionResponse>(
                jsonContent,
                _jsonOptions);

            if (extractionResponse == null)
            {
                _logger.LogError("Failed to deserialize Unstructured response");
                return TextExtractionResult.CreateFailure("Invalid response from Unstructured service");
            }

            // Step 6: Normalize text using domain service
            var normalizedText = _domainService.NormalizeText(extractionResponse.Text);

            // Step 7: Map quality score to ExtractionQuality enum
            var quality = MapQualityScore(extractionResponse.QualityScore);

            // Step 8: Log quality warning if below threshold
            if (extractionResponse.QualityScore < 0.80)
            {
                _logger.LogWarning(
                    "Unstructured extraction quality below threshold: {Score:F2} < 0.80. " +
                    "Consider fallback to Stage 2 (SmolDocling)",
                    extractionResponse.QualityScore);
            }

            // Step 9: Create success result
            var result = TextExtractionResult.CreateSuccess(
                extractedText: normalizedText,
                pageCount: extractionResponse.PageCount,
                characterCount: normalizedText.Length,
                ocrTriggered: false,  // Unstructured handles OCR internally
                quality: quality);

            _logger.LogInformation(
                "Unstructured extraction completed. RequestId: {RequestId}, Pages: {PageCount}, " +
                "Characters: {CharCount}, Quality: {Quality} (score: {Score:F2}), Duration: {Duration}ms",
                requestId,
                result.PageCount,
                result.CharacterCount,
                result.Quality,
                extractionResponse.QualityScore,
                extractionResponse.Metadata?.ExtractionDurationMs ?? 0);

            return result;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex,
                "HTTP request to Unstructured service failed. RequestId: {RequestId}",
                requestId);
            return TextExtractionResult.CreateFailure(
                $"Failed to connect to Unstructured service: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == ct)
        {
            _logger.LogWarning("Extraction cancelled by user. RequestId: {RequestId}", requestId);
            throw;
        }
        catch (TaskCanceledException ex)
        {
            _logger.LogError(ex,
                "Unstructured service timeout after {Timeout}s. RequestId: {RequestId}",
                configuredTimeout.TotalSeconds,
                requestId);
            return TextExtractionResult.CreateFailure(
                $"Unstructured service timeout after {configuredTimeout.TotalSeconds}s");
        }
        catch (JsonException ex)
        {
            _logger.LogError(ex,
                "Failed to parse Unstructured service response. RequestId: {RequestId}",
                requestId);
            return TextExtractionResult.CreateFailure(
                "Invalid JSON response from Unstructured service");
        }
        catch (Exception ex)
        {
            // ADAPTER BOUNDARY PATTERN: External service adapter - must handle all errors gracefully
            _logger.LogError(ex,
                "Unexpected error during Unstructured extraction. RequestId: {RequestId}",
                requestId);
            return TextExtractionResult.CreateFailure(
                $"Unexpected error during PDF extraction: {ex.Message}");
        }
    }

    public async Task<PagedTextExtractionResult> ExtractPagedTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken ct = default)
    {
        // Note: Unstructured returns semantic chunks, not strict page-by-page text
        // For now, we extract full text and then attempt to map chunks to pages

        var extractionResult = await ExtractTextAsync(pdfStream, enableOcrFallback, ct).ConfigureAwait(false);

        if (!extractionResult.Success)
        {
            return PagedTextExtractionResult.CreateFailure(extractionResult.ErrorMessage ?? "Extraction failed");
        }

        // Create simple page chunks (split by estimated page boundaries)
        var pageChunks = CreatePageChunksFromText(
            extractionResult.ExtractedText,
            extractionResult.PageCount);

        return PagedTextExtractionResult.CreateSuccess(
            pageChunks,
            extractionResult.PageCount,
            extractionResult.CharacterCount,
            extractionResult.OcrTriggered);
    }

    /// <summary>
    /// Map numeric quality score to ExtractionQuality enum
    /// </summary>
    private static ExtractionQuality MapQualityScore(double score)
    {
        return score switch
        {
            >= 0.80 => ExtractionQuality.High,       // ≥0.80: High quality
            >= 0.60 => ExtractionQuality.Medium,     // 0.60-0.79: Medium quality
            >= 0.40 => ExtractionQuality.Low,        // 0.40-0.59: Low quality
            _ => ExtractionQuality.VeryLow          // <0.40: Very low quality
        };
    }

    /// <summary>
    /// Create simple page chunks by splitting text
    /// (Fallback for paged extraction - not ideal but functional for MVP)
    /// </summary>
    private List<PageTextChunk> CreatePageChunksFromText(string fullText, int pageCount)
    {
        if (string.IsNullOrEmpty(fullText))
        {
            return new List<PageTextChunk>();
        }

        if (pageCount <= 0)
        {
            pageCount = 1;
        }

        var pageChunks = new List<PageTextChunk>();
        var charsPerPage = fullText.Length / pageCount;

        for (int i = 0; i < pageCount; i++)
        {
            var startIndex = i * charsPerPage;
            var endIndex = (i == pageCount - 1) ? fullText.Length - 1 : (i + 1) * charsPerPage - 1;

            var pageText = fullText.Substring(startIndex, endIndex - startIndex + 1);

            pageChunks.Add(new PageTextChunk(
                PageNumber: i + 1,  // 1-indexed
                Text: pageText,
                CharStartIndex: startIndex,
                CharEndIndex: endIndex));
        }

        return pageChunks;
    }
}

/// <summary>
/// DTO for Unstructured service response
/// </summary>
internal record UnstructuredExtractionResponse(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("chunks")] List<UnstructuredChunk> Chunks,
    [property: JsonPropertyName("quality_score")] double QualityScore,
    [property: JsonPropertyName("page_count")] int PageCount,
    [property: JsonPropertyName("metadata")] UnstructuredMetadata? Metadata);

internal record UnstructuredChunk(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("page_number")] int PageNumber,
    [property: JsonPropertyName("element_type")] string? ElementType,
    [property: JsonPropertyName("metadata")] Dictionary<string, object>? Metadata);

internal record UnstructuredMetadata(
    [property: JsonPropertyName("extraction_duration_ms")] int? ExtractionDurationMs,
    [property: JsonPropertyName("strategy_used")] string? StrategyUsed,
    [property: JsonPropertyName("language")] string? Language,
    [property: JsonPropertyName("detected_tables")] int? DetectedTables,
    [property: JsonPropertyName("detected_structures")] List<string>? DetectedStructures,
    [property: JsonPropertyName("quality_breakdown")] Dictionary<string, double>? QualityBreakdown);
