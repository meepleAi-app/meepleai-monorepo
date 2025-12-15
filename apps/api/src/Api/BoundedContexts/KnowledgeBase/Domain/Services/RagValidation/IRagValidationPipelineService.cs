using Api.Models;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service orchestrator for RAG validation pipeline (all 5 layers)
/// ISSUE-977: BGAI-035 - Wire all 5 validation layers in RAG pipeline
/// </summary>
/// <remarks>
/// Integrates all validation layers in the RAG pipeline:
///
/// Layer 1: Confidence Validation (≥0.70 threshold)
/// Layer 2: Multi-Model Consensus (GPT-4 + Claude, ≥0.90 similarity)
/// Layer 3: Citation Validation (verify PDF sources and page numbers)
/// Layer 4: Hallucination Detection (forbidden keyword analysis)
/// Layer 5: Validation Accuracy Tracking (≥80% accuracy baseline)
///
/// Architecture:
/// - Applies validations in sequence for comprehensive quality assurance
/// - Returns aggregated results with all validation outcomes
/// - Supports both standard and multi-model validation modes
/// - Integrates with existing RagService for seamless adoption
///
/// Usage:
/// - Standard mode: Validates existing LLM response (layers 1, 3, 4)
/// - Multi-model mode: Generates multi-model response + full validation (all 5 layers)
/// </remarks>
internal interface IRagValidationPipelineService
{
    /// <summary>
    /// Validate a QA response through all applicable validation layers
    /// Applies layers 1, 3, 4 (confidence, citation, hallucination)
    /// </summary>
    /// <param name="response">QA response to validate</param>
    /// <param name="gameId">Game ID for citation validation</param>
    /// <param name="language">Language for hallucination detection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aggregated validation result</returns>
    Task<RagValidationResult> ValidateResponseAsync(
        QaResponse response,
        string gameId,
        string? language = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Validate a response with multi-model consensus (all 5 layers)
    /// Applies layers 1, 2, 3, 4, 5 (includes multi-model validation and accuracy tracking)
    /// </summary>
    /// <param name="response">QA response to validate</param>
    /// <param name="gameId">Game ID for citation validation</param>
    /// <param name="systemPrompt">System prompt for multi-model consensus</param>
    /// <param name="userPrompt">User prompt for multi-model consensus</param>
    /// <param name="language">Language for hallucination detection</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Aggregated validation result with multi-model consensus</returns>
    Task<RagValidationResult> ValidateWithMultiModelAsync(
        QaResponse response,
        string gameId,
        string systemPrompt,
        string userPrompt,
        string? language = null,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Aggregated result from RAG validation pipeline
/// </summary>
internal record RagValidationResult
{
    /// <summary>
    /// Overall validation passed (all layers pass)
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Count of layers passed
    /// </summary>
    public required int LayersPassed { get; init; }

    /// <summary>
    /// Total layers executed
    /// </summary>
    public required int TotalLayers { get; init; }

    /// <summary>
    /// Layer 1: Confidence validation result
    /// </summary>
    public required ConfidenceValidationResult ConfidenceValidation { get; init; }

    /// <summary>
    /// Layer 2: Multi-model consensus result (null if not executed)
    /// </summary>
    public MultiModelConsensusResult? MultiModelConsensus { get; init; }

    /// <summary>
    /// Layer 3: Citation validation result
    /// </summary>
    public required CitationValidationResult CitationValidation { get; init; }

    /// <summary>
    /// Layer 4: Hallucination detection result
    /// </summary>
    public required HallucinationValidationResult HallucinationDetection { get; init; }

    /// <summary>
    /// Layer 5: Validation accuracy tracking (null if not executed)
    /// </summary>
    public string? ValidationAccuracyMetrics { get; init; }

    /// <summary>
    /// Overall validation message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Overall severity level
    /// </summary>
    public required RagValidationSeverity Severity { get; init; }

    /// <summary>
    /// Total validation duration in milliseconds
    /// </summary>
    public required long DurationMs { get; init; }
}

/// <summary>
/// Overall severity level for RAG validation
/// </summary>
internal enum RagValidationSeverity
{
    /// <summary>
    /// All validations passed
    /// </summary>
    Pass,

    /// <summary>
    /// Some validations failed but not critical
    /// </summary>
    Warning,

    /// <summary>
    /// Critical validations failed
    /// </summary>
    Critical,

    /// <summary>
    /// Validation error occurred
    /// </summary>
    Error
}
