using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed record UpdateGamebookProgressCommand(Guid CampaignId, Guid CallerUserId, int CurrentParagraph)
    : IRequest<GamebookCampaignDto>;
