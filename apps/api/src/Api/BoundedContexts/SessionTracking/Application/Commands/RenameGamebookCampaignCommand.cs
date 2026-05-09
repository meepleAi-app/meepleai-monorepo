using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed record RenameGamebookCampaignCommand(Guid CampaignId, Guid CallerUserId, string Title)
    : IRequest<GamebookCampaignDto>;
