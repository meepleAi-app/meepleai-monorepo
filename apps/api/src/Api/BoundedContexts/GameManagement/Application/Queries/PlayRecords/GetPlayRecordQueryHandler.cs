using System.Linq;
using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Services;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.Services.Pdf;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;

/// <summary>
/// Handles retrieving a single play record with full details.
/// Issue #3890: CQRS queries for play records.
/// Issue #2436 PR-C: Photos presigned read-path.
/// </summary>
internal class GetPlayRecordQueryHandler : IQueryHandler<GetPlayRecordQuery, PlayRecordDto>
{
    private const int PresignExpirySeconds = 3600;

    private readonly MeepleAiDbContext _context;
    private readonly IBlobStorageService _blobStorage;

    public GetPlayRecordQueryHandler(MeepleAiDbContext context, IBlobStorageService blobStorage)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _blobStorage = blobStorage ?? throw new ArgumentNullException(nameof(blobStorage));
    }

    public async Task<PlayRecordDto> Handle(GetPlayRecordQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entity = await _context.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players)
                .ThenInclude(p => p.Scores)
            .Include(r => r.Photos)
            .FirstOrDefaultAsync(r => r.Id == query.RecordId, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
            throw new NotFoundException("PlayRecord", query.RecordId.ToString());

        if (entity.CreatedByUserId != query.UserId
            && !entity.Players.Any(p => p.UserId == query.UserId))
        {
            throw new ForbiddenException("You do not have permission to view this play record.");
        }

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
            var url = await PlayRecordPhotoUrlResolver.ResolveAsync(_blobStorage, p.BlobUrl, PresignExpirySeconds).ConfigureAwait(false);
            var thumb = p.ThumbnailUrl is null
                ? null
                : await PlayRecordPhotoUrlResolver.ResolveAsync(_blobStorage, p.ThumbnailUrl, PresignExpirySeconds).ConfigureAwait(false);
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
            photos
        );
    }
}
