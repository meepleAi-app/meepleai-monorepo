using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed record CreateGamebookCampaignCommand(Guid GameId, Guid OwnerUserId, string Title)
    : IRequest<GamebookCampaignDto>;
