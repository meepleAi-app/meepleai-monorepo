using System.Linq;
using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.Infrastructure.Entities.GameManagement;
using Api.Services.Pdf;

namespace Api.BoundedContexts.GameManagement.Application.Services;

/// <summary>
/// Shared mapper that converts a <see cref="PlayRecordEntity"/> (with loaded navigations) into
/// a fully-populated <see cref="PlayRecordDto"/>, including:
/// <list type="bullet">
///   <item>ScoringConfig JSON deserialization</item>
///   <item>Winner/outcome computation via <see cref="PlayRecordOutcomeCalculator"/></item>
///   <item>Photo presigned-URL resolution via <see cref="PlayRecordPhotoUrlResolver"/></item>
///   <item>SessionPlayerDto projection</item>
///   <item>ShareToken passthrough (null when not shared)</item>
/// </list>
/// Extracted from <c>GetPlayRecordQueryHandler</c> so the anonymous
/// <c>GetPlayRecordByShareTokenQueryHandler</c> can reuse it without duplicating logic.
/// Issue #2437-2.
/// </summary>
internal static class PlayRecordDtoMapper
{
    public static async Task<PlayRecordDto> MapAsync(
        PlayRecordEntity entity,
        IBlobStorageService blobStorage,
        int presignExpirySeconds,
        CancellationToken cancellationToken = default)
    {
        // Deserialize outside expression tree to avoid optional parameter issues
        var scoringConfig = System.Text.Json.JsonSerializer.Deserialize<SessionScoringConfigDto>(entity.ScoringConfigJson)
            ?? new SessionScoringConfigDto(new List<string>(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

        // Compute outcome fields from player scores (no storage change — computed on read)
        var winnerPlayerIds = PlayRecordOutcomeCalculator.WinnerPlayerIds(entity.Players);
        var outcomeType = PlayRecordOutcomeCalculator.OutcomeType(entity.Players);

        // Map photos with presigned URLs (read path, #2436 PR-C). Ordered oldest→newest.
        var photos = new List<PlayRecordPhotoDto>(entity.Photos.Count);
        foreach (var p in entity.Photos.OrderBy(p => p.UploadedAt))
        {
            var url = await PlayRecordPhotoUrlResolver.ResolveAsync(blobStorage, p.BlobUrl, presignExpirySeconds).ConfigureAwait(false);
            var thumb = p.ThumbnailUrl is null
                ? null
                : await PlayRecordPhotoUrlResolver.ResolveAsync(blobStorage, p.ThumbnailUrl, presignExpirySeconds).ConfigureAwait(false);
            photos.Add(new PlayRecordPhotoDto(p.Id, url, thumb, p.OcrText, p.Caption, p.UploadedByUserId, p.UploadedAt));
        }

        return new PlayRecordDto(
            entity.Id,
            entity.GameId,
            entity.GameName,
            entity.SessionDate,
            entity.Duration,
            (Domain.Enums.PlayRecordStatus)entity.Status,
            entity.Players.Select(p => new SessionPlayerDto(
                p.Id,
                p.UserId,
                p.DisplayName,
                p.Scores.Select(s => new SessionScoreDto(
                    s.Dimension,
                    s.Value,
                    s.Unit
                )).ToList(),
                PlayRecordOutcomeCalculator.TotalScore(p)
            )).ToList(),
            scoringConfig,
            entity.CreatedByUserId,
            (Domain.Enums.PlayRecordVisibility)entity.Visibility,
            entity.StartTime,
            entity.EndTime,
            entity.Notes,
            entity.Location,
            entity.CreatedAt,
            entity.UpdatedAt,
            winnerPlayerIds,
            outcomeType,
            photos,
            entity.Xmin,
            entity.IsShared ? entity.ShareToken : null   // #2437-2: only expose token when currently shared
        );
    }
}
