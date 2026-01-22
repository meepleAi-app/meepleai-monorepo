using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// DTO representing a badge earned by a user.
/// </summary>
public sealed record UserBadgeDto
{
    /// <summary>
    /// Gets the unique identifier of the user badge assignment.
    /// </summary>
    public required Guid Id { get; init; }

    /// <summary>
    /// Gets the badge code (stable identifier like "FIRST_CONTRIBUTION").
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the badge display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the badge description.
    /// </summary>
    public required string Description { get; init; }

    /// <summary>
    /// Gets the optional icon URL.
    /// </summary>
    public string? IconUrl { get; init; }

    /// <summary>
    /// Gets the badge tier (Bronze, Silver, Gold, Platinum, Diamond).
    /// </summary>
    public required BadgeTier Tier { get; init; }

    /// <summary>
    /// Gets the date and time when the badge was earned.
    /// </summary>
    public required DateTime EarnedAt { get; init; }

    /// <summary>
    /// Gets whether the badge is displayed on the user's profile.
    /// </summary>
    public required bool IsDisplayed { get; init; }
}
