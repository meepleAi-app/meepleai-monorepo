using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public sealed record GetGamebookHistoryQuery(Guid CampaignId, Guid CallerUserId)
    : IRequest<IReadOnlyList<TranslatedParagraphDto>>;
