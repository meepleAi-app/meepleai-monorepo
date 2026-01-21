using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Domain.Services;

/// <summary>
/// Domain service interface for evaluating badge eligibility.
/// Determines which badges a user is eligible to earn based on their contribution history.
/// </summary>
public interface IBadgeEvaluator
{
    /// <summary>
    /// Evaluates all badges a user is currently eligible to earn.
    /// </summary>
    /// <param name="userId">The ID of the user to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>A list of badges the user is eligible to earn but hasn't earned yet.</returns>
    Task<IReadOnlyList<Badge>> EvaluateEligibleBadgesAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Checks if a specific badge requirement is met by a user.
    /// </summary>
    /// <param name="userId">The ID of the user to check.</param>
    /// <param name="requirement">The badge requirement to evaluate.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>True if the user meets the requirement, false otherwise.</returns>
    Task<bool> CheckRequirementAsync(
        Guid userId,
        BadgeRequirement requirement,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets statistics relevant to badge evaluation for a user.
    /// </summary>
    /// <param name="userId">The ID of the user.</param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>Badge evaluation statistics for the user.</returns>
    Task<BadgeEvaluationStats> GetUserStatsAsync(
        Guid userId,
        CancellationToken cancellationToken = default);
}

/// <summary>
/// Statistics relevant to badge evaluation for a user.
/// </summary>
public record BadgeEvaluationStats
{
    /// <summary>
    /// Gets the total number of approved contributions.
    /// </summary>
    public int TotalApprovedContributions { get; init; }

    /// <summary>
    /// Gets the total number of documents contributed.
    /// </summary>
    public int TotalDocumentsContributed { get; init; }

    /// <summary>
    /// Gets the current streak of consecutive approvals without changes requested.
    /// </summary>
    public int CurrentQualityStreak { get; init; }

    /// <summary>
    /// Gets the user's current rank in the contributor leaderboard.
    /// Null if not ranked.
    /// </summary>
    public int? LeaderboardRank { get; init; }

    /// <summary>
    /// Gets the approval rate (0.0 to 1.0).
    /// </summary>
    public decimal ApprovalRate { get; init; }

    /// <summary>
    /// Gets whether the user has made their first contribution.
    /// </summary>
    public bool HasFirstContribution { get; init; }

    /// <summary>
    /// Creates an empty stats instance for users with no contributions.
    /// </summary>
    public static BadgeEvaluationStats Empty => new()
    {
        TotalApprovedContributions = 0,
        TotalDocumentsContributed = 0,
        CurrentQualityStreak = 0,
        LeaderboardRank = null,
        ApprovalRate = 0m,
        HasFirstContribution = false
    };
}
