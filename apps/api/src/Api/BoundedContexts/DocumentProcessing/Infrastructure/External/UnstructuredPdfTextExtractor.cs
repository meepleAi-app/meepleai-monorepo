using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
namespace Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

/// <summary>
/// Captures the RAW Unstructured hi_res wire JSON for a PDF — the response body that
/// <see cref="Api.BoundedContexts.DocumentProcessing.Application.Services.ImageRegionExtractor"/>
/// parses for Image/FigureCaption/Table regions. The normal extraction path drops these
/// (empty-text elements) so image-table region grounding (#3435) needs the raw body.
/// </summary>
internal interface IRawHiResExtractor
{
    /// <summary>
    /// Runs a dedicated <c>strategy=hi_res</c> extraction on the long-timeout client and returns
    /// the raw response body (matching the wire shape ImageRegionExtractor expects), or null.
    /// Throws on HTTP failure/timeout — callers (batch runner) handle per-item failures.
    /// </summary>
    Task<string?> ExtractRawHiResAsync(Stream pdfStream, CancellationToken cancellationToken = default);
}

/// <summary>
/// Unstructured library adapter for PDF text extraction (Stage 1 of 3-stage pipeline)
/// Calls Python FastAPI microservice running Unstructured library
/// </summary>
internal class UnstructuredPdfTextExtractor : IPdfTextExtractor, IRawHiResExtractor
{
    /// <summary>Named HttpClient for the slow hi_res region pass (~200s, extended timeout,
    /// no timeout-retry). Registered in DocumentProcessingServiceExtensions (#3435).</summary>
    public const string HiResClientName = "UnstructuredServiceHiRes";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<UnstructuredPdfTextExtractor> _logger;
    private readonly IExtractionStrategySelector? _strategySelector;
    private readonly JsonSerializerOptions _jsonOptions;

    public UnstructuredPdfTextExtractor(
        IHttpClientFactory httpClientFactory,
        ILogger<UnstructuredPdfTextExtractor> logger,
        IExtractionStrategySelector? strategySelector = null)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
        // DC-2 (#3419): optional so DevTools + integration tests can construct without the scoped
        // selector; production DI always injects it. Null → fall back to the historical "fast".
        _strategySelector = strategySelector;
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
    private MultipartFormDataContent PrepareMultipartContent(Stream pdfStream, ExtractionStrategy? strategyOverride = null)
    {
        var content = new MultipartFormDataContent();
#pragma warning disable CA2000 // Dispose objects before losing scope
#pragma warning disable S125 // Sections of code should not be commented out
        // OWNERSHIP TRANSFER: MultipartFormDataContent takes ownership of added content and disposes them when it is disposed
#pragma warning restore S125
        var streamContent = new StreamContent(pdfStream);
        // DC-2 (#3419): route table-heavy PDFs to hi_res per the pipeline's per-request decision on
        // the scoped selector. Null selector (DevTools/integration/fresh ingest) → historical "fast".
        // #3435: strategyOverride forces hi_res for the dedicated region-seed pass regardless of the
        // (inert-on-corpus) selector.
        var strategyContent = new StringContent(
            (strategyOverride ?? _strategySelector?.Current ?? ExtractionStrategy.Fast).ToWireString());
        var languageContent = new StringContent("ita");
#pragma warning restore CA2000

        streamContent.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("application/pdf");
        content.Add(streamContent, "file", "document.pdf");
        content.Add(strategyContent, "strategy");
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
        string requestId = Guid.NewGuid().ToString("N");
        var client = _httpClientFactory.CreateClient("UnstructuredService");
        var configuredTimeout = client.Timeout;

        try
        {
            using var content = PrepareMultipartContent(pdfStream);
            using var response = await CallUnstructuredServiceAsync(client, content, cancellationToken).ConfigureAwait(false);
            var extractionResponse = await ParseExtractionResponseAsync(response, cancellationToken).ConfigureAwait(false);
            if (extractionResponse == null)
            {
                return PagedTextExtractionResult.CreateFailure("Invalid response from Unstructured service");
            }

            var normalizedText = PdfTextProcessingDomainService.NormalizeText(extractionResponse.Text);
            var pageChunks = CreatePageChunksFromText(normalizedText, extractionResponse.PageCount);
            var structuredElements = MapStructuredElements(extractionResponse.Elements);

            return PagedTextExtractionResult.CreateSuccess(
                pageChunks,
                extractionResponse.PageCount,
                normalizedText.Length,
                ocrTriggered: false,
                structuredElements: structuredElements);
        }
        catch (HttpRequestException ex)
        {
            _logger.LogError(ex, "HTTP request to Unstructured service (paged) failed. RequestId: {RequestId}", requestId);
            return PagedTextExtractionResult.CreateFailure($"Failed to connect to Unstructured service: {ex.Message}");
        }
        catch (TaskCanceledException ex) when (ex.CancellationToken == cancellationToken)
        {
            throw;
        }
        catch (TaskCanceledException)
        {
            return PagedTextExtractionResult.CreateFailure($"Unstructured service timeout after {configuredTimeout.TotalSeconds}s");
        }
        catch (JsonException)
        {
            return PagedTextExtractionResult.CreateFailure("Invalid JSON response from Unstructured service");
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error during paged Unstructured extraction. RequestId: {RequestId}", requestId);
            return PagedTextExtractionResult.CreateFailure($"Unexpected error during PDF extraction: {ex.Message}");
        }
#pragma warning restore CA1031
    }

    /// <inheritdoc />
    public async Task<string?> ExtractRawHiResAsync(
        Stream pdfStream,
        CancellationToken cancellationToken = default)
    {
        // Dedicated hi_res pass on the long-timeout client (#3435): forces strategy=hi_res
        // (the selector is inert on the real corpus) and returns the RAW response body —
        // the exact JSON discarded by ParseExtractionResponseAsync — so ImageRegionExtractor
        // can parse Image/FigureCaption/Table regions the normal path drops. Unlike the other methods
        // here it does NOT swallow failures: the batch runner needs to see timeouts/HTTP errors
        // to mark the item and continue (a swallowed "" would be indistinguishable from a
        // genuinely region-free PDF and would wrongly stamp the seed marker).
        var client = _httpClientFactory.CreateClient(HiResClientName);
        using var content = PrepareMultipartContent(pdfStream, ExtractionStrategy.HiRes);
        using var response = await CallUnstructuredServiceAsync(client, content, cancellationToken).ConfigureAwait(false);
        return await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
    }

    /// <summary>
    /// Maps raw Unstructured elements to the published <see cref="ExtractedElement"/> contract,
    /// coalescing null/whitespace categories to "NarrativeText" and skipping empty-text elements.
    /// </summary>
    private static IReadOnlyList<ExtractedElement>? MapStructuredElements(List<UnstructuredElement>? elements)
    {
        if (elements is null || elements.Count == 0)
        {
            return null;
        }

        var mapped = elements
            .Where(e => !string.IsNullOrWhiteSpace(e.Text))
            .Select(e => new ExtractedElement(
                Text: e.Text!,
                PageNumber: e.PageNumber > 0 ? e.PageNumber : 1,
                ElementType: string.IsNullOrWhiteSpace(e.Category) ? "NarrativeText" : e.Category!,
                BoundingBox: e.Bbox is null
                    ? null
                    : new ElementBoundingBox(e.Bbox.X, e.Bbox.Y, e.Bbox.Width, e.Bbox.Height)))
            .ToList();

        return mapped.Count > 0 ? mapped : null;
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
    [property: JsonPropertyName("elements")] List<UnstructuredElement>? Elements,
    [property: JsonPropertyName("quality_score")] double QualityScore,
    [property: JsonPropertyName("page_count")] int PageCount,
    [property: JsonPropertyName("metadata")] UnstructuredMetadata? Metadata);

internal record UnstructuredChunk(
    [property: JsonPropertyName("text")] string Text,
    [property: JsonPropertyName("page_number")] int PageNumber,
    [property: JsonPropertyName("element_type")] string? ElementType,
    [property: JsonPropertyName("metadata")] Dictionary<string, object>? Metadata);

internal record UnstructuredElement(
    [property: JsonPropertyName("text")] string? Text,
    [property: JsonPropertyName("page_number")] int PageNumber,
    [property: JsonPropertyName("category")] string? Category,
    [property: JsonPropertyName("bbox")] UnstructuredBbox? Bbox = null);

/// <summary>SP-B (#3406): normalized [0,1] bounding box emitted by the Python service.</summary>
internal record UnstructuredBbox(
    [property: JsonPropertyName("x")] float X,
    [property: JsonPropertyName("y")] float Y,
    [property: JsonPropertyName("width")] float Width,
    [property: JsonPropertyName("height")] float Height);

internal record UnstructuredMetadata(
    [property: JsonPropertyName("extraction_duration_ms")] int? ExtractionDurationMs,
    [property: JsonPropertyName("strategy_used")] string? StrategyUsed,
    [property: JsonPropertyName("language")] string? Language,
    [property: JsonPropertyName("detected_tables")] int? DetectedTables,
    [property: JsonPropertyName("detected_structures")] List<string>? DetectedStructures,
    [property: JsonPropertyName("quality_breakdown")] Dictionary<string, double>? QualityBreakdown);

