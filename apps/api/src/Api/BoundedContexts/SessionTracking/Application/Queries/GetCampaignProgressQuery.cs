using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Issue #1388: returns the per-book progress rows for a campaign so the FE can
/// populate the "Resume Books" list on the play page. Ownership is enforced in
/// the handler — non-owners receive <see cref="Api.Middleware.Exceptions.ForbiddenException"/>
/// (issue #1404: was <c>ConflictException</c>, now mapped to HTTP 403).
/// </summary>
public sealed record GetCampaignProgressQuery(Guid CampaignId, Guid CallerUserId)
    : IRequest<IReadOnlyList<SessionBookProgressDto>>;
