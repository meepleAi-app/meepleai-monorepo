using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

/// <summary>
/// Session Flow v2.1 — Plan 1bis T4 handler.
/// Queries <c>SessionTrackingSessions</c> for the most-recently-updated Active or
/// Paused session owned by the requesting user. Optionally resolves the
/// GameNight envelope via the <c>GameNightSessions</c> link table.
/// </summary>
internal sealed class GetCurrentSessionQueryHandler
    : IRequestHandler<GetCurrentSessionQuery, CurrentSessionDto?>
{
    private readonly MeepleAiDbContext _db;

    public GetCurrentSessionQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<CurrentSessionDto?> Handle(
        GetCurrentSessionQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await _db.SessionTrackingSessions
            .AsNoTracking()
            .Where(s => s.UserId == request.UserId
                        && (s.Status == "Active" || s.Status == "Paused")
                        && !s.IsDeleted)
            .OrderByDescending(s => s.UpdatedAt ?? s.SessionDate)
            .Select(s => new
            {
                s.Id,
                GameId = s.GameId ?? Guid.Empty,
                s.Status,
                s.SessionCode,
                s.SessionDate,
                s.UpdatedAt
            })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (session is null)
            return null;

        // Resolve optional GameNight envelope.
        var gameNightId = await _db.GameNightSessions
            .AsNoTracking()
            .Where(gns => gns.SessionId == session.Id)
            .Select(gns => (Guid?)gns.GameNightEventId)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return new CurrentSessionDto(
            session.Id,
            session.GameId,
            session.Status,
            session.SessionCode,
            session.SessionDate,
            session.UpdatedAt,
            gameNightId);
    }
}
