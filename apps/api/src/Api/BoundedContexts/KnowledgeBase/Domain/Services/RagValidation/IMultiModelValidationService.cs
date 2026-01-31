

#pragma warning disable MA0048 // File name must match type name - Contains Service with Configuration classes
namespace Api.BoundedContexts.KnowledgeBase.Domain.Services;

/// <summary>
/// Domain service for multi-model consensus validation (GPT-4 + Claude)
/// ISSUE-974: BGAI-032 - Multi-model validation with consensus threshold ≥0.90
/// ISSUE-975: BGAI-033 - Consensus similarity calculation using cosine ≥0.90
/// </summary>
/// <remarks>
/// Validates AI responses by querying multiple LLM models and comparing their outputs.
/// Achieves consensus when similarity between responses meets the threshold (≥0.90).
///
/// Architecture:
/// - Queries both GPT-4 and Claude models independently
/// - Calculates text similarity using TF-IDF cosine similarity
/// - Returns consensus result with similarity score and combined response
///
/// Use cases:
/// - Critical board game rule clarifications
/// - High-stakes questions requiring extra validation
/// - Quality assurance for production responses
/// </remarks>
internal interface IMultiModelValidationService
{
    /// <summary>
    /// Validate a query against multiple models and check for consensus
    /// </summary>
    /// <param name="systemPrompt">System-level instructions for both models</param>
    /// <param name="userPrompt">User's question to validate</param>
    /// <param name="temperature">Sampling temperature (default: 0.3 for consistency)</param>
    /// <param name="maxTokens">Maximum tokens to generate (default: 1000)</param>
    /// <param name="cancellationToken">Cancellation token</param>
    /// <returns>Consensus validation result with similarity score and responses</returns>
    Task<MultiModelConsensusResult> ValidateWithConsensusAsync(
        string systemPrompt,
        string userPrompt,
        double temperature = 0.3,
        int maxTokens = 1000,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Calculate text similarity between two responses (0.0-1.0)
    /// </summary>
    /// <param name="text1">First text to compare</param>
    /// <param name="text2">Second text to compare</param>
    /// <returns>Similarity score (0.0 = completely different, 1.0 = identical)</returns>
    double CalculateSimilarity(string text1, string text2);

    /// <summary>
    /// Get the current consensus threshold (≥0.90)
    /// </summary>
    double ConsensusThreshold { get; }
}

/// <summary>
/// Result of multi-model consensus validation
/// </summary>
internal record MultiModelConsensusResult
{
    /// <summary>
    /// Whether consensus was achieved (similarity ≥ threshold)
    /// </summary>
    public required bool HasConsensus { get; init; }

    /// <summary>
    /// Similarity score between model responses (0.0-1.0)
    /// </summary>
    public required double SimilarityScore { get; init; }

    /// <summary>
    /// Minimum similarity threshold required for consensus
    /// </summary>
    public required double RequiredThreshold { get; init; }

    /// <summary>
    /// Response from GPT-4 model
    /// </summary>
    public required ModelResponse Gpt4Response { get; init; }

    /// <summary>
    /// Response from Claude model
    /// </summary>
    public required ModelResponse ClaudeResponse { get; init; }

    /// <summary>
    /// Combined/merged response (when consensus achieved)
    /// </summary>
    public string? ConsensusResponse { get; init; }

    /// <summary>
    /// Validation message
    /// </summary>
    public required string Message { get; init; }

    /// <summary>
    /// Total time taken for validation (milliseconds)
    /// </summary>
    public required long TotalDurationMs { get; init; }

    /// <summary>
    /// Severity level for logging/alerting
    /// </summary>
    public required ConsensusSeverity Severity { get; init; }
}

/// <summary>
/// Individual model response details
/// </summary>
internal record ModelResponse
{
    /// <summary>
    /// Model identifier used (e.g., "openai/gpt-4o", "anthropic/claude-3.5-sonnet")
    /// </summary>
    public required string ModelId { get; init; }

    /// <summary>
    /// Generated response text
    /// </summary>
    public required string ResponseText { get; init; }

    /// <summary>
    /// Whether the request was successful
    /// </summary>
    public required bool IsSuccess { get; init; }

    /// <summary>
    /// Error message if request failed
    /// </summary>
    public string? ErrorMessage { get; init; }

    /// <summary>
    /// Duration in milliseconds
    /// </summary>
    public required long DurationMs { get; init; }

    /// <summary>
    /// Token usage statistics (from Api.Services.LlmUsage)
    /// </summary>
    public Api.Services.LlmUsage? Usage { get; init; }
}

/// <summary>
/// Severity level for consensus validation
/// </summary>
internal enum ConsensusSeverity
{
    /// <summary>
    /// High consensus achieved (similarity ≥0.90)
    /// </summary>
    High,

    /// <summary>
    /// Moderate consensus (similarity 0.70-0.90)
    /// </summary>
    Moderate,

    /// <summary>
    /// Low consensus (similarity 0.50-0.70)
    /// </summary>
    Low,

    /// <summary>
    /// No consensus (similarity &lt;0.50)
    /// </summary>
    None,

    /// <summary>
    /// One or more models failed to respond
    /// </summary>
    Error
}
