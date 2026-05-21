using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.Observability;
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
            throw new ForbiddenException("Caller is not the campaign owner");

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

            // Issue #833: gamebook OCR metrics.
            // - confidence: single mean per page (Tesseract returns 0..100; normalise to 0..1 for histogram bucket compat).
            // - segmentation match quality: "exact" if paragraphs were extracted, "miss" if zero. "partial" is deferred
            //   until segmentation has a quality classifier (out of scope per spec-panel 2026-05-18).
            var normalisedConfidence = Math.Clamp(ocr.AverageConfidence / 100.0, 0.0, 1.0);
            var exactMatches = segments.Count > 0 ? 1L : 0L;
            var misses = segments.Count == 0 ? 1L : 0L;
            MeepleAiMetrics.RecordGamebookOcrSegmentation(
                confidenceScores: new[] { normalisedConfidence },
                language: "ita",
                exactMatches: exactMatches,
                partialMatches: 0,
                misses: misses);
        }
        catch (Exception ex)
        {
            artifact.MarkFailed($"OCR: {ex.Message}");

            // Issue #833: record OCR failure as a "miss" with zero confidence.
            MeepleAiMetrics.RecordGamebookOcrSegmentation(
                confidenceScores: Array.Empty<double>(),
                language: "ita",
                exactMatches: 0,
                partialMatches: 0,
                misses: 1);
        }

        await _photos.SaveChangesAsync(cancellationToken).ConfigureAwait(false);

        return UploadGamebookPhotoCommandHandler.MapToDto(artifact);
    }
}
