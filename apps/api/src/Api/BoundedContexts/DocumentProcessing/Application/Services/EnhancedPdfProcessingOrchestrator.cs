using System.Diagnostics;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;

namespace Api.BoundedContexts.DocumentProcessing.Application.Services;

/// <summary>
/// Orchestrates 3-stage PDF extraction pipeline with quality-based fallback
/// Stage 1: Unstructured (≥0.80) → Stage 2: SmolDocling (≥0.70) → Stage 3: Docnet (best effort)
/// </summary>
/// <remarks>
/// Issue #949: BGAI-010 - Enhanced PDF Processing Orchestrator
/// Architecture: ADR-003 (3-Stage PDF Pipeline)
/// Success Rate Estimate: 80% Stage 1, 15% Stage 2, 5% Stage 3
/// </remarks>
public class EnhancedPdfProcessingOrchestrator
{
    private readonly IPdfTextExtractor _unstructuredExtractor;
    private readonly IPdfTextExtractor _smolDoclingExtractor;
    private readonly IPdfTextExtractor _docnetExtractor;
    private readonly ILogger<EnhancedPdfProcessingOrchestrator> _logger;
    private readonly IConfiguration _configuration;

    // Quality thresholds from ADR-003
    private const double Stage1QualityThreshold = 0.80; // Unstructured acceptance threshold
    private const double Stage2QualityThreshold = 0.70; // SmolDocling acceptance threshold

    public EnhancedPdfProcessingOrchestrator(
        IPdfTextExtractor unstructuredExtractor,
        IPdfTextExtractor smolDoclingExtractor,
        IPdfTextExtractor docnetExtractor,
        ILogger<EnhancedPdfProcessingOrchestrator> logger,
        IConfiguration configuration)
    {
        _unstructuredExtractor = unstructuredExtractor;
        _smolDoclingExtractor = smolDoclingExtractor;
        _docnetExtractor = docnetExtractor;
        _logger = logger;
        _configuration = configuration;
    }

    /// <summary>
    /// Extracts text from PDF using 3-stage fallback pipeline with quality-based routing
    /// </summary>
    /// <param name="pdfStream">PDF file stream</param>
    /// <param name="enableOcrFallback">Whether to enable OCR fallback in Stage 3</param>
    /// <param name="ct">Cancellation token</param>
    /// <returns>Extraction result with stage metadata</returns>
    public async Task<EnhancedExtractionResult> ExtractTextWithFallbackAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken ct = default)
    {
        var requestId = Guid.NewGuid().ToString();
        var overallStopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "[{RequestId}] Starting 3-stage PDF extraction pipeline",
            requestId);

        // Copy stream to byte array for multiple extraction attempts
        byte[] pdfBytes;
        using (var memoryStream = new MemoryStream())
        {
            await pdfStream.CopyToAsync(memoryStream, ct);
            pdfBytes = memoryStream.ToArray();
        }

        // Stage 1: Unstructured (fastest, high quality target)
        var stage1Result = await TryExtractWithStage(
            1,
            "Unstructured",
            _unstructuredExtractor,
            pdfBytes,
            Stage1QualityThreshold,
            enableOcrFallback,
            requestId,
            ct);

        if (stage1Result != null)
        {
            overallStopwatch.Stop();
            return CreateEnhancedResult(stage1Result, 1, "Unstructured", overallStopwatch.Elapsed, requestId);
        }

        // Stage 2: SmolDocling (VLM-based, complex layouts)
        var stage2Result = await TryExtractWithStage(
            2,
            "SmolDocling",
            _smolDoclingExtractor,
            pdfBytes,
            Stage2QualityThreshold,
            enableOcrFallback,
            requestId,
            ct);

        if (stage2Result != null)
        {
            overallStopwatch.Stop();
            return CreateEnhancedResult(stage2Result, 2, "SmolDocling", overallStopwatch.Elapsed, requestId);
        }

        // Stage 3: Docnet (fallback, best effort)
        _logger.LogWarning(
            "[{RequestId}] Stages 1-2 failed or low quality, using Stage 3 (Docnet) fallback",
            requestId);

        var stage3Stopwatch = Stopwatch.StartNew();
        await using var fallbackStream = new MemoryStream(pdfBytes);
        var stage3Result = await _docnetExtractor.ExtractTextAsync(fallbackStream, enableOcrFallback, ct);
        stage3Stopwatch.Stop();

        _logger.LogInformation(
            "[{RequestId}] Stage 3 (Docnet) completed in {DurationMs}ms - Success={Success}, Quality={Quality}",
            requestId, stage3Stopwatch.Elapsed.TotalMilliseconds, stage3Result.Success, stage3Result.Quality);

        overallStopwatch.Stop();
        return CreateEnhancedResult(stage3Result, 3, "Docnet", overallStopwatch.Elapsed, requestId);
    }

    /// <summary>
    /// Attempts extraction with a specific stage and validates quality threshold
    /// </summary>
    private async Task<TextExtractionResult?> TryExtractWithStage(
        int stageNumber,
        string stageName,
        IPdfTextExtractor extractor,
        byte[] pdfBytes,
        double qualityThreshold,
        bool enableOcrFallback,
        string requestId,
        CancellationToken ct)
    {
        var stageStopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "[{RequestId}] Attempting Stage {Stage} ({StageName}) - Quality threshold: {Threshold:F2}",
            requestId, stageNumber, stageName, qualityThreshold);

        try
        {
            await using var stream = new MemoryStream(pdfBytes);
            var result = await extractor.ExtractTextAsync(stream, enableOcrFallback, ct);
            stageStopwatch.Stop();

            if (!result.Success)
            {
                _logger.LogWarning(
                    "[{RequestId}] Stage {Stage} ({StageName}) failed: {Error}",
                    requestId, stageNumber, stageName, result.ErrorMessage);
                return null;
            }

            // Map quality enum to approximate score for threshold comparison
            var qualityScore = MapQualityToScore(result.Quality);

            if (qualityScore >= qualityThreshold)
            {
                _logger.LogInformation(
                    "[{RequestId}] Stage {Stage} ({StageName}) succeeded in {DurationMs}ms - Quality: {Quality} ({Score:F2} ≥ {Threshold:F2})",
                    requestId, stageNumber, stageName, stageStopwatch.Elapsed.TotalMilliseconds,
                    result.Quality, qualityScore, qualityThreshold);

                return result;
            }

            _logger.LogWarning(
                "[{RequestId}] Stage {Stage} ({StageName}) quality below threshold - Quality: {Quality} ({Score:F2} < {Threshold:F2}), falling back to next stage",
                requestId, stageNumber, stageName, result.Quality, qualityScore, qualityThreshold);

            return null;
        }
        catch (Exception ex)
        {
            stageStopwatch.Stop();
            _logger.LogError(ex,
                "[{RequestId}] Stage {Stage} ({StageName}) threw exception: {Message}",
                requestId, stageNumber, stageName, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Maps ExtractionQuality enum to approximate quality score (0.0-1.0)
    /// </summary>
    /// <remarks>
    /// Thresholds from ADR-003:
    /// - High: ≥0.75 → 0.85
    /// - Medium: 0.60-0.75 → 0.70
    /// - Low: 0.40-0.60 → 0.50
    /// - VeryLow: <0.40 → 0.25
    /// </remarks>
    private static double MapQualityToScore(ExtractionQuality quality)
    {
        return quality switch
        {
            ExtractionQuality.High => 0.85,      // Excellent quality
            ExtractionQuality.Medium => 0.70,    // Good quality
            ExtractionQuality.Low => 0.50,       // Marginal quality
            ExtractionQuality.VeryLow => 0.25,   // Poor quality
            _ => 0.0                             // Unknown
        };
    }

    /// <summary>
    /// Creates enhanced result with orchestration metadata
    /// </summary>
    private EnhancedExtractionResult CreateEnhancedResult(
        TextExtractionResult extractionResult,
        int stageUsed,
        string stageName,
        TimeSpan totalDuration,
        string requestId)
    {
        _logger.LogInformation(
            "[{RequestId}] Pipeline completed - Stage: {Stage} ({StageName}), Total Duration: {DurationMs}ms, Pages: {Pages}, Chars: {Chars}, Quality: {Quality}",
            requestId, stageUsed, stageName, totalDuration.TotalMilliseconds,
            extractionResult.PageCount, extractionResult.CharacterCount, extractionResult.Quality);

        return new EnhancedExtractionResult(
            Success: extractionResult.Success,
            ExtractedText: extractionResult.ExtractedText,
            PageCount: extractionResult.PageCount,
            CharacterCount: extractionResult.CharacterCount,
            OcrTriggered: extractionResult.OcrTriggered,
            Quality: extractionResult.Quality,
            StageUsed: stageUsed,
            StageName: stageName,
            TotalDurationMs: (int)totalDuration.TotalMilliseconds,
            ErrorMessage: extractionResult.ErrorMessage);
    }

    /// <summary>
    /// Extracts text page-by-page using 3-stage fallback pipeline
    /// </summary>
    public async Task<EnhancedPagedExtractionResult> ExtractPagedTextWithFallbackAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken ct = default)
    {
        var requestId = Guid.NewGuid().ToString();
        var overallStopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "[{RequestId}] Starting 3-stage paged PDF extraction pipeline",
            requestId);

        // Copy stream to byte array for multiple extraction attempts
        byte[] pdfBytes;
        using (var memoryStream = new MemoryStream())
        {
            await pdfStream.CopyToAsync(memoryStream, ct);
            pdfBytes = memoryStream.ToArray();
        }

        // Stage 1: Unstructured
        var stage1Result = await TryExtractPagedWithStage(
            1,
            "Unstructured",
            _unstructuredExtractor,
            pdfBytes,
            Stage1QualityThreshold,
            enableOcrFallback,
            requestId,
            ct);

        if (stage1Result != null)
        {
            overallStopwatch.Stop();
            return CreateEnhancedPagedResult(stage1Result, 1, "Unstructured", overallStopwatch.Elapsed, requestId);
        }

        // Stage 2: SmolDocling
        var stage2Result = await TryExtractPagedWithStage(
            2,
            "SmolDocling",
            _smolDoclingExtractor,
            pdfBytes,
            Stage2QualityThreshold,
            enableOcrFallback,
            requestId,
            ct);

        if (stage2Result != null)
        {
            overallStopwatch.Stop();
            return CreateEnhancedPagedResult(stage2Result, 2, "SmolDocling", overallStopwatch.Elapsed, requestId);
        }

        // Stage 3: Docnet (fallback)
        _logger.LogWarning(
            "[{RequestId}] Stages 1-2 failed for paged extraction, using Stage 3 (Docnet) fallback",
            requestId);

        var stage3Stopwatch = Stopwatch.StartNew();
        await using var fallbackStream = new MemoryStream(pdfBytes);
        var stage3Result = await _docnetExtractor.ExtractPagedTextAsync(fallbackStream, enableOcrFallback, ct);
        stage3Stopwatch.Stop();

        _logger.LogInformation(
            "[{RequestId}] Stage 3 (Docnet) paged extraction completed in {DurationMs}ms - Success={Success}, Chunks={Chunks}",
            requestId, stage3Stopwatch.Elapsed.TotalMilliseconds, stage3Result.Success, stage3Result.PageChunks.Count);

        overallStopwatch.Stop();
        return CreateEnhancedPagedResult(stage3Result, 3, "Docnet", overallStopwatch.Elapsed, requestId);
    }

    /// <summary>
    /// Attempts paged extraction with a specific stage
    /// </summary>
    private async Task<PagedTextExtractionResult?> TryExtractPagedWithStage(
        int stageNumber,
        string stageName,
        IPdfTextExtractor extractor,
        byte[] pdfBytes,
        double qualityThreshold,
        bool enableOcrFallback,
        string requestId,
        CancellationToken ct)
    {
        var stageStopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "[{RequestId}] Attempting paged Stage {Stage} ({StageName}) - Quality threshold: {Threshold:F2}",
            requestId, stageNumber, stageName, qualityThreshold);

        try
        {
            await using var stream = new MemoryStream(pdfBytes);
            var result = await extractor.ExtractPagedTextAsync(stream, enableOcrFallback, ct);
            stageStopwatch.Stop();

            if (!result.Success)
            {
                _logger.LogWarning(
                    "[{RequestId}] Paged Stage {Stage} ({StageName}) failed: {Error}",
                    requestId, stageNumber, stageName, result.ErrorMessage);
                return null;
            }

            // Quality assessment for paged extraction (based on chunk count and content)
            var hasContent = result.PageChunks.Any(c => !c.IsEmpty);
            if (!hasContent)
            {
                _logger.LogWarning(
                    "[{RequestId}] Paged Stage {Stage} ({StageName}) produced no content, falling back",
                    requestId, stageNumber, stageName);
                return null;
            }

            // Calculate quality score based on text coverage (similar to PdfQualityValidationDomainService)
            // This ensures paged extraction honors the same quality thresholds as non-paged
            var qualityScore = CalculatePagedQualityScore(result);

            if (qualityScore >= qualityThreshold)
            {
                _logger.LogInformation(
                    "[{RequestId}] Paged Stage {Stage} ({StageName}) succeeded in {DurationMs}ms - Chunks: {Count}, Quality Score: {Score:F2} (≥ {Threshold:F2})",
                    requestId, stageNumber, stageName, stageStopwatch.Elapsed.TotalMilliseconds, 
                    result.PageChunks.Count, qualityScore, qualityThreshold);

                return result;
            }

            _logger.LogWarning(
                "[{RequestId}] Paged Stage {Stage} ({StageName}) quality below threshold - Score: {Score:F2} < {Threshold:F2}, falling back to next stage",
                requestId, stageNumber, stageName, qualityScore, qualityThreshold);

            return null;
        }
        catch (Exception ex)
        {
            stageStopwatch.Stop();
            _logger.LogError(ex,
                "[{RequestId}] Paged Stage {Stage} ({StageName}) threw exception: {Message}",
                requestId, stageNumber, stageName, ex.Message);
            return null;
        }
    }

    /// <summary>
    /// Calculates quality score for paged extraction using linear interpolation
    /// </summary>
    /// <remarks>
    /// Uses same logic as PdfQualityValidationDomainService.CalculateTextCoverage:
    /// - minChars = 500 (default from ADR-003)
    /// - idealChars = 1000 (minChars * 2)
    ///
    /// Scoring (linear interpolation):
    /// - &lt;500 cpp: 0.0 → 0.5 (e.g., 250 cpp → 0.25, 400 cpp → 0.40)
    /// - 500-1000 cpp: 0.5 → 1.0 (e.g., 800 cpp → 0.80 ✅ passes Stage 1, 900 cpp → 0.90)
    /// - ≥1000 cpp: 1.0
    ///
    /// This ensures 800 cpp meets Stage 1 threshold (0.80), matching domain service behavior.
    /// </remarks>
    private static double CalculatePagedQualityScore(PagedTextExtractionResult result)
    {
        if (result.TotalPages == 0)
            return 0.0;

        var charsPerPage = (double)result.TotalCharacters / result.TotalPages;
        const int minChars = 500;    // From ADR-003: MinCharsPerPage default
        const int idealChars = 1000; // minChars * 2

        // Below minimum: linear interpolation from 0.0 to 0.5
        if (charsPerPage < minChars)
        {
            return Math.Min(charsPerPage / minChars * 0.5, 0.5);
        }

        // At or above ideal: maximum score
        if (charsPerPage >= idealChars)
        {
            return 1.0;
        }

        // Between minimum and ideal: linear interpolation from 0.5 to 1.0
        return 0.5 + (charsPerPage - minChars) / (idealChars - minChars) * 0.5;
    }

    /// <summary>
    /// Creates enhanced paged result with orchestration metadata
    /// </summary>
    private EnhancedPagedExtractionResult CreateEnhancedPagedResult(
        PagedTextExtractionResult pagedResult,
        int stageUsed,
        string stageName,
        TimeSpan totalDuration,
        string requestId)
    {
        _logger.LogInformation(
            "[{RequestId}] Paged pipeline completed - Stage: {Stage} ({StageName}), Duration: {DurationMs}ms, Chunks: {Count}",
            requestId, stageUsed, stageName, totalDuration.TotalMilliseconds, pagedResult.PageChunks.Count);

        return new EnhancedPagedExtractionResult(
            Success: pagedResult.Success,
            PageChunks: pagedResult.PageChunks,
            TotalPages: pagedResult.TotalPages,
            TotalCharacters: pagedResult.TotalCharacters,
            OcrTriggered: pagedResult.OcrTriggered,
            StageUsed: stageUsed,
            StageName: stageName,
            TotalDurationMs: (int)totalDuration.TotalMilliseconds,
            ErrorMessage: pagedResult.ErrorMessage);
    }
}

/// <summary>
/// Enhanced extraction result with orchestration metadata
/// </summary>
public record EnhancedExtractionResult(
    bool Success,
    string ExtractedText,
    int PageCount,
    int CharacterCount,
    bool OcrTriggered,
    ExtractionQuality Quality,
    int StageUsed,
    string StageName,
    int TotalDurationMs,
    string? ErrorMessage = null)
{
    /// <summary>
    /// Creates success result from stage extraction
    /// </summary>
    public static EnhancedExtractionResult FromStage(
        TextExtractionResult result,
        int stageUsed,
        string stageName,
        int totalDurationMs)
    {
        return new EnhancedExtractionResult(
            Success: result.Success,
            ExtractedText: result.ExtractedText,
            PageCount: result.PageCount,
            CharacterCount: result.CharacterCount,
            OcrTriggered: result.OcrTriggered,
            Quality: result.Quality,
            StageUsed: stageUsed,
            StageName: stageName,
            TotalDurationMs: totalDurationMs,
            ErrorMessage: result.ErrorMessage);
    }
}

/// <summary>
/// Enhanced paged extraction result with orchestration metadata
/// </summary>
public record EnhancedPagedExtractionResult(
    bool Success,
    List<PageTextChunk> PageChunks,
    int TotalPages,
    int TotalCharacters,
    bool OcrTriggered,
    int StageUsed,
    string StageName,
    int TotalDurationMs,
    string? ErrorMessage = null)
{
    /// <summary>
    /// Creates success result from stage extraction
    /// </summary>
    public static EnhancedPagedExtractionResult FromStage(
        PagedTextExtractionResult result,
        int stageUsed,
        string stageName,
        int totalDurationMs)
    {
        return new EnhancedPagedExtractionResult(
            Success: result.Success,
            PageChunks: result.PageChunks,
            TotalPages: result.TotalPages,
            TotalCharacters: result.TotalCharacters,
            OcrTriggered: result.OcrTriggered,
            StageUsed: stageUsed,
            StageName: stageName,
            TotalDurationMs: totalDurationMs,
            ErrorMessage: result.ErrorMessage);
    }
}