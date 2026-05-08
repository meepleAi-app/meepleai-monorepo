using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

internal sealed class SegmentGamebookPhotoCommandHandler : IRequestHandler<SegmentGamebookPhotoCommand, GamebookPhotoArtifactDto>
{
    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly IGamebookPhotoArtifactRepository _photos;
    private readonly IGamebookPhotoStorage _storage;
    private readonly IOcrService _ocr;

    public SegmentGamebookPhotoCommandHandler(
        IGamebookCampaignSessionRepository campaigns,
        IGamebookPhotoArtifactRepository photos,
        IGamebookPhotoStorage storage,
        IOcrService ocr)
    {
        _campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
        _photos = photos ?? throw new ArgumentNullException(nameof(photos));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
    }

    public async Task<GamebookPhotoArtifactDto> Handle(SegmentGamebookPhotoCommand cmd, CancellationToken cancellationToken)
    {
        var campaign = await _campaigns.GetByIdAsync(cmd.CampaignId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Campaign {cmd.CampaignId} not found");

        if (campaign.OwnerUserId != cmd.CallerUserId)
            throw new ConflictException("Caller is not the campaign owner");

        var artifact = await _photos.GetByIdAsync(cmd.PhotoId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException($"Photo {cmd.PhotoId} not found");

        if (artifact.CampaignId != cmd.CampaignId)
            throw new ConflictException("Photo does not belong to this campaign");

        // Idempotent: if already segmented (or beyond), return current state without re-running OCR
        if (artifact.Status != PhotoArtifactStatus.Uploaded)
            return UploadGamebookPhotoCommandHandler.MapToDto(artifact);

        using var stream = await _storage.RetrieveAsync(artifact.S3Key, cancellationToken).ConfigureAwait(false);

        try
        {
            var ocr = await _ocr.ExtractAsync(stream, cancellationToken).ConfigureAwait(false);
            var segments = ocr.Paragraphs
                .Select(p => GamebookSegment.Create(p.Number, p.Text, p.Bbox?.ToString()))
                .ToList();
            artifact.RecordSegments(segments, ocr.FullText);
        }
        catch (Exception ex)
        {
            artifact.MarkFailed($"OCR: {ex.Message}");
        }

        await _photos.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return UploadGamebookPhotoCommandHandler.MapToDto(artifact);
    }
}
