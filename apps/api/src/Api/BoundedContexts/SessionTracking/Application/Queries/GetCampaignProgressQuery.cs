using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Issue #1388: returns the per-book progress rows for a campaign so the FE can
/// populate the "Resume Books" list on the play page. Ownership is enforced in
/// the handler — non-owners receive <see cref="Api.Middleware.Exceptions.ConflictException"/>.
/// </summary>
public sealed record GetCampaignProgressQuery(Guid CampaignId, Guid CallerUserId)
    : IRequest<IReadOnlyList<SessionBookProgressDto>>;
