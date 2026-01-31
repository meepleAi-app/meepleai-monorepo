using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Unstructured library adapter for PDF text extraction (Stage 1 of 3-stage pipeline)
/// Calls Python FastAPI microservice running Unstructured library
/// </summary>
internal class UnstructuredPdfTextExtractor : IPdfTextExtractor
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UnstructuredPdfTextExtractor> _logger;
    private readonly JsonSerializerOptions _jsonOptions;

    public UnstructuredPdfTextExtractor(
        IHttpClientFactory httpClientFactory,
        ILogger<UnstructuredPdfTextExtractor> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        _jsonOptions = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.SnakeCaseLower,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
        };
    }

    public async Task<TextExtractionResult> ExtractTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken cancellationToken = default)
    {
        string? requestId = Guid.NewGuid().ToString("N");
        var client = _httpClientFactory.CreateClient("UnstructuredService");
        var configuredTimeout = client.Timeout;

        try
        {
            _logger.LogInformation(
                "Starting Unstructured extraction (Stage 1). RequestId: {RequestId}",
                requestId);

            // Step 1: Prepare multipart form data
            using var content = PrepareMultipartContent(pdfStream);

            // Step 2: Call Python service
            using var response = await CallUnstructuredServiceAsync(client, content, cancellationToken).ConfigureAwait(false);

            // Step 3: Parse and validate response
            var extractionResponse = await ParseExtractionResponseAsync(response, cancellationToken).ConfigureAwait(false);
            if (extractionResponse == null)
            {
                return TextExtractionResult.CreateFailure("Invalid response from Unstructured service");
            }

            // Step 4: Create result with normalized text and quality assessment
            var result = CreateExtractionResult(extractionResponse, requestId);

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
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            _logger.LogWarning(ex, "Extraction cancelled by user. RequestId: {RequestId}", requestId);
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
#pragma warning disable CA1031
#pragma warning disable S125 // Sections of code should not be commented out
        // INFRASTRUCTURE SERVICE PATTERN: Graceful degradation
        // Catches all Unstructured API failures. Returns error result instead of throwing
        // to allow PDF pipeline orchestrator to fall back to next stage. External service adapter boundary.
#pragma warning restore S125
        catch (Exception ex)
        {
            _logger.LogError(ex,
                "Unexpected error during Unstructured extraction. RequestId: {RequestId}",
                requestId);
            return TextExtractionResult.CreateFailure(
                $"Unexpected error during PDF extraction: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    /// <summary>
    /// Prepares multipart form data content for Unstructured API request.
    /// </summary>
    private static MultipartFormDataContent PrepareMultipartContent(Stream pdfStream)
    {
        var content = new MultipartFormDataContent();
#pragma warning disable CA2000 // Dispose objects before losing scope
#pragma warning disable S125 // Sections of code should not be commented out
        // OWNERSHIP TRANSFER: MultipartFormDataContent takes ownership of added content and disposes them when it is disposed
#pragma warning restore S125
        var streamContent = new StreamContent(pdfStream);
        var strategyContent = new StringContent("fast");
        var languageContent = new StringContent("ita");
#pragma warning restore CA2000

        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(streamContent, "file", "document.pdf");
        content.Add(strategyContent, "strategy");  // Use fast strategy for <2s target
        content.Add(languageContent, "language");

        return content;
    }

    /// <summary>
    /// Calls Unstructured service and validates HTTP response status.
    /// </summary>
    private async Task<HttpResponseMessage> CallUnstructuredServiceAsync(
        HttpClient client,
        MultipartFormDataContent content,
        CancellationToken cancellationToken)
    {
        var response = await client.PostAsync("/api/v1/extract", content, cancellationToken).ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var errorContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            _logger.LogError(
                "Unstructured service returned error: {StatusCode}, Body: {ErrorContent}",
                response.StatusCode,
                errorContent);

            throw new HttpRequestException(
                $"Unstructured extraction failed with status {response.StatusCode}: {errorContent}");
        }

        return response;
    }

    /// <summary>
    /// Parses and deserializes Unstructured service JSON response.
    /// </summary>
    private async Task<UnstructuredExtractionResponse?> ParseExtractionResponseAsync(
        HttpResponseMessage response,
        CancellationToken cancellationToken)
    {
        var jsonContent = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
        var extractionResponse = JsonSerializer.Deserialize<UnstructuredExtractionResponse>(
            jsonContent,
            _jsonOptions);

        if (extractionResponse == null)
        {
            _logger.LogError("Failed to deserialize Unstructured response");
        }

        return extractionResponse;
    }

    /// <summary>
    /// Creates TextExtractionResult from Unstructured response with normalization and quality assessment.
    /// </summary>
    private TextExtractionResult CreateExtractionResult(
        UnstructuredExtractionResponse extractionResponse,
        string requestId)
    {
        // Normalize text using domain service
        var normalizedText = PdfTextProcessingDomainService.NormalizeText(extractionResponse.Text);

        // Map quality score to ExtractionQuality enum
        var quality = MapQualityScore(extractionResponse.QualityScore);

        // Log quality warning if below threshold
        if (extractionResponse.QualityScore < 0.80)
        {
            _logger.LogWarning(
                "Unstructured extraction quality below threshold: {Score:F2} < 0.80. " +
                "Consider fallback to Stage 2 (SmolDocling)",
                extractionResponse.QualityScore);
        }

        // Create success result
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

    public async Task<PagedTextExtractionResult> ExtractPagedTextAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken cancellationToken = default)
    {
        // Note: Unstructured returns semantic chunks, not strict page-by-page text
        // For now, we extract full text and then attempt to map chunks to pages

        var extractionResult = await ExtractTextAsync(pdfStream, enableOcrFallback, cancellationToken).ConfigureAwait(false);

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

