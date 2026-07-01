using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// #2632 (SI-1b): the Session IDs that advance a libro-game campaign (ownership-enforced via
/// <see cref="GetGamebookCampaignQuery"/>). Consumed by the GameManagement spine query, which
/// resolves each to its owning GameNight — liveness/timing live on the <c>GameNightSession</c>
/// (Status/StartedAt), NOT on <c>Session.StartedAt</c> (never set outside <c>OpenLiveMode</c>),
/// so only the IDs are returned here.
/// </summary>
public sealed record ListGamebookCampaignSessionsQuery(Guid CampaignId, Guid CallerUserId)
    : IRequest<IReadOnlyList<Guid>>;
