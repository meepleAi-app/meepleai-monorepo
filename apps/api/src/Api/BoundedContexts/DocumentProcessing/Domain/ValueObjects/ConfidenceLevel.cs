namespace Api.BoundedContexts.DocumentProcessing.Domain.ValueObjects;

/// <summary>
/// Categorical confidence level derived from an OCR confidence score.
/// Libro Game AI Assistant MVP Phase 1.
/// </summary>
public enum ConfidenceLevel
{
    High,
    Medium,
    Low
}

/// <summary>
/// Extension methods for mapping numeric confidence scores to <see cref="ConfidenceLevel"/>.
/// </summary>
public static class ConfidenceLevelExtensions
{
    /// <summary>
    /// Converts a numeric OCR confidence score (0.0–1.0) to a categorical <see cref="ConfidenceLevel"/>.
    /// </summary>
    /// <param name="score">Confidence score between 0.0 and 1.0.</param>
    /// <returns>Categorical confidence level.</returns>
    public static ConfidenceLevel FromScore(double score) => score switch
    {
        >= 0.85 => ConfidenceLevel.High,
        >= 0.7 => ConfidenceLevel.Medium,
        _ => ConfidenceLevel.Low
    };
}
