namespace Api.BoundedContexts.KnowledgeBase.Domain.ValueObjects;

/// <summary>
/// Value object representing the result of move validation by Arbitro agent.
/// </summary>
/// <remarks>
/// Immutable result returned after validating a move against game rules.
/// Issue #3759: Rules Arbitration Engine
/// </remarks>
internal sealed record MoveValidationResult
{
    public bool IsValid { get; }
    public string Reason { get; }
    public IReadOnlyList<Guid> AppliedRuleIds { get; }
    public double ConfidenceScore { get; }
    public IReadOnlyList<string> Citations { get; }
    public string? ErrorMessage { get; }
    public double ExecutionTimeMs { get; }

    private MoveValidationResult(
        bool isValid,
        string reason,
        IReadOnlyList<Guid> appliedRuleIds,
        double confidenceScore,
        IReadOnlyList<string> citations,
        double executionTimeMs,
        string? errorMessage = null)
    {
        if (string.IsNullOrWhiteSpace(reason))
            throw new ArgumentException("Validation reason cannot be empty", nameof(reason));

        if (confidenceScore < 0 || confidenceScore > 1)
            throw new ArgumentOutOfRangeException(nameof(confidenceScore), "Confidence must be between 0 and 1");

        if (executionTimeMs < 0)
            throw new ArgumentOutOfRangeException(nameof(executionTimeMs), "Execution time cannot be negative");

        IsValid = isValid;
        Reason = reason.Trim();
        AppliedRuleIds = appliedRuleIds ?? Array.Empty<Guid>();
        ConfidenceScore = confidenceScore;
        Citations = citations ?? Array.Empty<string>();
        ExecutionTimeMs = executionTimeMs;
        ErrorMessage = errorMessage?.Trim();
    }

    /// <summary>
    /// Creates a valid move result.
    /// </summary>
    public static MoveValidationResult Valid(
        string reason,
        IReadOnlyList<Guid> appliedRuleIds,
        double confidenceScore,
        IReadOnlyList<string> citations,
        double executionTimeMs)
    {
        return new MoveValidationResult(
            isValid: true,
            reason: reason,
            appliedRuleIds: appliedRuleIds,
            confidenceScore: confidenceScore,
            citations: citations,
            executionTimeMs: executionTimeMs);
    }

    /// <summary>
    /// Creates an invalid move result.
    /// </summary>
    public static MoveValidationResult Invalid(
        string reason,
        IReadOnlyList<Guid> appliedRuleIds,
        double confidenceScore,
        IReadOnlyList<string> citations,
        double executionTimeMs)
    {
        return new MoveValidationResult(
            isValid: false,
            reason: reason,
            appliedRuleIds: appliedRuleIds,
            confidenceScore: confidenceScore,
            citations: citations,
            executionTimeMs: executionTimeMs);
    }

    /// <summary>
    /// Creates an error result when validation fails.
    /// </summary>
    public static MoveValidationResult Error(string errorMessage, double executionTimeMs)
    {
        return new MoveValidationResult(
            isValid: false,
            reason: "Validation error occurred",
            appliedRuleIds: Array.Empty<Guid>(),
            confidenceScore: 0,
            citations: Array.Empty<string>(),
            executionTimeMs: executionTimeMs,
            errorMessage: errorMessage);
    }
}
