namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// Represents the tier level of a badge, indicating its prestige and difficulty to earn.
/// </summary>
public enum BadgeTier
{
    /// <summary>
    /// Entry-level badge for initial achievements.
    /// Easiest to earn, typically for first-time accomplishments.
    /// </summary>
    Bronze = 0,

    /// <summary>
    /// Intermediate badge for moderate achievements.
    /// Requires consistent contribution effort.
    /// </summary>
    Silver = 1,

    /// <summary>
    /// Advanced badge for significant achievements.
    /// Requires notable dedication and quality.
    /// </summary>
    Gold = 2,

    /// <summary>
    /// Expert-level badge for exceptional achievements.
    /// Reserved for highly dedicated contributors.
    /// </summary>
    Platinum = 3,

    /// <summary>
    /// Legendary badge for extraordinary achievements.
    /// The highest tier, reserved for outstanding community members.
    /// </summary>
    Diamond = 4
}
