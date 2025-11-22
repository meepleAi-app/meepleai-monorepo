namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for detecting hallucinations via forbidden keyword analysis
/// ISSUE-972: BGAI-030 - Hallucination detection (forbidden keywords)
/// </summary>
public interface IHallucinationDetectionService
{
    /// <summary>
    /// Detect hallucinations in AI-generated response text
    /// </summary>
    /// <param name="responseText">AI-generated response to analyze</param>
    /// <param name="language">Language code (en, it, de, fr, es) - auto-detects if null</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Validation result with detected hallucination keywords</returns>
    Task<HallucinationValidationResult> DetectHallucinationsAsync(
        string responseText,
        string? language = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Get count of forbidden keywords for a language
    /// </summary>
    int GetForbiddenKeywordCount(string language);
}

/// <summary>
/// Result of hallucination detection
/// </summary>
public record HallucinationValidationResult
{
    /// <summary>
    /// Whether response is free of hallucination indicators
    /// </summary>
    public required bool IsValid { get; init; }

    /// <summary>
    /// Detected forbidden keywords in response
    /// </summary>
    public required List<string> DetectedKeywords { get; init; }

    /// <summary>
    /// Language used for detection
    /// </summary>
    public required string Language { get; init; }

    /// <summary>
    /// Total forbidden keywords checked
    /// </summary>
    public required int TotalKeywordsChecked { get; init; }

    /// <summary>
    /// Validation message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Severity of detected hallucinations
    /// </summary>
    public required HallucinationSeverity Severity { get; init; }
}

/// <summary>
/// Severity level for hallucination detection
/// </summary>
public enum HallucinationSeverity
{
    /// <summary>
    /// No hallucinations detected
    /// </summary>
    None,

    /// <summary>
    /// Minor uncertainty phrases detected (1-2 keywords)
    /// </summary>
    Low,

    /// <summary>
    /// Multiple uncertainty indicators (3-4 keywords)
    /// </summary>
    Medium,

    /// <summary>
    /// Critical hallucination indicators (5+ keywords or explicit "I don't know")
    /// </summary>
    High
}
