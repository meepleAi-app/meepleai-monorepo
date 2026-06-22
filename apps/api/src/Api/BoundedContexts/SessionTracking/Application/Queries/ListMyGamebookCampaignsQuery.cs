using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public sealed record ListMyGamebookCampaignsQuery(Guid CallerUserId, Guid? GameId) : IRequest<IReadOnlyList<GamebookCampaignDto>>;
