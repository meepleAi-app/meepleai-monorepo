namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Represents the category of a badge, indicating what type of achievement it recognizes.
/// </summary>
public enum BadgeCategory
{
    /// <summary>
    /// Badges based on contribution count.
    /// Awarded for reaching contribution milestones.
    /// </summary>
    Contribution = 0,

    /// <summary>
    /// Badges based on quality metrics.
    /// Awarded for high approval rates and clean submissions.
    /// </summary>
    Quality = 1,

    /// <summary>
    /// Badges based on activity and engagement.
    /// Awarded for consistent participation.
    /// </summary>
    Engagement = 2,

    /// <summary>
    /// Special badges for unique accomplishments.
    /// Manually awarded or event-based achievements.
    /// </summary>
    Special = 3
}
