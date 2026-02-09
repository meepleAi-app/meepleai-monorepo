using Api.BoundedContexts.GameManagement.Application.DTOs.PlayRecords;
using Api.BoundedContexts.GameManagement.Application.Queries.PlayRecords;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.PlayRecords;

/// <summary>
/// Handles retrieving a single play record with full details.
/// Issue #3890: CQRS queries for play records.
/// </summary>
internal class GetPlayRecordQueryHandler : IQueryHandler<GetPlayRecordQuery, PlayRecordDto>
{
    private readonly MeepleAiDbContext _context;

    public GetPlayRecordQueryHandler(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<PlayRecordDto> Handle(GetPlayRecordQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var entity = await _context.PlayRecords
            .AsNoTracking()
            .Include(r => r.Players)
                .ThenInclude(p => p.Scores)
            .FirstOrDefaultAsync(r => r.Id == query.RecordId, cancellationToken)
            .ConfigureAwait(false);

        if (entity == null)
            throw new NotFoundException("PlayRecord", query.RecordId.ToString());

        // Deserialize outside expression tree to avoid optional parameter issues
        var scoringConfig = System.Text.Json.JsonSerializer.Deserialize<SessionScoringConfigDto>(entity.ScoringConfigJson)
            ?? new SessionScoringConfigDto(new List<string>(), new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase));

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
                )).ToList()
            )).ToList(),
            scoringConfig,
            entity.CreatedByUserId,
            (Domain.Enums.PlayRecordVisibility)entity.Visibility,
            entity.StartTime,
            entity.EndTime,
            entity.Notes,
            entity.Location,
            entity.CreatedAt,
            entity.UpdatedAt
        );
    }
}
