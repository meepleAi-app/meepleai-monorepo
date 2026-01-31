using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// DTO representing a single entry in the badge leaderboard.
/// </summary>
public sealed record LeaderboardEntryDto
{
    /// <summary>
    /// Gets the rank of this user in the leaderboard (1-indexed).
    /// </summary>
    public required int Rank { get; init; }

    /// <summary>
    /// Gets the user ID.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// Gets the username.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Gets the optional avatar URL.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// Gets the total number of approved contributions.
    /// </summary>
    public required int ContributionCount { get; init; }

    /// <summary>
    /// Gets the total number of badges earned.
    /// </summary>
    public required int BadgeCount { get; init; }

    /// <summary>
    /// Gets the highest tier badge earned by this user.
    /// </summary>
    public required BadgeTier HighestBadgeTier { get; init; }

    /// <summary>
    /// Gets the top 3 badges for this user (ordered by tier descending).
    /// </summary>
    public required List<UserBadgeDto> TopBadges { get; init; }
}
