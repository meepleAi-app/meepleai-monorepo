using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// #2632 (SI-1b): lists the sittings (Sessions) that advance a libro-game campaign, most-recent
/// first. Enforces campaign ownership (via <see cref="GetGamebookCampaignQuery"/>). Consumed by the
/// GameManagement spine query to derive the campaign status + owning GameNight.
/// </summary>
public sealed record ListGamebookCampaignSessionsQuery(Guid CampaignId, Guid CallerUserId)
    : IRequest<IReadOnlyList<GamebookSittingDto>>;

/// <summary>A single gamebook sitting's spine-relevant projection.</summary>
public sealed record GamebookSittingDto(Guid SessionId, bool IsLive, DateTime? StartedAt);
