using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.GetUserBadges;

/// <summary>
/// Query to get all badges earned by a specific user.
/// Issue #2728: Application - Badge Assignment Handlers
/// </summary>
internal sealed record GetUserBadgesQuery(
    Guid UserId,
    bool IncludeHidden = false
) : IQuery<List<UserBadgeDto>>;
