using System.Diagnostics;
using Api.BoundedContexts.DocumentProcessing.Domain.Services;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Configuration;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.DependencyInjection;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.External;
using Api.BoundedContexts.DocumentProcessing.Infrastructure.Helpers;
using Api.Observability;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;

#pragma warning disable MA0048 // File name must match type name - Contains Interface with supporting types
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
internal class EnhancedPdfProcessingOrchestrator
{
    private readonly IPdfTextExtractor _unstructuredExtractor;
    private readonly IPdfTextExtractor _smolDoclingExtractor;
    private readonly IPdfTextExtractor _docnetExtractor;
    private readonly ILogger<EnhancedPdfProcessingOrchestrator> _logger;
    private readonly IConfiguration _configuration;
    private readonly PdfProcessingOptions _options;

    // Quality thresholds from ADR-003
    private const double Stage1QualityThreshold = 0.80; // Unstructured acceptance threshold
    private const double Stage2QualityThreshold = 0.70; // SmolDocling acceptance threshold
    /// <summary>
    /// ISSUE-1174: Constructor uses keyed services to resolve stage extractors.
    /// This prevents circular DI dependency where IPdfTextExtractor would otherwise
    /// resolve to OrchestratedPdfTextExtractor → creating a circular chain.
    /// POST-MERGE: Uses PdfExtractorKeys constants for compile-time safety.
    /// </summary>
    public EnhancedPdfProcessingOrchestrator(
        [FromKeyedServices(DocumentProcessingServiceExtensions.PdfExtractorKeys.Unstructured)] IPdfTextExtractor unstructuredExtractor,
        [FromKeyedServices(DocumentProcessingServiceExtensions.PdfExtractorKeys.SmolDocling)] IPdfTextExtractor smolDoclingExtractor,
        [FromKeyedServices(DocumentProcessingServiceExtensions.PdfExtractorKeys.Docnet)] IPdfTextExtractor docnetExtractor,
        ILogger<EnhancedPdfProcessingOrchestrator> logger,
        IConfiguration configuration,
        IOptions<PdfProcessingOptions> options)
    {
        _unstructuredExtractor = unstructuredExtractor;
        _smolDoclingExtractor = smolDoclingExtractor;
        _docnetExtractor = docnetExtractor;
        _logger = logger;
        _configuration = configuration;
        _options = options.Value;
    }
    /// <summary>
    /// BGAI-087: Loads PDF data using size-based strategy (memory vs temp file)
    /// </summary>
    private async Task<PdfDataHandle> LoadPdfBytesAsync(
        Stream pdfStream,
        string requestId,
        CancellationToken cancellationToken)
    {
        var threshold = _options.LargePdfThresholdBytes;
        var useTempFile = _options.UseTempFileForLargePdfs;
        var pdfSize = pdfStream.CanSeek ? pdfStream.Length : -1;

        // Large PDF + temp file enabled: Use temp file
        if (useTempFile && pdfStream.CanSeek && pdfSize >= threshold)
        {
            _logger.LogInformation(
                "[{RequestId}] PDF size {Size}MB ≥ threshold {Threshold}MB - using temp file strategy",
                requestId, pdfSize / 1_000_000.0, threshold / 1_000_000.0);

            // S5445 fix: Use secure temp file creation instead of Path.GetTempFileName()
            var tempFile = SecureTempFileHelper.CreateSecureTempFilePath(".pdf");
            try
            {
                var fileStream = new FileStream(
                    tempFile,
                    FileMode.Create,
                    FileAccess.Write,
                    FileShare.None,
                    bufferSize: 81920,
                    useAsync: true);
                await using (fileStream.ConfigureAwait(false))
                {
                    await pdfStream.CopyToAsync(fileStream, cancellationToken).ConfigureAwait(false);
                    return PdfDataHandle.FromTempFile(tempFile);
                }
            }
            catch
            {
                // Cleanup temp file on error
                SecureTempFileHelper.CleanupTempFile(tempFile);
                throw;
            }
        }

        // Small PDF or temp file disabled: Use memory
        _logger.LogDebug(
            "[{RequestId}] PDF size {Size}MB < threshold {Threshold}MB - using in-memory strategy",
            requestId, pdfSize / 1_000_000.0, threshold / 1_000_000.0);

        using var memoryStream = new MemoryStream();
        await pdfStream.CopyToAsync(memoryStream, cancellationToken).ConfigureAwait(false);
        return PdfDataHandle.FromBytes(memoryStream.ToArray());
    }
    /// <summary>
    /// Extracts text from PDF using 3-stage fallback pipeline with quality-based routing
    /// </summary>
    /// <param name="pdfStream">PDF file stream</param>
    /// <param name="enableOcrFallback">Whether to enable OCR fallback in Stage 3</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Extraction result with stage metadata</returns>
    public async Task<EnhancedExtractionResult> ExtractTextWithFallbackAsync(
        Stream pdfStream,
        bool enableOcrFallback = true,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);
        var requestId = Guid.NewGuid().ToString();
        var overallStopwatch = Stopwatch.StartNew();

        // Defense in depth: Validate file size before processing
        if (!ValidateFileSize(pdfStream, requestId, out var sizeError))
        {
            return EnhancedExtractionResult.CreateFailure(sizeError!);
        }

        _logger.LogInformation(
            "[{RequestId}] Starting 3-stage PDF extraction pipeline (size: {Size} bytes)",
            requestId, pdfStream.CanSeek ? pdfStream.Length : -1);

        // BGAI-087: Load PDF with size-based strategy (memory vs temp file)
        using var pdfData = await LoadPdfBytesAsync(pdfStream, requestId, cancellationToken).ConfigureAwait(false);

        try
        {
            // Try Stages 1-2 with quality thresholds
            var result = await TryStages1And2Async(pdfData, enableOcrFallback, requestId, cancellationToken).ConfigureAwait(false);
            if (result != null)
            {
                overallStopwatch.Stop();
                return result;
            }

            // Stage 3: Docnet (fallback, best effort)
            _logger.LogWarning(
                "[{RequestId}] Stages 1-2 failed or low quality, using Stage 3 (Docnet) fallback",
                requestId);

            overallStopwatch.Stop();
            return await ExecuteStage3FallbackAsync(pdfData, enableOcrFallback, overallStopwatch.Elapsed, requestId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // BGAI-087: Ensure temp file cleanup via Dispose
        }
    }

    /// <summary>
    /// Tries extraction with Stages 1 and 2, returns result if successful
    /// </summary>
    private async Task<EnhancedExtractionResult?> TryStages1And2Async(
        PdfDataHandle pdfData,
        bool enableOcrFallback,
        string requestId,
        CancellationToken cancellationToken)
    {
        // Stage 1: Unstructured
        var stage1Result = await TryExtractWithStage(
            1, "Unstructured", _unstructuredExtractor, pdfData,
            Stage1QualityThreshold, enableOcrFallback, requestId, cancellationToken).ConfigureAwait(false);

        if (stage1Result != null)
            return CreateEnhancedResult(stage1Result, 1, "Unstructured", TimeSpan.Zero, requestId);

        // Stage 2: SmolDocling
        var stage2Result = await TryExtractWithStage(
            2, "SmolDocling", _smolDoclingExtractor, pdfData,
            Stage2QualityThreshold, enableOcrFallback, requestId, cancellationToken).ConfigureAwait(false);

        return stage2Result != null
            ? CreateEnhancedResult(stage2Result, 2, "SmolDocling", TimeSpan.Zero, requestId)
            : null;
    }

    /// <summary>
    /// Tries paged extraction with Stages 1 and 2, returns result if successful
    /// </summary>
    private async Task<EnhancedPagedExtractionResult?> TryStages1And2PagedAsync(
        PdfDataHandle pdfData,
        bool enableOcrFallback,
        string requestId,
        CancellationToken cancellationToken)
    {
        // Stage 1: Unstructured
        var stage1Result = await TryExtractPagedWithStage(
            1, "Unstructured", _unstructuredExtractor, pdfData,
            Stage1QualityThreshold, enableOcrFallback, requestId, cancellationToken).ConfigureAwait(false);

        if (stage1Result != null)
            return CreateEnhancedPagedResult(stage1Result, 1, "Unstructured", TimeSpan.Zero, requestId);

        // Stage 2: SmolDocling
        var stage2Result = await TryExtractPagedWithStage(
            2, "SmolDocling", _smolDoclingExtractor, pdfData,
            Stage2QualityThreshold, enableOcrFallback, requestId, cancellationToken).ConfigureAwait(false);

        return stage2Result != null
            ? CreateEnhancedPagedResult(stage2Result, 2, "SmolDocling", TimeSpan.Zero, requestId)
            : null;
    }

    /// <summary>
    /// Attempts extraction with a specific stage and validates quality threshold
    /// </summary>
    private async Task<TextExtractionResult?> TryExtractWithStage(
        int stageNumber,
        string stageName,
        IPdfTextExtractor extractor,
        PdfDataHandle pdfData,
        double qualityThreshold,
        bool enableOcrFallback,
        string requestId,
        CancellationToken cancellationToken)
    {
        var stageStopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "[{RequestId}] Attempting Stage {Stage} ({StageName}) - Quality threshold: {Threshold:F2}",
            requestId, stageNumber, stageName, qualityThreshold);

        try
        {
            var stream = pdfData.GetStream();
            await using (stream.ConfigureAwait(false))
            {
                var result = await extractor.ExtractTextAsync(stream, enableOcrFallback, cancellationToken).ConfigureAwait(false);
                stageStopwatch.Stop();

                return EvaluateStageResult(result, stageNumber, stageName, qualityThreshold, requestId, stageStopwatch.Elapsed);
            }
        }
        catch (Exception ex)
        {
            stageStopwatch.Stop();
            _logger.LogError(ex,
                "[{RequestId}] Stage {Stage} ({StageName}) threw exception: {Message}",
                requestId, stageNumber, stageName, ex.Message);

            // BGAI-043: Record failed stage extraction (exception)
            RecordStageMetricSafely(stageName, false, stageStopwatch.Elapsed.TotalMilliseconds, null);

            return null;
        }
    }

    private TextExtractionResult? EvaluateStageResult(
        TextExtractionResult result,
        int stageNumber,
        string stageName,
        double qualityThreshold,
        string requestId,
        TimeSpan elapsed)
    {
        if (!result.Success)
        {
            _logger.LogWarning(
                "[{RequestId}] Stage {Stage} ({StageName}) failed: {Error}",
                requestId, stageNumber, stageName, result.ErrorMessage);

            // BGAI-043: Record failed stage extraction
            RecordStageMetricSafely(stageName, false, elapsed.TotalMilliseconds, null);

            return null;
        }

        // Map quality enum to approximate score for threshold comparison
        var qualityScore = MapQualityToScore(result.Quality);

        if (qualityScore >= qualityThreshold)
        {
            _logger.LogInformation(
                "[{RequestId}] Stage {Stage} ({StageName}) succeeded in {DurationMs}ms - Quality: {Quality} ({Score:F2} ≥ {Threshold:F2})",
                requestId, stageNumber, stageName, elapsed.TotalMilliseconds,
                result.Quality, qualityScore, qualityThreshold);

            // BGAI-043: Record successful stage extraction with quality score
            RecordStageMetricSafely(stageName, true, elapsed.TotalMilliseconds, qualityScore);

            return result;
        }

        _logger.LogWarning(
            "[{RequestId}] Stage {Stage} ({StageName}) quality below threshold - Quality: {Quality} ({Score:F2} < {Threshold:F2}), falling back to next stage",
            requestId, stageNumber, stageName, result.Quality, qualityScore, qualityThreshold);

        // BGAI-043: Record failed stage extraction (quality too low)
        RecordStageMetricSafely(stageName, false, elapsed.TotalMilliseconds, qualityScore);

        return null;
    }

    /// <summary>
    /// Executes Stage 3 (Docnet) fallback for text extraction
    /// </summary>
    private async Task<EnhancedExtractionResult> ExecuteStage3FallbackAsync(
        PdfDataHandle pdfData,
        bool enableOcrFallback,
        TimeSpan totalDuration,
        string requestId,
        CancellationToken cancellationToken)
    {
        var stage3Stopwatch = Stopwatch.StartNew();
        var fallbackStream = pdfData.GetStream();
        await using (fallbackStream.ConfigureAwait(false))
        {
            var stage3Result = await _docnetExtractor.ExtractTextAsync(fallbackStream, enableOcrFallback, cancellationToken).ConfigureAwait(false);
            stage3Stopwatch.Stop();

            _logger.LogInformation(
                "[{RequestId}] Stage 3 (Docnet) completed in {DurationMs}ms - Success={Success}, Quality={Quality}",
                requestId, stage3Stopwatch.Elapsed.TotalMilliseconds, stage3Result.Success, stage3Result.Quality);

            return CreateEnhancedResult(stage3Result, 3, "Docnet", totalDuration, requestId);
        }
    }

    /// <summary>
    /// Executes Stage 3 (Docnet) fallback for paged text extraction
    /// </summary>
    private async Task<EnhancedPagedExtractionResult> ExecutePagedStage3FallbackAsync(
        PdfDataHandle pdfData,
        bool enableOcrFallback,
        TimeSpan totalDuration,
        string requestId,
        CancellationToken cancellationToken)
    {
        var stage3Stopwatch = Stopwatch.StartNew();
        var fallbackStream = pdfData.GetStream();
        await using (fallbackStream.ConfigureAwait(false))
        {
            var stage3Result = await _docnetExtractor.ExtractPagedTextAsync(fallbackStream, enableOcrFallback, cancellationToken).ConfigureAwait(false);
            stage3Stopwatch.Stop();

            _logger.LogInformation(
                "[{RequestId}] Stage 3 (Docnet) paged extraction completed in {DurationMs}ms - Success={Success}, Chunks={Chunks}",
                requestId, stage3Stopwatch.Elapsed.TotalMilliseconds, stage3Result.Success, stage3Result.PageChunks.Count);

            return CreateEnhancedPagedResult(stage3Result, 3, "Docnet", totalDuration, requestId);
        }
    }

    /// <summary>
    /// Creates paged validation failure result
    /// </summary>
    private static EnhancedPagedExtractionResult CreatePagedValidationFailureResult(string errorMessage, TimeSpan duration)
    {
        return new EnhancedPagedExtractionResult(
            Success: false,
            PageChunks: (IList<PageTextChunk>)new List<PageTextChunk>(),
            TotalPages: 0,
            TotalCharacters: 0,
            OcrTriggered: false,
            StageUsed: 0,
            StageName: "Validation",
            TotalDurationMs: (int)duration.TotalMilliseconds,
            ErrorMessage: errorMessage);
    }

    /// <summary>
    /// Validates PDF file size against configured maximum
    /// </summary>
    private bool ValidateFileSize(Stream pdfStream, string requestId, out string? errorMessage)
    {
        var maxSize = _configuration.GetValue<long>(
            "PdfProcessing:MaxFileSizeBytes",
            104857600); // 100 MB default

        if (pdfStream.CanSeek && pdfStream.Length > maxSize)
        {
            _logger.LogWarning(
                "[{RequestId}] PDF size {Size} bytes exceeds maximum {Max} bytes - rejecting",
                requestId, pdfStream.Length, maxSize);

            errorMessage = $"PDF size ({pdfStream.Length / 1_000_000:F1} MB) exceeds maximum allowed ({maxSize / 1_000_000} MB)";
            return false;
        }

        errorMessage = null;
        return true;
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
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(pdfStream);
        var requestId = Guid.NewGuid().ToString();
        var overallStopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "[{RequestId}] Starting 3-stage paged PDF extraction pipeline",
            requestId);

        // ISSUE-1160: Defense in depth - Validate file size before processing
        if (!ValidateFileSize(pdfStream, requestId, out var sizeError))
        {
            overallStopwatch.Stop();
            return CreatePagedValidationFailureResult(sizeError!, overallStopwatch.Elapsed);
        }

        // BGAI-087: Load PDF with size-based strategy (memory vs temp file)
        using var pdfData = await LoadPdfBytesAsync(pdfStream, requestId, cancellationToken).ConfigureAwait(false);

        try
        {
            // Try Stages 1-2 with quality thresholds
            var result = await TryStages1And2PagedAsync(pdfData, enableOcrFallback, requestId, cancellationToken).ConfigureAwait(false);
            if (result != null)
            {
                overallStopwatch.Stop();
                return result;
            }

            // Stage 3: Docnet (fallback)
            _logger.LogWarning(
                "[{RequestId}] Stages 1-2 failed for paged extraction, using Stage 3 (Docnet) fallback",
                requestId);

            overallStopwatch.Stop();
            return await ExecutePagedStage3FallbackAsync(pdfData, enableOcrFallback, overallStopwatch.Elapsed, requestId, cancellationToken).ConfigureAwait(false);
        }
        finally
        {
            // BGAI-087: Ensure temp file cleanup via Dispose
        }
    }

    /// <summary>
    /// Attempts paged extraction with a specific stage
    /// </summary>
    private async Task<PagedTextExtractionResult?> TryExtractPagedWithStage(
        int stageNumber,
        string stageName,
        IPdfTextExtractor extractor,
        PdfDataHandle pdfData,
        double qualityThreshold,
        bool enableOcrFallback,
        string requestId,
        CancellationToken cancellationToken)
    {
        var stageStopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "[{RequestId}] Attempting paged Stage {Stage} ({StageName}) - Quality threshold: {Threshold:F2}",
            requestId, stageNumber, stageName, qualityThreshold);

        try
        {
            var stream = pdfData.GetStream();
            await using (stream.ConfigureAwait(false))
            {
                var result = await extractor.ExtractPagedTextAsync(stream, enableOcrFallback, cancellationToken).ConfigureAwait(false);
                stageStopwatch.Stop();

                return EvaluatePagedStageResult(result, stageNumber, stageName, qualityThreshold, requestId, stageStopwatch.Elapsed);
            }
        }
        catch (Exception ex)
        {
            stageStopwatch.Stop();
            _logger.LogError(ex,
                "[{RequestId}] Paged Stage {Stage} ({StageName}) threw exception: {Message}",
                requestId, stageNumber, stageName, ex.Message);

            // BGAI-043: Record failed paged stage extraction (exception)
            RecordStageMetricSafely(stageName, false, stageStopwatch.Elapsed.TotalMilliseconds, null);

            return null;
        }
    }

    private PagedTextExtractionResult? EvaluatePagedStageResult(
        PagedTextExtractionResult result,
        int stageNumber,
        string stageName,
        double qualityThreshold,
        string requestId,
        TimeSpan elapsed)
    {
        if (!result.Success)
        {
            _logger.LogWarning(
                "[{RequestId}] Paged Stage {Stage} ({StageName}) failed: {Error}",
                requestId, stageNumber, stageName, result.ErrorMessage);

            // BGAI-043: Record failed paged stage extraction
            RecordStageMetricSafely(stageName, false, elapsed.TotalMilliseconds, null);

            return null;
        }

        // Quality assessment for paged extraction (based on chunk count and content)
        var hasContent = result.PageChunks.Any(c => !c.IsEmpty);
        if (!hasContent)
        {
            _logger.LogWarning(
                "[{RequestId}] Paged Stage {Stage} ({StageName}) produced no content, falling back",
                requestId, stageNumber, stageName);

            // BGAI-043: Record failed paged stage extraction (no content)
            RecordStageMetricSafely(stageName, false, elapsed.TotalMilliseconds, 0.0);

            return null;
        }

        // Calculate quality score based on text coverage (similar to PdfQualityValidationDomainService)
        // This ensures paged extraction honors the same quality thresholds as non-paged
        var qualityScore = CalculatePagedQualityScore(result);

        if (qualityScore >= qualityThreshold)
        {
            _logger.LogInformation(
                "[{RequestId}] Paged Stage {Stage} ({StageName}) succeeded in {DurationMs}ms - Chunks: {Count}, Quality Score: {Score:F2} (≥ {Threshold:F2})",
                requestId, stageNumber, stageName, elapsed.TotalMilliseconds,
                result.PageChunks.Count, qualityScore, qualityThreshold);

            // BGAI-043: Record successful paged stage extraction with quality score
            RecordStageMetricSafely(stageName, true, elapsed.TotalMilliseconds, qualityScore);

            return result;
        }

        _logger.LogWarning(
            "[{RequestId}] Paged Stage {Stage} ({StageName}) quality below threshold - Score: {Score:F2} < {Threshold:F2}, falling back to next stage",
            requestId, stageNumber, stageName, qualityScore, qualityThreshold);

        // BGAI-043: Record failed paged stage extraction (quality too low)
        RecordStageMetricSafely(stageName, false, elapsed.TotalMilliseconds, qualityScore);

        return null;
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

    /// <summary>
    /// BGAI-043: Records PDF extraction stage metrics in fire-and-forget pattern
    /// </summary>
    private void RecordStageMetricSafely(string stageName, bool success, double durationMs, double? qualityScore)
    {
        _ = Task.Run(() =>
        {
            try
            {
                MeepleAiMetrics.RecordPdfExtractionStage(stageName, success, durationMs, qualityScore);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Failed to record PDF extraction stage metric for stage {Stage}", stageName);
            }
        });
    }
    /// <summary>
    /// BGAI-087: Handles PDF data with automatic cleanup for temp files
    /// Supports both in-memory (byte[]) and temp file storage strategies
    /// </summary>
    private sealed class PdfDataHandle : IDisposable
    {
        private byte[]? _bytes;
        private string? _tempFilePath;

        private PdfDataHandle() { }

        public static PdfDataHandle FromBytes(byte[] bytes)
        {
            return new PdfDataHandle { _bytes = bytes };
        }

        public static PdfDataHandle FromTempFile(string tempFilePath)
        {
            return new PdfDataHandle { _tempFilePath = tempFilePath };
        }

        /// <summary>
        /// Gets a new stream for PDF data (caller must dispose)
        /// </summary>
        public Stream GetStream()
        {
            if (_bytes != null)
            {
                return new MemoryStream(_bytes);
            }

            if (_tempFilePath != null)
            {
                return new FileStream(
                    _tempFilePath,
                    FileMode.Open,
                    FileAccess.Read,
                    FileShare.Read,
                    bufferSize: 81920,
                    useAsync: true);
            }

            throw new InvalidOperationException("PdfDataHandle not initialized");
        }

        public void Dispose()
        {
            if (_tempFilePath != null && File.Exists(_tempFilePath))
            {
                try
                {
                    File.Delete(_tempFilePath);
                }
                catch
                {
                    // Best effort cleanup - log errors in production
                }
            }

            _bytes = null;
            _tempFilePath = null;
        }
    }
}

/// <summary>
/// Enhanced extraction result with orchestration metadata
/// </summary>
internal record EnhancedExtractionResult(
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

    /// <summary>
    /// Creates failure result with error message (defense in depth validation)
    /// </summary>
    public static EnhancedExtractionResult CreateFailure(string errorMessage)
    {
        return new EnhancedExtractionResult(
            Success: false,
            ExtractedText: string.Empty,
            PageCount: 0,
            CharacterCount: 0,
            OcrTriggered: false,
            Quality: ExtractionQuality.VeryLow,
            StageUsed: 0,
            StageName: "None",
            TotalDurationMs: 0,
            ErrorMessage: errorMessage);
    }
}

/// <summary>
/// Enhanced paged extraction result with orchestration metadata
/// </summary>
internal record EnhancedPagedExtractionResult(
    bool Success,
    IList<PageTextChunk> PageChunks,
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

