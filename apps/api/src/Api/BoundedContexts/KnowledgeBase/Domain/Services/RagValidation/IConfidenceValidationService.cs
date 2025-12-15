

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for validating AI response confidence against quality thresholds
/// ISSUE-970: BGAI-028 - Confidence validation layer (threshold ≥0.70)
/// </summary>
internal interface IConfidenceValidationService
{
    /// <summary>
    /// Validate response confidence score against threshold
    /// </summary>
    /// <param name="confidence">Overall confidence score (0.0-1.0)</param>
    /// <returns>Validation result with pass/fail and metadata</returns>
    ConfidenceValidationResult ValidateConfidence(double? confidence);

    /// <summary>
    /// Get current confidence threshold
    /// </summary>
    double ConfidenceThreshold { get; }
}

/// <summary>
/// Result of confidence validation
/// </summary>
internal record ConfidenceValidationResult
{
    /// <summary>
    /// Whether confidence meets minimum threshold
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Actual confidence score that was validated
    /// </summary>
    public required double? ActualConfidence { get; init; }

    /// <summary>
    /// Minimum required threshold
    /// </summary>
    public required double RequiredThreshold { get; init; }

    /// <summary>
    /// Validation status message
    /// </summary>
    public required string ValidationMessage { get; init; }

    /// <summary>
    /// Severity level for logging/alerting
    /// </summary>
    public required ValidationSeverity Severity { get; init; }
}

/// <summary>
/// Severity level for confidence validation
/// </summary>
internal enum ValidationSeverity
{
    /// <summary>
    /// Confidence meets threshold (≥0.70)
    /// </summary>
    Pass,

    /// <summary>
    /// Confidence below threshold but present (0.60-0.70)
    /// </summary>
    Warning,

    /// <summary>
    /// Confidence critically low (<0.60)
    /// </summary>
    Critical,

    /// <summary>
    /// No confidence score available
    /// </summary>
    Unknown
}
