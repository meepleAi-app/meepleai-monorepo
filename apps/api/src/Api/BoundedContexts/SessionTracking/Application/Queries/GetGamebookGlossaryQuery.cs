using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public sealed record GetGamebookGlossaryQuery(Guid CampaignId, Guid CallerUserId)
    : IRequest<IReadOnlyList<GamebookGlossaryEntryDto>>;
