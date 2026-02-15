using Api.SharedKernel.Domain.Entities;

namespace Api.BoundedContexts.KnowledgeBase.Domain.Entities;

/// <summary>
/// Entity representing user feedback on Decisore Agent move suggestions.
/// Issue #4335: Decisore Agent Beta Testing and User Feedback Iteration.
/// </summary>
/// <remarks>
/// Tracks user feedback for move suggestion quality and win correlation:
/// - Rating: 1-5 star rating of suggestion quality
/// - Quality assessment: Whether suggestion was helpful/harmful/neutral
/// - Outcome: Actual game outcome (win/loss/draw) for correlation analysis
/// - Links to original suggestion via SuggestionId (correlation ID)
/// </remarks>
internal sealed class DecisoreMoveFeedback : AggregateRoot<Guid>
{
    /// <summary>
    /// Correlation ID linking to the original analysis/suggestion.
    /// Generated during analysis and returned in StrategicAnalysisResultDto.
    /// </summary>
    public Guid SuggestionId { get; private set; }

    /// <summary>
    /// Game session where the suggestion was made.
    /// </summary>
    public Guid GameSessionId { get; private set; }

    /// <summary>
    /// Player who submitted the feedback.
    /// </summary>
    public Guid UserId { get; private set; }

    /// <summary>
    /// User's rating of the suggestion quality (1-5 stars).
    /// 1 = Very poor, 5 = Excellent.
    /// </summary>
    public int Rating { get; private set; }

    /// <summary>
    /// User's assessment of move quality.
    /// - Helpful: Suggestion led to improved position
    /// - Neutral: Suggestion was okay but not impactful
    /// - Harmful: Suggestion worsened position
    /// </summary>
    public MoveQualityAssessment Quality { get; private set; }

    /// <summary>
    /// Optional text comment from user.
    /// </summary>
    public string? Comment { get; private set; }

    /// <summary>
    /// Actual game outcome (for win correlation analysis).
    /// Win/Loss/Draw/InProgress
    /// </summary>
    public GameOutcome Outcome { get; private set; }

    /// <summary>
    /// Whether the user followed the suggested move.
    /// Critical for correlation: only followed suggestions impact outcome.
    /// </summary>
    public bool SuggestionFollowed { get; private set; }

    /// <summary>
    /// AI's top suggested move (for comparison if not followed).
    /// </summary>
    public string TopSuggestedMove { get; private set; }

    /// <summary>
    /// AI's position evaluation score (-1 to +1).
    /// </summary>
    public double PositionStrength { get; private set; }

    /// <summary>
    /// Analysis depth used (beginner/intermediate/expert).
    /// </summary>
    public string AnalysisDepth { get; private set; }

    /// <summary>
    /// When the feedback was submitted.
    /// </summary>
    public DateTime SubmittedAt { get; private set; }

    /// <summary>
    /// Private constructor for EF Core.
    /// </summary>
#pragma warning disable CS8618
    private DecisoreMoveFeedback() : base()
#pragma warning restore CS8618
    {
    }

    /// <summary>
    /// Creates new feedback for a Decisore move suggestion.
    /// </summary>
    public static DecisoreMoveFeedback Create(
        Guid suggestionId,
        Guid gameSessionId,
        Guid userId,
        int rating,
        MoveQualityAssessment quality,
        GameOutcome outcome,
        bool suggestionFollowed,
        string topSuggestedMove,
        double positionStrength,
        string analysisDepth,
        string? comment = null,
        TimeProvider? timeProvider = null)
    {
        if (suggestionId == Guid.Empty)
            throw new ArgumentException("SuggestionId cannot be empty", nameof(suggestionId));
        if (gameSessionId == Guid.Empty)
            throw new ArgumentException("GameSessionId cannot be empty", nameof(gameSessionId));
        if (userId == Guid.Empty)
            throw new ArgumentException("UserId cannot be empty", nameof(userId));
        if (rating < 1 || rating > 5)
            throw new ArgumentOutOfRangeException(nameof(rating), "Rating must be between 1 and 5");
        if (string.IsNullOrWhiteSpace(topSuggestedMove))
            throw new ArgumentException("Top suggested move cannot be empty", nameof(topSuggestedMove));
        if (positionStrength < -1 || positionStrength > 1)
            throw new ArgumentOutOfRangeException(nameof(positionStrength), "Position strength must be between -1 and 1");
        if (string.IsNullOrWhiteSpace(analysisDepth))
            throw new ArgumentException("Analysis depth cannot be empty", nameof(analysisDepth));

        var provider = timeProvider ?? TimeProvider.System;

        var feedback = new DecisoreMoveFeedback
        {
            Id = Guid.NewGuid(),
            SuggestionId = suggestionId,
            GameSessionId = gameSessionId,
            UserId = userId,
            Rating = rating,
            Quality = quality,
            Comment = comment?.Trim(),
            Outcome = outcome,
            SuggestionFollowed = suggestionFollowed,
            TopSuggestedMove = topSuggestedMove.Trim(),
            PositionStrength = positionStrength,
            AnalysisDepth = analysisDepth.Trim().ToLowerInvariant(),
            SubmittedAt = provider.GetUtcNow().UtcDateTime
        };

        return feedback;
    }

    /// <summary>
    /// Updates the game outcome (called when game finishes).
    /// </summary>
    public void UpdateOutcome(GameOutcome newOutcome)
    {
        Outcome = newOutcome;
    }
}

/// <summary>
/// User's assessment of move suggestion quality.
/// </summary>
internal enum MoveQualityAssessment
{
    /// <summary>
    /// Suggestion improved position / led to advantage.
    /// </summary>
    Helpful,

    /// <summary>
    /// Suggestion was okay but not impactful.
    /// </summary>
    Neutral,

    /// <summary>
    /// Suggestion worsened position / led to disadvantage.
    /// </summary>
    Harmful
}

/// <summary>
/// Actual game outcome for correlation analysis.
/// </summary>
internal enum GameOutcome
{
    /// <summary>
    /// Player won the game.
    /// </summary>
    Win,

    /// <summary>
    /// Player lost the game.
    /// </summary>
    Loss,

    /// <summary>
    /// Game ended in draw.
    /// </summary>
    Draw,

    /// <summary>
    /// Game still in progress (feedback submitted mid-game).
    /// </summary>
    InProgress
}
