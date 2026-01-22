using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Compact DTO for badge summary in contributor displays.
/// Contains minimal information for listing top badges.
/// ISSUE-2735: API - Endpoints Contributor Stats
/// </summary>
public sealed record BadgeSummaryDto
{
    /// <summary>
    /// Gets the badge code (e.g., "TOP_CONTRIBUTOR").
    /// </summary>
    public required string Code { get; init; }

    /// <summary>
    /// Gets the badge display name.
    /// </summary>
    public required string Name { get; init; }

    /// <summary>
    /// Gets the icon URL for the badge.
    /// </summary>
    public required string IconUrl { get; init; }

    /// <summary>
    /// Gets the badge tier level.
    /// </summary>
    public required BadgeTier Tier { get; init; }
}
