using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for GetAuditLogsQuery. Returns paginated, filtered audit log entries.
/// Issue #3691: Audit Log System.
/// </summary>
internal class GetAuditLogsQueryHandler : IQueryHandler<GetAuditLogsQuery, AuditLogListResult>
{
    private readonly MeepleAiDbContext _db;

    public GetAuditLogsQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<AuditLogListResult> Handle(GetAuditLogsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var query = _db.AuditLogs
            .AsNoTracking()
            .AsQueryable();

        // Apply filters
        if (request.AdminUserId.HasValue)
        {
            query = query.Where(a => a.UserId == request.AdminUserId.Value);
        }

        if (!string.IsNullOrWhiteSpace(request.Action))
        {
            query = query.Where(a => a.Action == request.Action);
        }

        if (!string.IsNullOrWhiteSpace(request.Resource))
        {
            query = query.Where(a => a.Resource == request.Resource);
        }

        if (!string.IsNullOrWhiteSpace(request.Result))
        {
            query = query.Where(a => a.Result == request.Result);
        }

        if (request.StartDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt >= request.StartDate.Value);
        }

        if (request.EndDate.HasValue)
        {
            query = query.Where(a => a.CreatedAt <= request.EndDate.Value);
        }

        var totalCount = await query.CountAsync(cancellationToken).ConfigureAwait(false);

        var entries = await query
            .OrderByDescending(a => a.CreatedAt)
            .Skip(request.Offset)
            .Take(request.Limit)
            .GroupJoin(
                _db.Users,
                audit => audit.UserId,
                user => user.Id,
                (audit, users) => new { audit, users })
            .SelectMany(
                g => g.users.DefaultIfEmpty(),
                (g, user) => new AuditLogDto(
                    g.audit.Id,
                    g.audit.UserId,
                    g.audit.Action,
                    g.audit.Resource,
                    g.audit.ResourceId,
                    g.audit.Result,
                    g.audit.Details,
                    g.audit.IpAddress,
                    g.audit.CreatedAt,
                    user != null ? user.DisplayName : null,
                    user != null ? user.Email : null))
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return new AuditLogListResult(entries, totalCount, request.Limit, request.Offset);
    }
}
