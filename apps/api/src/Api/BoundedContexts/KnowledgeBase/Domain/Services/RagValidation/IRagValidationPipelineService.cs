using Api.Models;

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service orchestrator for RAG validation pipeline
/// ISSUE-977: BGAI-035 - Wire the validation layers in RAG pipeline
/// </summary>
/// <remarks>
/// Integrates the validation layers in the RAG pipeline:
///
/// Layer 1: Confidence Validation (≥0.70 threshold)
/// Layer 3: Citation Validation (verify PDF sources and page numbers)
/// Layer 4: Hallucination Detection (forbidden keyword analysis)
///
/// #4179: il livello 2 (consenso fra due modelli) e il 5, che ne dipendeva, sono stati tolti;
/// i numeri dei livelli restano quelli originali.
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
    /// Layer 3: Citation validation result
    /// </summary>
    public required CitationValidationResult CitationValidation { get; init; }

    /// <summary>
    /// Layer 4: Hallucination detection result
    /// </summary>
    public required HallucinationValidationResult HallucinationDetection { get; init; }

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
