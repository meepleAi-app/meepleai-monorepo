namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Represents the type of requirement for earning a badge.
/// </summary>
public enum BadgeRequirementType
{
    /// <summary>
    /// Requirement based on number of approved contributions.
    /// </summary>
    ContributionCount = 0,

    /// <summary>
    /// Requirement based on number of documents contributed.
    /// </summary>
    DocumentCount = 1,

    /// <summary>
    /// Requirement based on consecutive approvals without changes requested.
    /// </summary>
    QualityStreak = 2,

    /// <summary>
    /// Requirement based on ranking in the contributor leaderboard.
    /// </summary>
    TopContributor = 3,

    /// <summary>
    /// Requirement for the very first contribution.
    /// </summary>
    FirstContribution = 4,

    /// <summary>
    /// Custom requirement with complex evaluation logic.
    /// </summary>
    Custom = 5
}
