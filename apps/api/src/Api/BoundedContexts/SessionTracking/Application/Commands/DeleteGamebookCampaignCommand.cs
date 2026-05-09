using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed record DeleteGamebookCampaignCommand(Guid CampaignId, Guid CallerUserId) : IRequest;
