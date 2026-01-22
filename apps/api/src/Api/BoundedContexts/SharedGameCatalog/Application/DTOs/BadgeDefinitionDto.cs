using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// DTO representing a badge definition/type available in the system.
/// </summary>
public sealed record BadgeDefinitionDto
{
    /// <summary>
    /// Gets the unique identifier of the badge definition.
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
    /// Gets the badge category (Contribution, Quality, Engagement, Special).
    /// </summary>
    public required BadgeCategory Category { get; init; }

    /// <summary>
    /// Gets the human-readable description of how to earn this badge.
    /// </summary>
    public required string RequirementDescription { get; init; }
}
