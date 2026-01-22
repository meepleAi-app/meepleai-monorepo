using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.ToggleBadgeDisplay;

/// <summary>
/// Command to toggle the display visibility of a user's badge.
/// Issue #2736: API - Badge Endpoints
/// </summary>
/// <param name="UserBadgeId">The ID of the user badge assignment to update</param>
/// <param name="UserId">The ID of the user who owns the badge</param>
/// <param name="IsDisplayed">Whether the badge should be displayed on profile</param>
internal sealed record ToggleBadgeDisplayCommand(
    Guid UserBadgeId,
    Guid UserId,
    bool IsDisplayed
) : ICommand;
