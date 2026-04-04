using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.Analytics;

/// <summary>
/// Handler for MAU query.
/// Counts distinct users with active sessions in the last 30 days (MAU)
/// and last 24 hours (DAU). Retention is estimated as DAU/MAU ratio.
/// </summary>
internal class GetMauQueryHandler : IQueryHandler<GetMauQuery, MauDto>
{
    private readonly MeepleAiDbContext _db;

    public GetMauQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<MauDto> Handle(GetMauQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var now = DateTime.UtcNow;
        var thirtyDaysAgo = now.AddDays(-30);
        var oneDayAgo = now.AddDays(-1);

        // MAU: distinct users with a session seen in last 30 days
        var mau = await _db.Set<UserSessionEntity>().AsNoTracking()
            .Where(s => s.RevokedAt == null && (s.LastSeenAt ?? s.CreatedAt) >= thirtyDaysAgo)
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync(cancellationToken).ConfigureAwait(false);

        // DAU: distinct users with a session seen in last 24 hours
        var dau = await _db.Set<UserSessionEntity>().AsNoTracking()
            .Where(s => s.RevokedAt == null && (s.LastSeenAt ?? s.CreatedAt) >= oneDayAgo)
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync(cancellationToken).ConfigureAwait(false);

        // Retention rate: DAU / MAU (a common stickiness metric)
        var retentionRate = mau > 0
            ? Math.Round((double)dau / mau * 100, 2)
            : 0.0;

        return new MauDto(
            MonthlyActiveUsers: mau,
            DailyActiveUsers: dau,
            RetentionRate: retentionRate);
    }
}
