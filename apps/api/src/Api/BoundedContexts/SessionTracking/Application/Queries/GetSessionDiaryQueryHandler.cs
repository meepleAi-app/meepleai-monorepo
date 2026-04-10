using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

// Resolve the T9 DTO explicitly — a legacy 7-arg SessionEventDto also lives in the
// Queries namespace (Issue #276) and would shadow the T9 8-arg record inside this file.
using DiaryEventDto = Api.BoundedContexts.SessionTracking.Application.DTOs.SessionEventDto;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Session Flow v2.1 — T9 handler.
/// Reads <c>SessionEventEntity</c> rows for a single session and projects them
/// onto <see cref="DiaryEventDto"/>. Soft-deleted events are excluded.
/// </summary>
internal sealed class GetSessionDiaryQueryHandler
    : IQueryHandler<GetSessionDiaryQuery, IReadOnlyList<DiaryEventDto>>
{
    private readonly MeepleAiDbContext _db;

    public GetSessionDiaryQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<DiaryEventDto>> Handle(
        GetSessionDiaryQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = _db.SessionEvents
            .AsNoTracking()
            .Where(e => e.SessionId == request.SessionId && !e.IsDeleted);

        if (request.EventTypes is { Count: > 0 })
        {
            var types = request.EventTypes;
            query = query.Where(e => types.Contains(e.EventType));
        }

        if (request.Since.HasValue)
        {
            var since = request.Since.Value;
            query = query.Where(e => e.Timestamp >= since);
        }

        var entries = await query
            .OrderBy(e => e.Timestamp)
            .ThenBy(e => e.Id)
            .Take(request.Limit)
            .Select(e => new DiaryEventDto(
                e.Id,
                e.SessionId,
                e.GameNightId,
                e.EventType,
                e.Timestamp,
                e.Payload,
                e.CreatedBy,
                e.Source))
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entries;
    }
}
