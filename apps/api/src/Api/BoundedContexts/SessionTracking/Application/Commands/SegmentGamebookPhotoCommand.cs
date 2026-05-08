using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed record SegmentGamebookPhotoCommand(
    Guid CampaignId,
    Guid PhotoId,
    Guid CallerUserId) : IRequest<GamebookPhotoArtifactDto>;
