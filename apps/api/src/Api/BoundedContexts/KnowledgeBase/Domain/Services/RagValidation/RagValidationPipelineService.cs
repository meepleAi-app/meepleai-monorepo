using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service orchestrator for RAG validation pipeline (all 5 layers)
/// ISSUE-977: BGAI-035 - Wire all 5 validation layers in RAG pipeline
/// ISSUE-979: BGAI-037 - Performance optimization (parallel validation)
/// </summary>
/// <remarks>
/// Orchestrates validation pipeline by coordinating all 5 validation services:
/// 1. ConfidenceValidationService (≥0.70)
/// 2. MultiModelValidationService (GPT-4 + Claude consensus ≥0.90)
/// 3. CitationValidationService (verify PDF sources)
/// 4. HallucinationDetectionService (forbidden keywords)
/// 5. ValidationAccuracyTrackingService (≥80% accuracy)
///
/// Design:
/// - Parallel validation execution for optimal performance (BGAI-037)
/// - Layer 1 (Confidence) executes first for early exit capability
/// - Layers 2, 3, 4 execute in parallel using Task.WhenAll()
/// - Expected performance improvement: 30-66% reduction in validation time
/// - Comprehensive logging for quality tracking
/// - Thread-safe and stateless for singleton lifecycle
/// </remarks>
public class RagValidationPipelineService : IRagValidationPipelineService
{
    private readonly IConfidenceValidationService _confidenceValidation;
    private readonly IMultiModelValidationService _multiModelValidation;
    private readonly ICitationValidationService _citationValidation;
    private readonly IHallucinationDetectionService _hallucinationDetection;
    private readonly ValidationAccuracyTrackingService _accuracyTracking;
    private readonly ILogger<RagValidationPipelineService> _logger;

    public RagValidationPipelineService(
        IConfidenceValidationService confidenceValidation,
        IMultiModelValidationService multiModelValidation,
        ICitationValidationService citationValidation,
        IHallucinationDetectionService hallucinationDetection,
        ValidationAccuracyTrackingService accuracyTracking,
        ILogger<RagValidationPipelineService> logger)
    {
        _confidenceValidation = confidenceValidation ?? throw new ArgumentNullException(nameof(confidenceValidation));
        _multiModelValidation = multiModelValidation ?? throw new ArgumentNullException(nameof(multiModelValidation));
        _citationValidation = citationValidation ?? throw new ArgumentNullException(nameof(citationValidation));
        _hallucinationDetection = hallucinationDetection ?? throw new ArgumentNullException(nameof(hallucinationDetection));
        _accuracyTracking = accuracyTracking ?? throw new ArgumentNullException(nameof(accuracyTracking));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<RagValidationResult> ValidateResponseAsync(
        QaResponse response,
        string gameId,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));

        if (string.IsNullOrWhiteSpace(gameId))
            throw new ArgumentException("Game ID cannot be null or empty", nameof(gameId));

        language ??= "en";
        var snippets = (response.snippets ?? new List<Snippet>()).Where(s => s != null).ToList();
        var answerText = response.answer ?? string.Empty;

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting RAG validation pipeline (standard mode: 3 layers, parallel execution) for game {GameId}",
            gameId);

        // Layer 1: Confidence Validation (must be first, synchronous early exit possible)
        var confidenceResult = _confidenceValidation.ValidateConfidence(response.confidence)
                               ?? DefaultConfidenceResult(response.confidence);
        _logger.LogDebug(
            "Layer 1 (Confidence): {Status} - {Message}",
            confidenceResult.IsValid ? "PASS" : "FAIL",
            confidenceResult.ValidationMessage);

        // Layer 3 & 4: Execute in parallel for performance optimization (BGAI-037)
        var citationTask = EnsureCitationTask(
            _citationValidation.ValidateCitationsAsync(snippets, gameId, CancellationToken.None),
            snippets.Count);

        var hallucinationTask = EnsureHallucinationTask(
            _hallucinationDetection.DetectHallucinationsAsync(answerText, language, CancellationToken.None));

        // Wait for both tasks to complete
        await Task.WhenAll(citationTask, hallucinationTask).ConfigureAwait(false);

        var citationResult = EnsureCitationResult(await citationTask.ConfigureAwait(false), snippets.Count);
        var hallucinationResult = EnsureHallucinationResult(await hallucinationTask.ConfigureAwait(false));
        var citation = citationResult!;
        var hallucination = hallucinationResult!;

        var citationValidCount = citation.ValidCitations;
        var citationTotalCount = citation.TotalCitations;
        var citationStatus = citation.IsValid ? "PASS" : "FAIL";

        _logger.LogDebug(
            "Layer 3 (Citation): {Status} - {ValidCount}/{TotalCount} citations valid",
            citationStatus,
            citationValidCount,
            citationTotalCount);

        var hallucinationDetected = hallucination.DetectedKeywords?.Count ?? 0;
        var hallucinationSeverity = hallucination.Severity;
        var hallucinationStatus = hallucination.IsValid ? "PASS" : "FAIL";

        _logger.LogDebug(
            "Layer 4 (Hallucination): {Status} - {DetectedCount} keywords detected (severity: {Severity})",
            hallucinationStatus,
            hallucinationDetected,
            hallucinationSeverity);

        stopwatch.Stop();

        // Calculate overall validation status
        var layersPassed = 0;
        const int totalLayers = 3; // Standard mode: 3 layers

        if (confidenceResult.IsValid) layersPassed++;
        if (citation.IsValid) layersPassed++;
        if (hallucination.IsValid) layersPassed++;

        var isValid = layersPassed == totalLayers;
        var severity = CalculateSeverity(confidenceResult!, null, citationResult!, hallucinationResult!);
        var message = BuildValidationMessage(isValid, layersPassed, totalLayers, false);

        _logger.LogInformation(
            "RAG validation pipeline complete (standard mode): {Status} - {PassedCount}/{TotalCount} layers passed (duration: {Duration}ms)",
            isValid ? "PASS" : "FAIL",
            layersPassed,
            totalLayers,
            stopwatch.ElapsedMilliseconds);

        return new RagValidationResult
        {
            IsValid = isValid,
            LayersPassed = layersPassed,
            TotalLayers = totalLayers,
            ConfidenceValidation = confidenceResult,
            MultiModelConsensus = null,
            CitationValidation = citation,
            HallucinationDetection = hallucination,
            ValidationAccuracyMetrics = null,
            Message = message,
            Severity = severity,
            DurationMs = stopwatch.ElapsedMilliseconds
        };
    }

    /// <inheritdoc/>
    public async Task<RagValidationResult> ValidateWithMultiModelAsync(
        QaResponse response,
        string gameId,
        string systemPrompt,
        string userPrompt,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        if (response == null)
            throw new ArgumentNullException(nameof(response));

        if (string.IsNullOrWhiteSpace(gameId))
            throw new ArgumentException("Game ID cannot be null or empty", nameof(gameId));

        if (string.IsNullOrWhiteSpace(systemPrompt))
            throw new ArgumentException("System prompt cannot be null or empty", nameof(systemPrompt));

        if (string.IsNullOrWhiteSpace(userPrompt))
            throw new ArgumentException("User prompt cannot be null or empty", nameof(userPrompt));

        language ??= "en";
        var snippets = (response.snippets ?? new List<Snippet>()).Where(s => s != null).ToList();
        var answerText = response.answer ?? string.Empty;

        var stopwatch = Stopwatch.StartNew();

        _logger.LogInformation(
            "Starting RAG validation pipeline (multi-model mode: 4 layers, parallel execution) for game {GameId}",
            gameId);

        // Layer 1: Confidence Validation (must be first, synchronous early exit possible)
        var confidenceResult = _confidenceValidation.ValidateConfidence(response.confidence)
                               ?? DefaultConfidenceResult(response.confidence);
        _logger.LogDebug(
            "Layer 1 (Confidence): {Status} - {Message}",
            confidenceResult.IsValid ? "PASS" : "FAIL",
            confidenceResult.ValidationMessage);

        // Layer 2, 3, 4: Execute in parallel for performance optimization (BGAI-037)
        // Note: Layer 4 depends on Layer 2 result for text selection, but we handle this with continuation
        var multiModelTask = EnsureMultiModelTask(
            _multiModelValidation.ValidateWithConsensusAsync(
                systemPrompt,
                userPrompt,
                temperature: 0.3,
                maxTokens: 1000,
                CancellationToken.None));

        var citationTask = EnsureCitationTask(
            _citationValidation.ValidateCitationsAsync(snippets, gameId, CancellationToken.None),
            snippets.Count);

        // Start hallucination detection after multi-model completes (needs consensus response)
        var hallucinationTask = multiModelTask.ContinueWith(async task =>
        {
            var multiModelResult = EnsureMultiModelResult(await task.ConfigureAwait(false));
            var textToValidate = multiModelResult.HasConsensus && !string.IsNullOrWhiteSpace(multiModelResult.ConsensusResponse)
                ? multiModelResult.ConsensusResponse
                : answerText;

            return await _hallucinationDetection.DetectHallucinationsAsync(
                textToValidate,
                language,
                CancellationToken.None);
        }, cancellationToken).Unwrap();

        // Wait for all tasks to complete
        await Task.WhenAll(multiModelTask, citationTask, hallucinationTask).ConfigureAwait(false);

        var multiModelResult = EnsureMultiModelResult(await multiModelTask.ConfigureAwait(false));
        var citationResult = EnsureCitationResult(await citationTask.ConfigureAwait(false), snippets.Count);
        var hallucinationResult = EnsureHallucinationResult(await hallucinationTask.ConfigureAwait(false));
        var multiModel = multiModelResult!;
        var citation = citationResult!;
        var hallucination = hallucinationResult!;

        var multiModelStatus = multiModel.HasConsensus ? "PASS" : "FAIL";
        var multiModelSimilarity = multiModel.SimilarityScore;
        var multiModelThreshold = multiModel.RequiredThreshold;

        _logger.LogDebug(
            "Layer 2 (Multi-Model Consensus): {Status} - Similarity: {Similarity:F3} (threshold: {Threshold:F2})",
            multiModelStatus,
            multiModelSimilarity,
            multiModelThreshold);

        var citationValidCount2 = citation.ValidCitations;
        var citationTotalCount2 = citation.TotalCitations;
        var citationStatus2 = citation.IsValid ? "PASS" : "FAIL";

        _logger.LogDebug(
            "Layer 3 (Citation): {Status} - {ValidCount}/{TotalCount} citations valid",
            citationStatus2,
            citationValidCount2,
            citationTotalCount2);

        var hallucinationDetected2 = hallucination.DetectedKeywords?.Count ?? 0;
        var hallucinationSeverity2 = hallucination.Severity;
        var hallucinationStatus2 = hallucination.IsValid ? "PASS" : "FAIL";

        _logger.LogDebug(
            "Layer 4 (Hallucination): {Status} - {DetectedCount} keywords detected (severity: {Severity})",
            hallucinationStatus2,
            hallucinationDetected2,
            hallucinationSeverity2);

        stopwatch.Stop();

        // Calculate overall validation status
        var layersPassed = 0;
        const int totalLayers = 4; // Multi-model mode: 4 layers (Layer 5 is for metrics tracking, not pass/fail)

        if (confidenceResult.IsValid) layersPassed++;
        if (multiModel.HasConsensus) layersPassed++;
        if (citation.IsValid) layersPassed++;
        if (hallucination.IsValid) layersPassed++;

        var isValid = layersPassed == totalLayers;
        var severity = CalculateSeverity(confidenceResult!, multiModelResult!, citationResult!, hallucinationResult!);
        var message = BuildValidationMessage(isValid, layersPassed, totalLayers, true);

        // Layer 5: Validation Accuracy Tracking (informational only, doesn't affect pass/fail)
        // This layer is used for measuring the validation system's performance over time
        var accuracyMetrics = $"Validation accuracy tracking enabled (baseline threshold: {ValidationAccuracyTrackingService.MinimumAccuracyThreshold:P0})";

        _logger.LogInformation(
            "RAG validation pipeline complete (multi-model mode): {Status} - {PassedCount}/{TotalCount} layers passed (duration: {Duration}ms)",
            isValid ? "PASS" : "FAIL",
            layersPassed,
            totalLayers,
            stopwatch.ElapsedMilliseconds);

        return new RagValidationResult
        {
            IsValid = isValid,
            LayersPassed = layersPassed,
            TotalLayers = totalLayers,
            ConfidenceValidation = confidenceResult,
            MultiModelConsensus = multiModel,
            CitationValidation = citation,
            HallucinationDetection = hallucination,
            ValidationAccuracyMetrics = accuracyMetrics,
            Message = message,
            Severity = severity,
            DurationMs = stopwatch.ElapsedMilliseconds
        };
    }

    /// <summary>
    /// Calculate overall validation severity based on individual layer results
    /// </summary>
    private RagValidationSeverity CalculateSeverity(
        ConfidenceValidationResult confidence,
        MultiModelConsensusResult? multiModel,
        CitationValidationResult citation,
        HallucinationValidationResult hallucination)
    {
        // Critical: Any critical-level failure
        if (confidence.Severity == ValidationSeverity.Critical ||
            (multiModel?.Severity == ConsensusSeverity.Error) ||
            hallucination.Severity == HallucinationSeverity.High)
        {
            return RagValidationSeverity.Critical;
        }

        // Warning: Any warning-level or some failures
        if (confidence.Severity == ValidationSeverity.Warning ||
            !citation.IsValid ||
            hallucination.Severity == HallucinationSeverity.Medium ||
            hallucination.Severity == HallucinationSeverity.Low ||
            (multiModel != null && !multiModel.HasConsensus))
        {
            return RagValidationSeverity.Warning;
        }

        // Pass: All validations passed
        return RagValidationSeverity.Pass;
    }

    /// <summary>
    /// Build overall validation message
    /// </summary>
    private string BuildValidationMessage(bool isValid, int layersPassed, int totalLayers, bool multiModelMode)
    {
        var mode = multiModelMode ? "multi-model" : "standard";
        var status = isValid ? "All validations passed" : $"{layersPassed}/{totalLayers} validations passed";

        return $"{status} ({mode} mode)";
    }

    private static CitationValidationResult EnsureCitationResult(CitationValidationResult? result, int totalSnippets)
    {
        if (result != null)
        {
            return result;
        }

        return new CitationValidationResult
        {
            IsValid = false,
            TotalCitations = totalSnippets,
            ValidCitations = 0,
            Errors = new List<CitationValidationError>(),
            Message = "Citation validation unavailable (result was null)"
        };
    }

    private static HallucinationValidationResult EnsureHallucinationResult(HallucinationValidationResult? result)
    {
        if (result != null)
        {
            return result;
        }

        return new HallucinationValidationResult
        {
            IsValid = false,
            DetectedKeywords = new List<string>(),
            Language = "en",
            TotalKeywordsChecked = 0,
            Message = "Hallucination detection unavailable (result was null)",
            Severity = HallucinationSeverity.Medium
        };
    }

    private static MultiModelConsensusResult EnsureMultiModelResult(MultiModelConsensusResult? result)
    {
        if (result != null)
        {
            return result;
        }

        return new MultiModelConsensusResult
        {
            HasConsensus = false,
            ConsensusResponse = string.Empty,
            SimilarityScore = 0.0,
            RequiredThreshold = 0.9,
            Severity = ConsensusSeverity.Error,
            Message = "Multi-model validation unavailable (result was null)",
            TotalDurationMs = 0,
            Gpt4Response = DefaultModelResponse("gpt-4o"),
            ClaudeResponse = DefaultModelResponse("claude-3")
        };
    }

    private static ConfidenceValidationResult DefaultConfidenceResult(double? confidence) =>
        new ConfidenceValidationResult
        {
            IsValid = false,
            ActualConfidence = confidence ?? 0,
            RequiredThreshold = 0.7,
            ValidationMessage = "Confidence validation unavailable (result was null)",
            Severity = ValidationSeverity.Warning
        };

    private static ModelResponse DefaultModelResponse(string modelId) =>
        new ModelResponse
        {
            ModelId = modelId,
            ResponseText = string.Empty,
            IsSuccess = false,
            Usage = new LlmUsage(0, 0, 0),
            DurationMs = 0
        };

    private static Task<CitationValidationResult> EnsureCitationTask(Task<CitationValidationResult>? task, int totalSnippets) =>
        task ?? Task.FromResult(DefaultCitationResult(totalSnippets));

    private static Task<HallucinationValidationResult> EnsureHallucinationTask(Task<HallucinationValidationResult>? task) =>
        task ?? Task.FromResult(DefaultHallucinationResult());

    private static Task<MultiModelConsensusResult> EnsureMultiModelTask(Task<MultiModelConsensusResult>? task) =>
        task ?? Task.FromResult(DefaultMultiModelResult());

    private static CitationValidationResult DefaultCitationResult(int totalSnippets) =>
        EnsureCitationResult(null, totalSnippets);

    private static HallucinationValidationResult DefaultHallucinationResult() =>
        EnsureHallucinationResult(null);

    private static MultiModelConsensusResult DefaultMultiModelResult() =>
        EnsureMultiModelResult(null);
}
