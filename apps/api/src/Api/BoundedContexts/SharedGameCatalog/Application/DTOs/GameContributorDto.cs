namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// DTO representing a contributor to a shared game.
/// Includes summary information and top badges for public display.
/// ISSUE-2735: API - Endpoints Contributor Stats
/// </summary>
public sealed record GameContributorDto
{
    /// <summary>
    /// Gets the user ID of the contributor.
    /// </summary>
    public required Guid UserId { get; init; }

    /// <summary>
    /// Gets the username of the contributor.
    /// </summary>
    public required string UserName { get; init; }

    /// <summary>
    /// Gets the optional avatar URL of the contributor.
    /// </summary>
    public string? AvatarUrl { get; init; }

    /// <summary>
    /// Gets whether this is the primary (original) contributor.
    /// </summary>
    public required bool IsPrimaryContributor { get; init; }

    /// <summary>
    /// Gets the total number of contributions made.
    /// </summary>
    public required int ContributionCount { get; init; }

    /// <summary>
    /// Gets the date of the first contribution.
    /// </summary>
    public required DateTime FirstContributionAt { get; init; }

    /// <summary>
    /// Gets the top 3 badges earned by this contributor.
    /// </summary>
    public required List<BadgeSummaryDto> TopBadges { get; init; }
}
