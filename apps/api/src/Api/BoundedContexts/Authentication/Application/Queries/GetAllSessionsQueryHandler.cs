using Api.Infrastructure;
using Api.Models;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Authentication.Application.Queries;

public class GetAllSessionsQueryHandler : IQueryHandler<GetAllSessionsQuery, List<SessionInfo>>
{
    private readonly MeepleAiDbContext _db;

    public GetAllSessionsQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<List<SessionInfo>> Handle(GetAllSessionsQuery request, CancellationToken cancellationToken)
    {
        if (request.Limit <= 0 || request.Limit > 1000)
        {
            throw new ArgumentException("Limit must be between 1 and 1000", nameof(request.Limit));
        }

        var query = _db.UserSessions
            .AsNoTracking() // PERF-05: Read-only query for admin listing
            .Include(s => s.User)
            .AsQueryable();

        if (request.UserId.HasValue)
        {
            query = query.Where(s => s.UserId == request.UserId.Value);
        }

        var sessions = await query
            .OrderByDescending(s => s.CreatedAt)
            .Take(request.Limit)
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
            .ToListAsync(cancellationToken);

        return sessions;
    }
}
