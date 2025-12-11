namespace Api.BoundedContexts.Administration.Application.DTOs;

/// <summary>
/// Statistics about agent feedback for analytics and monitoring.
/// Provides aggregated metrics on user feedback for AI agent responses.
/// </summary>
public record FeedbackStatsDto
{
    /// <summary>
    /// Total number of feedback submissions.
    /// </summary>
    public required int TotalFeedbacks { get; init; }

    /// <summary>
    /// Number of "helpful" feedback responses.
    /// </summary>
    public required int HelpfulCount { get; init; }

    /// <summary>
    /// Number of "not-helpful" feedback responses.
    /// </summary>
    public required int NotHelpfulCount { get; init; }

    /// <summary>
    /// Rate of helpful feedback (0.0 to 1.0).
    /// Calculated as HelpfulCount / TotalFeedbacks.
    /// </summary>
    public required double HelpfulRate { get; init; }

    /// <summary>
    /// Feedback counts grouped by endpoint.
    /// Key: endpoint name, Value: count of feedback for that endpoint.
    /// </summary>
    public required IDictionary<string, int> FeedbackByEndpoint { get; init; }

    /// <summary>
    /// Feedback counts grouped by outcome type.
    /// Key: outcome ("helpful", "not-helpful"), Value: count.
    /// </summary>
    public required IDictionary<string, int> FeedbackByOutcome { get; init; }
}