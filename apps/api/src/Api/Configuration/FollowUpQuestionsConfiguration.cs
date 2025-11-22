namespace Api.Configuration;

/// <summary>
/// Configuration for AI-generated follow-up questions feature (CHAT-02).
/// </summary>
public class FollowUpQuestionsConfiguration
{
    public const string SectionName = "FollowUpQuestions";

    /// <summary>
    /// Whether follow-up question generation is enabled globally.
    /// </summary>
    public bool Enabled { get; set; } = true;

    /// <summary>
    /// Maximum number of follow-up questions to generate per response.
    /// </summary>
    public int MaxQuestionsPerResponse { get; set; } = 5;

    /// <summary>
    /// Timeout for follow-up generation in milliseconds (default: 10 seconds).
    /// Prevents follow-up generation from blocking the main QA response.
    /// </summary>
    public int GenerationTimeoutMs { get; set; } = 10000;

    /// <summary>
    /// Maximum retry attempts for JSON parsing failures.
    /// </summary>
    public int MaxRetries { get; set; } = 2;

    /// <summary>
    /// Whether to fail the entire QA request if follow-up generation fails.
    /// If false, returns QA response with null followUpQuestions.
    /// </summary>
    public bool FailOnGenerationError { get; set; } = false;

    /// <summary>
    /// Cache key version for follow-up questions (increment on schema changes).
    /// </summary>
    public int CacheVersion { get; set; } = 2;
}
