using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading.Tasks;
using Api.Models;
using Api.Services;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service orchestrator for RAG validation pipeline
/// ISSUE-977: BGAI-035 - Wire the validation layers in RAG pipeline
/// ISSUE-979: BGAI-037 - Performance optimization (parallel validation)
/// </summary>
/// <remarks>
/// Orchestrates validation pipeline by coordinating the validation services:
/// 1. ConfidenceValidationService (≥0.70)
/// 3. CitationValidationService (verify PDF sources)
/// 4. HallucinationDetectionService (forbidden keywords)
///
/// #4179: il livello 2, il consenso fra due modelli, è stato tolto. Chiamava OpenRouter
/// direttamente, fuori dal gateway e dal registro costi, verso due varianti «:free» sparite dal
/// listino del provider, e aggiungeva due chiamate a ogni AskQuestion.
///
/// Design:
/// - Parallel validation execution for optimal performance (BGAI-037)
/// - Layer 1 (Confidence) executes first for early exit capability
/// - Layers 3, 4 execute in parallel using Task.WhenAll()
/// - Expected performance improvement: 30-66% reduction in validation time
/// - Comprehensive logging for quality tracking
/// - Thread-safe and stateless for singleton lifecycle
/// </remarks>
internal class RagValidationPipelineService : IRagValidationPipelineService
{
    private readonly IConfidenceValidationService _confidenceValidation;
    private readonly ICitationValidationService _citationValidation;
    private readonly IHallucinationDetectionService _hallucinationDetection;
    private readonly ILogger<RagValidationPipelineService> _logger;

    public RagValidationPipelineService(
        IConfidenceValidationService confidenceValidation,
        ICitationValidationService citationValidation,
        IHallucinationDetectionService hallucinationDetection,
        ILogger<RagValidationPipelineService> logger)
    {
        _confidenceValidation = confidenceValidation ?? throw new ArgumentNullException(nameof(confidenceValidation));
        _citationValidation = citationValidation ?? throw new ArgumentNullException(nameof(citationValidation));
        _hallucinationDetection = hallucinationDetection ?? throw new ArgumentNullException(nameof(hallucinationDetection));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public async Task<RagValidationResult> ValidateResponseAsync(
        QaResponse response,
        string gameId,
        string? language = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(response);

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
        var (citationResult, hallucinationResult) = await ExecuteStandardParallelValidationAsync(
            snippets, gameId, answerText, language, cancellationToken).ConfigureAwait(false);

        var citation = citationResult!;
        var hallucination = hallucinationResult!;

        LogStandardValidationDetails(citation, hallucination);

        stopwatch.Stop();

        // Calculate overall validation status
        var layersPassed = 0;
        const int totalLayers = 3; // Standard mode: 3 layers

        if (confidenceResult.IsValid) layersPassed++;
        if (citation.IsValid) layersPassed++;
        if (hallucination.IsValid) layersPassed++;

        var isValid = layersPassed == totalLayers;
        var severity = CalculateSeverity(confidenceResult!, citationResult!, hallucinationResult!);
        var message = BuildValidationMessage(isValid, layersPassed, totalLayers);

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
            CitationValidation = citation,
            HallucinationDetection = hallucination,
            Message = message,
            Severity = severity,
            DurationMs = stopwatch.ElapsedMilliseconds
        };
    }

#pragma warning disable S1172 // Unused method parameters - CancellationToken intentionally ignored, using CancellationToken.None for validation tasks
    private async Task<(CitationValidationResult, HallucinationValidationResult)> ExecuteStandardParallelValidationAsync(
        List<Snippet> snippets,
        string gameId,
        string answerText,
        string language,
        CancellationToken cancellationToken)
#pragma warning restore S1172
    {
        var citationTask = EnsureCitationTask(
            _citationValidation.ValidateCitationsAsync(snippets, gameId, CancellationToken.None),
            snippets.Count);

        var hallucinationTask = EnsureHallucinationTask(
            _hallucinationDetection.DetectHallucinationsAsync(answerText, language, CancellationToken.None));

        await Task.WhenAll(citationTask, hallucinationTask).ConfigureAwait(false);

        var citationResult = EnsureCitationResult(await citationTask.ConfigureAwait(false), snippets.Count);
        var hallucinationResult = EnsureHallucinationResult(await hallucinationTask.ConfigureAwait(false));

        return (citationResult, hallucinationResult);
    }

    private void LogStandardValidationDetails(CitationValidationResult citation, HallucinationValidationResult hallucination)
    {
        var citationStatus = citation.IsValid ? "PASS" : "FAIL";
        _logger.LogDebug(
            "Layer 3 (Citation): {Status} - {ValidCount}/{TotalCount} citations valid",
            citationStatus,
            citation.ValidCitations,
            citation.TotalCitations);

        var hallucinationStatus = hallucination.IsValid ? "PASS" : "FAIL";
        _logger.LogDebug(
            "Layer 4 (Hallucination): {Status} - {DetectedCount} keywords detected (severity: {Severity})",
            hallucinationStatus,
            hallucination.DetectedKeywords?.Count ?? 0,
            hallucination.Severity);
    }

    /// <summary>
    /// Calculate overall validation severity based on individual layer results
    /// </summary>
    private RagValidationSeverity CalculateSeverity(
        ConfidenceValidationResult confidence,
        CitationValidationResult citation,
        HallucinationValidationResult hallucination)
    {
        // Critical: Any critical-level failure
        if (confidence.Severity == ValidationSeverity.Critical ||
            hallucination.Severity == HallucinationSeverity.High)
        {
            return RagValidationSeverity.Critical;
        }

        // Warning: Any warning-level or some failures
        if (confidence.Severity == ValidationSeverity.Warning ||
            !citation.IsValid ||
            hallucination.Severity == HallucinationSeverity.Medium ||
            hallucination.Severity == HallucinationSeverity.Low)
        {
            return RagValidationSeverity.Warning;
        }

        // Pass: All validations passed
        return RagValidationSeverity.Pass;
    }

    /// <summary>
    /// Build overall validation message
    /// </summary>
    private static string BuildValidationMessage(bool isValid, int layersPassed, int totalLayers)
    {
        var status = isValid ? "All validations passed" : $"{layersPassed}/{totalLayers} validations passed";

        return $"{status} (standard mode)";
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

    private static ConfidenceValidationResult DefaultConfidenceResult(double? confidence) =>
        new ConfidenceValidationResult
        {
            IsValid = false,
            ActualConfidence = confidence ?? 0,
            RequiredThreshold = 0.7,
            ValidationMessage = "Confidence validation unavailable (result was null)",
            Severity = ValidationSeverity.Warning
        };

    private static Task<CitationValidationResult> EnsureCitationTask(Task<CitationValidationResult>? task, int totalSnippets) =>
        task ?? Task.FromResult(DefaultCitationResult(totalSnippets));

    private static Task<HallucinationValidationResult> EnsureHallucinationTask(Task<HallucinationValidationResult>? task) =>
        task ?? Task.FromResult(DefaultHallucinationResult());

    private static CitationValidationResult DefaultCitationResult(int totalSnippets) =>
        EnsureCitationResult(null, totalSnippets);

    private static HallucinationValidationResult DefaultHallucinationResult() =>
        EnsureHallucinationResult(null);
}
