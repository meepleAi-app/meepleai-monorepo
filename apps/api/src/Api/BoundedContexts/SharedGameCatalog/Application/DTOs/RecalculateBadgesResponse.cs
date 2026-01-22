namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Response DTO for badge recalculation operations.
/// </summary>
public sealed record RecalculateBadgesResponse
{
    /// <summary>
    /// Gets the number of users processed during recalculation.
    /// </summary>
    public required int UsersProcessed { get; init; }

    /// <summary>
    /// Gets the number of badges awarded during recalculation.
    /// </summary>
    public required int BadgesAwarded { get; init; }

    /// <summary>
    /// Gets the number of badges revoked during recalculation.
    /// </summary>
    public required int BadgesRevoked { get; init; }
}
