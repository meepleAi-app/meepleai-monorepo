using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.BoundedContexts.SessionTracking.Domain.Enums;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
using Api.BoundedContexts.SessionTracking.Domain.ValueObjects;
using Api.BoundedContexts.SessionTracking.Infrastructure.Caching;
using Api.BoundedContexts.SessionTracking.Infrastructure.Services;
using Api.Middleware.Exceptions;
using Api.Observability;
using Api.Services;
using MediatR;
// Alias to disambiguate from Api.Services.ILanguageDetectionService (unrelated shared-kernel service)
using ILangDetect = Api.BoundedContexts.SessionTracking.Infrastructure.Services.ILanguageDetectionService;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

internal sealed class SegmentGamebookPhotoCommandHandler : IRequestHandler<SegmentGamebookPhotoCommand, GamebookPhotoArtifactDto>
{
    private readonly IGamebookCampaignSessionRepository _campaigns;
    private readonly IGamebookPhotoArtifactRepository _photos;
    private readonly IGamebookPhotoStorage _storage;
    private readonly IOcrService _ocr;
    private readonly ILangDetect _langDetection;
    private readonly IHybridCacheService _cache;

    public SegmentGamebookPhotoCommandHandler(
        IGamebookCampaignSessionRepository campaigns,
        IGamebookPhotoArtifactRepository photos,
        IGamebookPhotoStorage storage,
        IOcrService ocr,
        ILangDetect langDetection,
        IHybridCacheService cache)
    {
        _campaigns = campaigns ?? throw new ArgumentNullException(nameof(campaigns));
        _photos = photos ?? throw new ArgumentNullException(nameof(photos));
        _storage = storage ?? throw new ArgumentNullException(nameof(storage));
        _ocr = ocr ?? throw new ArgumentNullException(nameof(ocr));
        _langDetection = langDetection ?? throw new ArgumentNullException(nameof(langDetection));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
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

            // #1559 DEC-2: lang detection on full OCR text per-photo.
            // Failures must not block OCR persistence (try/catch; service contract also guarantees no-throw,
            // but we defensively guard here too for belt-and-suspenders).
            LanguageDetectionResult langResult;
            try
            {
                langResult = _langDetection.Detect(ocr.FullText);
            }
            catch
            {
                langResult = new LanguageDetectionResult(null, 0.0);
            }

            // Null-coerce lang confidence: only persist it when detection actually succeeded
            // (i.e. a non-null language was returned). This preserves backward-compat for
            // pre-#1559 photos and for detection failures where Lang=null, Confidence=0.0.
            double? persistedConfidence = langResult.Lang is not null ? langResult.Confidence : null;

            artifact.RecordSegments(
                segments,
                ocr.FullText,
                detectedSourceLang: langResult.Lang,
                langDetectionConfidence: persistedConfidence);

            // #1559 DEC-5/6/7: cache OCR + lang snapshot for translate handler (TTL 24h).
            // Uses GetOrCreateAsync with a pre-computed factory so the value is always stored.
            var cacheValue = new PhotoOcrCacheValue(
                FullText: ocr.FullText,
                Paragraphs: ocr.Paragraphs.Select(p => new OcrParagraphCacheEntry(p.Number, p.Text)).ToList(),
                AverageConfidence: ocr.AverageConfidence,
                DetectedSourceLang: langResult.Lang,
                LangDetectionConfidence: persistedConfidence);

            await _cache.GetOrCreateAsync(
                cacheKey: GamebookCacheKeys.PhotoOcr(artifact.Id),
                factory: _ => Task.FromResult(cacheValue),
                tags: null,
                expiration: TimeSpan.FromHours(24),
                ct: cancellationToken).ConfigureAwait(false);

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
