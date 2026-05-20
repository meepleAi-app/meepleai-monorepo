using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed record UploadGamebookPhotoCommand(
    Guid CampaignId,
    Guid GameBookId,
    Guid CallerUserId,
    Stream PhotoStream,
    string ContentType) : IRequest<GamebookPhotoArtifactDto>;
