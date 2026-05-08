using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed record UploadGamebookPhotoCommand(
    Guid CampaignId,
    Guid CallerUserId,
    Stream PhotoStream,
    string ContentType,
    GamebookPageType PageType) : IRequest<GamebookPhotoArtifactDto>;
