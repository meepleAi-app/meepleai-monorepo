using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Queries;

public class GetUserSessionsQueryHandler : IQueryHandler<GetUserSessionsQuery, List<SessionInfo>>
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public GetUserSessionsQueryHandler(MeepleAiDbContext db, TimeProvider? timeProvider = null)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<List<SessionInfo>> Handle(GetUserSessionsQuery request, CancellationToken cancellationToken)
    {
        var now = _timeProvider.GetUtcNow().UtcDateTime;

        var sessions = await _db.UserSessions
            .AsNoTracking() // PERF-05: Read-only query for listing sessions
            .Include(s => s.User)
            .Where(s => s.UserId == request.UserId && s.RevokedAt == null && s.ExpiresAt > now)
            .OrderByDescending(s => s.LastSeenAt ?? s.CreatedAt)
            .Select(s => new SessionInfo(
                s.Id.ToString(),
                s.UserId.ToString(),
                s.User.Email,
                s.CreatedAt,
                s.ExpiresAt,
                s.LastSeenAt,
                s.RevokedAt,
                s.IpAddress,
                s.UserAgent
            ))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return sessions;
    }
}
