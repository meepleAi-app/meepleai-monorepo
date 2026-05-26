using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for <see cref="GetActiveImpersonationsQuery"/> (SP5 Admin Security S2 — T6).
/// Read-only projection over user_sessions ⋈ users, filtered to active impersonations. Uses the
/// partial index ix_user_sessions_impersonated_by_user_id so it scans only impersonation rows.
/// </summary>
internal sealed class GetActiveImpersonationsQueryHandler
    : IQueryHandler<GetActiveImpersonationsQuery, IReadOnlyList<ImpersonationStatusDto>>
{
    private readonly MeepleAiDbContext _db;
    private readonly TimeProvider _timeProvider;

    public GetActiveImpersonationsQueryHandler(MeepleAiDbContext db, TimeProvider timeProvider)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<IReadOnlyList<ImpersonationStatusDto>> Handle(
        GetActiveImpersonationsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var now = _timeProvider.GetUtcNow().UtcDateTime;

        // 1. Materialize active impersonation sessions (matches the partial index filter on
        // impersonated_by_user_id). This set is small by construction (active impersonations only),
        // so a follow-up email lookup is cheap and avoids a nullable-key join EF can't translate.
        var sessionsQuery = _db.UserSessions
            .AsNoTracking()
            .Where(s => s.ImpersonatedByUserId != null
                     && s.RevokedAt == null
                     && s.ImpersonatedUntil != null
                     && s.ImpersonatedUntil > now);

        if (query.FilterByAdminUserId is { } adminId)
        {
            sessionsQuery = sessionsQuery.Where(s => s.ImpersonatedByUserId == adminId);
        }

        var sessions = await sessionsQuery
            .OrderBy(s => s.CreatedAt)
            .Select(s => new
            {
                s.Id,
                ActorId = s.ImpersonatedByUserId!.Value,
                SubjectId = s.UserId,
                s.CreatedAt,
                Until = s.ImpersonatedUntil!.Value,
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (sessions.Count == 0)
        {
            return [];
        }

        // 2. Resolve actor + subject emails in a single IN-query.
        var userIds = sessions
            .SelectMany(s => new[] { s.ActorId, s.SubjectId })
            .Distinct()
            .ToList();
        var emailById = await _db.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.Email })
            .ToDictionaryAsync(u => u.Id, u => u.Email, cancellationToken)
            .ConfigureAwait(false);

        return sessions
            .Select(s => new ImpersonationStatusDto(
                s.Id,
                s.ActorId,
                emailById.GetValueOrDefault(s.ActorId, "(unknown)"),
                s.SubjectId,
                emailById.GetValueOrDefault(s.SubjectId, "(unknown)"),
                s.CreatedAt,
                s.Until))
            .ToList();
    }
}
