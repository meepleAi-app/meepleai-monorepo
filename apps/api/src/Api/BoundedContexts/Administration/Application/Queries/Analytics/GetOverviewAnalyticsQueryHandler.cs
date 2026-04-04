using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.Analytics;

/// <summary>
/// Handler for overview analytics query.
/// Counts total users, games, PDF documents, and chat threads.
/// </summary>
internal class GetOverviewAnalyticsQueryHandler : IQueryHandler<GetOverviewAnalyticsQuery, OverviewAnalyticsDto>
{
    private readonly MeepleAiDbContext _db;

    public GetOverviewAnalyticsQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<OverviewAnalyticsDto> Handle(GetOverviewAnalyticsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var todayUtc = DateTime.UtcNow.Date;

        var totalUsers = await _db.Set<UserEntity>().AsNoTracking()
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var totalGames = await _db.Set<GameEntity>().AsNoTracking()
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var totalDocuments = await _db.Set<PdfDocumentEntity>().AsNoTracking()
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var totalChats = await _db.Set<ChatThreadEntity>().AsNoTracking()
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var todayNewUsers = await _db.Set<UserEntity>().AsNoTracking()
            .CountAsync(u => u.CreatedAt >= todayUtc, cancellationToken).ConfigureAwait(false);

        var todayNewChats = await _db.Set<ChatThreadEntity>().AsNoTracking()
            .CountAsync(t => t.CreatedAt >= todayUtc, cancellationToken).ConfigureAwait(false);

        return new OverviewAnalyticsDto(
            TotalUsers: totalUsers,
            TotalGames: totalGames,
            TotalDocuments: totalDocuments,
            TotalChats: totalChats,
            TodayNewUsers: todayNewUsers,
            TodayNewChats: todayNewChats);
    }
}
