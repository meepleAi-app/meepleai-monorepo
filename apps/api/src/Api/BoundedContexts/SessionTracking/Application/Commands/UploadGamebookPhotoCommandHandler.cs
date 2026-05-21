using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

internal sealed class UploadGamebookPhotoCommandHandler : IRequestHandler<UploadGamebookPhotoCommand, GamebookPhotoArtifactDto>
{
    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly IGamebookPhotoArtifactRepository _photos;
    private readonly IGamebookPhotoStorage _storage;

    public UploadGamebookPhotoCommandHandler(
        IGamebookCampaignSessionRepository campaigns,
        IGamebookPhotoArtifactRepository photos,
        IGamebookPhotoStorage storage)
    {
        _campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
        _photos = photos ?? throw new ArgumentNullException(nameof(photos));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
    }

    public async Task<GamebookPhotoArtifactDto> Handle(UploadGamebookPhotoCommand cmd, CancellationToken cancellationToken)
    {
        var campaign = await _campaigns.GetByIdAsync(cmd.CampaignId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Campaign {cmd.CampaignId} not found");

        if (campaign.OwnerUserId != cmd.CallerUserId)
            throw new ForbiddenException("Caller is not the campaign owner");

        // Pre-compute a photoId for use as the storage path component
        var photoId = Guid.NewGuid();
        var storageKey = await _storage.UploadAsync(
            cmd.PhotoStream, cmd.ContentType, cmd.CampaignId, photoId, cancellationToken).ConfigureAwait(false);

        var artifact = GamebookPhotoArtifact.Create(cmd.CampaignId, cmd.GameBookId, storageKey);

        await _photos.AddAsync(artifact, cancellationToken).ConfigureAwait(false);
        await _photos.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return MapToDto(artifact);
    }

    internal static GamebookPhotoArtifactDto MapToDto(GamebookPhotoArtifact a) => new(
        a.Id,
        a.CampaignId,
        a.Status.ToString(),
        a.OcrFullText,
        a.Segments.Select(s => new GamebookSegmentDto(s.ParagraphNumber, s.SourceText, s.BoundingBox)).ToList().AsReadOnly(),
        a.FailureReason,
        a.CreatedAt,
        a.ExpiresAt);
}
