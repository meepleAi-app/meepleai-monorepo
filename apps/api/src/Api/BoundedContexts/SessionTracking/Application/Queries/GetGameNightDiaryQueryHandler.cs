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
/// Returns the unified diary for a <c>GameNightEvent</c>, spanning every
/// session attached to that night. Soft-deleted events are excluded.
/// </summary>
internal sealed class GetGameNightDiaryQueryHandler
    : IQueryHandler<GetGameNightDiaryQuery, IReadOnlyList<DiaryEventDto>>
{
    private readonly MeepleAiDbContext _db;

    public GetGameNightDiaryQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<IReadOnlyList<DiaryEventDto>> Handle(
        GetGameNightDiaryQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        // C2 fix: verify caller is the night organizer
        var organizerId = await _db.GameNightEvents
            .AsNoTracking()
            .Where(g => g.Id == request.GameNightEventId)
            .Select(g => (Guid?)g.OrganizerId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (organizerId is null)
            throw new Api.Middleware.Exceptions.NotFoundException($"GameNightEvent {request.GameNightEventId} not found.");

        if (organizerId.Value != request.RequesterId)
            throw new Api.Middleware.Exceptions.ForbiddenException("Only the night organizer can read its diary.");

        var query = _db.SessionEvents
            .AsNoTracking()
            .Where(e => e.GameNightId == request.GameNightEventId && !e.IsDeleted);

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
