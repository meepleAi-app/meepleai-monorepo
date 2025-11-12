using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for validating AI response confidence against quality thresholds
/// ISSUE-970: BGAI-028 - Confidence validation layer (threshold ≥0.70)
/// </summary>
/// <remarks>
/// Enforces minimum confidence threshold for AI-generated responses.
/// Threshold: 0.70 (>95% accuracy target for board game rules)
///
/// Validation Tiers:
/// - ≥0.70: PASS (acceptable quality)
/// - 0.60-0.70: WARNING (below threshold but usable)
/// - <0.60: CRITICAL (unacceptable quality)
/// - null: UNKNOWN (no confidence score available)
///
/// Integration: Used by RagService to validate responses before returning to users.
/// </remarks>
public class ConfidenceValidationService : IConfidenceValidationService
{
    private readonly ILogger<ConfidenceValidationService> _logger;

    /// <summary>
    /// Minimum confidence threshold for acceptable responses
    /// Target: >95% accuracy for board game rules (correlates to ~0.70 confidence)
    /// </summary>
    public const double MinimumConfidenceThreshold = 0.70;

    /// <summary>
    /// Warning threshold (below target but not critical)
    /// </summary>
    private const double WarningThreshold = 0.60;

    public ConfidenceValidationService(ILogger<ConfidenceValidationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    /// <inheritdoc/>
    public double ConfidenceThreshold => MinimumConfidenceThreshold;

    /// <inheritdoc/>
    public ConfidenceValidationResult ValidateConfidence(double? confidence)
    {
        // Case 1: No confidence score available
        if (!confidence.HasValue)
        {
            _logger.LogWarning("Confidence validation: No confidence score available (unknown quality)");

            return new ConfidenceValidationResult
            {
                IsValid = false,
                ActualConfidence = null,
                RequiredThreshold = MinimumConfidenceThreshold,
                ValidationMessage = "No confidence score available",
                Severity = ValidationSeverity.Unknown
            };
        }

        var actualConfidence = confidence.Value;

        // Case 2: Confidence meets threshold (≥0.70)
        if (actualConfidence >= MinimumConfidenceThreshold)
        {
            _logger.LogDebug(
                "Confidence validation: PASS (confidence={Confidence:F3} ≥ threshold={Threshold:F2})",
                actualConfidence, MinimumConfidenceThreshold);

            return new ConfidenceValidationResult
            {
                IsValid = true,
                ActualConfidence = actualConfidence,
                RequiredThreshold = MinimumConfidenceThreshold,
                ValidationMessage = $"Confidence {actualConfidence:F3} meets threshold {MinimumConfidenceThreshold:F2}",
                Severity = ValidationSeverity.Pass
            };
        }

        // Case 3: Confidence below threshold but above warning (0.60-0.70)
        if (actualConfidence >= WarningThreshold)
        {
            _logger.LogWarning(
                "Confidence validation: WARNING (confidence={Confidence:F3} < threshold={Threshold:F2}, but ≥{WarningThreshold:F2})",
                actualConfidence, MinimumConfidenceThreshold, WarningThreshold);

            return new ConfidenceValidationResult
            {
                IsValid = false,
                ActualConfidence = actualConfidence,
                RequiredThreshold = MinimumConfidenceThreshold,
                ValidationMessage = $"Confidence {actualConfidence:F3} below threshold {MinimumConfidenceThreshold:F2} (warning level)",
                Severity = ValidationSeverity.Warning
            };
        }

        // Case 4: Confidence critically low (<0.60)
        _logger.LogError(
            "Confidence validation: CRITICAL (confidence={Confidence:F3} < critical threshold={CriticalThreshold:F2})",
            actualConfidence, WarningThreshold);

        return new ConfidenceValidationResult
        {
            IsValid = false,
            ActualConfidence = actualConfidence,
            RequiredThreshold = MinimumConfidenceThreshold,
            ValidationMessage = $"Confidence {actualConfidence:F3} critically low (< {WarningThreshold:F2})",
            Severity = ValidationSeverity.Critical
        };
    }
}
