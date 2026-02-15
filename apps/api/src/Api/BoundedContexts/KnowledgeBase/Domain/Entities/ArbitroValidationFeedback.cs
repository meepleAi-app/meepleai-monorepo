using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Entity representing user feedback on Arbitro Agent move validation results.
/// Issue #4328: Arbitro Agent Beta Testing and User Feedback Iteration.
/// </summary>
/// <remarks>
/// Tracks user feedback for accuracy measurement and model improvement:
/// - Rating: 1-5 star rating of validation usefulness
/// - Accuracy assessment: Whether AI decision was correct (user ground truth)
/// - Comment: Optional text feedback
/// - Links to original validation via ValidationId (correlation ID)
/// </remarks>
internal sealed class ArbitroValidationFeedback : AggregateRoot<Guid>
{
    /// <summary>
    /// Correlation ID linking to the original validation request.
    /// Generated during validation and returned in MoveValidationResultDto.
    /// </summary>
    public Guid ValidationId { get; private set; }

    /// <summary>
    /// Game session where the validation occurred.
    /// </summary>
    public Guid GameSessionId { get; private set; }

    /// <summary>
    /// Player who submitted the feedback (may differ from move player).
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// User's rating of the validation (1-5 stars).
    /// 1 = Very poor, 5 = Excellent.
    /// </summary>
    public int Rating { get; private set; }

    /// <summary>
    /// User's assessment of decision accuracy.
    /// - Correct: AI decision matched actual rules
    /// - Incorrect: AI decision was wrong
    /// - Uncertain: User not sure / ambiguous case
    /// </summary>
    public AccuracyAssessment Accuracy { get; private set; }

    /// <summary>
    /// Optional text comment from user.
    /// </summary>
    public string? Comment { get; private set; }

    /// <summary>
    /// AI's original decision (VALID/INVALID/UNCERTAIN) for comparison.
    /// Captured at feedback submission for accuracy analysis.
    /// </summary>
    public string AiDecision { get; private set; }

    /// <summary>
    /// AI's original confidence score (0.0-1.0) for correlation analysis.
    /// </summary>
    public double AiConfidence { get; private set; }

    /// <summary>
    /// Whether conflicts were detected during validation.
    /// </summary>
    public bool HadConflicts { get; private set; }

    /// <summary>
    /// When the feedback was submitted.
    /// </summary>
    public DateTime SubmittedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private ArbitroValidationFeedback() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates new feedback for an Arbitro validation.
    /// </summary>
    public static ArbitroValidationFeedback Create(
        Guid validationId,
        Guid gameSessionId,
        Guid userId,
        int rating,
        AccuracyAssessment accuracy,
        string aiDecision,
        double aiConfidence,
        bool hadConflicts,
        string? comment = null,
        TimeProvider? timeProvider = null)
    {
        if (validationId == Guid.Empty)
            throw new ArgumentException("ValidationId cannot be empty", nameof(validationId));
        if (gameSessionId == Guid.Empty)
            throw new ArgumentException("GameSessionId cannot be empty", nameof(gameSessionId));
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
        if (rating < 1 || rating > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5");
        if (string.IsNullOrWhiteSpace(aiDecision))
            throw new ArgumentException("AI decision cannot be empty", nameof(aiDecision));
        if (aiConfidence < 0 || aiConfidence > 1)
            throw new ArgumentOutOfRangeException(nameof(aiConfidence), "AI confidence must be between 0 and 1");

        var provider = timeProvider ?? TimeProvider.System;

        var feedback = new ArbitroValidationFeedback
        {
            Id = Guid.NewGuid(),
            ValidationId = validationId,
            GameSessionId = gameSessionId,
            UserId = userId,
            Rating = rating,
            Accuracy = accuracy,
            Comment = comment?.Trim(),
            AiDecision = aiDecision.Trim().ToUpperInvariant(),
            AiConfidence = aiConfidence,
            HadConflicts = hadConflicts,
            SubmittedAt = provider.GetUtcNow().UtcDateTime
        };

        return feedback;
    }

    /// <summary>
    /// Updates the comment text.
    /// </summary>
    public void UpdateComment(string newComment)
    {
        Comment = newComment?.Trim();
    }
}

/// <summary>
/// User's assessment of AI decision accuracy.
/// </summary>
internal enum AccuracyAssessment
{
    /// <summary>
    /// AI decision was correct according to user's understanding.
    /// </summary>
    Correct,

    /// <summary>
    /// AI decision was incorrect / violated actual rules.
    /// </summary>
    Incorrect,

    /// <summary>
    /// User uncertain / ambiguous case / conflicting interpretations.
    /// </summary>
    Uncertain
}
