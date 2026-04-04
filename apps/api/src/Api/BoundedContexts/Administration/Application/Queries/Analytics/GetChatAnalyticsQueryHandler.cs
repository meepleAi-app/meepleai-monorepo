using Api.BoundedContexts.Administration.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries.Analytics;

/// <summary>
/// Handler for chat analytics query.
/// Queries ChatThread and ChatLog entities for totals and 7-day histogram.
/// </summary>
internal class GetChatAnalyticsQueryHandler : IQueryHandler<GetChatAnalyticsQuery, ChatAnalyticsDto>
{
    private readonly MeepleAiDbContext _db;

    public GetChatAnalyticsQueryHandler(MeepleAiDbContext db)
    {
        _db = db ?? throw new ArgumentNullException(nameof(db));
    }

    public async Task<ChatAnalyticsDto> Handle(GetChatAnalyticsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var totalThreads = await _db.Set<ChatThreadEntity>().AsNoTracking()
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var totalMessages = await _db.Set<ChatLogEntity>().AsNoTracking()
            .CountAsync(cancellationToken).ConfigureAwait(false);

        var avgMessagesPerThread = totalThreads > 0
            ? (double)totalMessages / totalThreads
            : 0.0;

        // 7-day message histogram
        var sevenDaysAgo = DateTime.UtcNow.Date.AddDays(-6);
        var dailyCounts = await _db.Set<ChatLogEntity>().AsNoTracking()
            .Where(m => m.CreatedAt >= sevenDaysAgo)
            .GroupBy(m => m.CreatedAt.Date)
            .Select(g => new { Date = g.Key, Count = g.Count() })
            .OrderBy(x => x.Date)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // Fill in missing days with zero counts
        var messagesLast7Days = new List<DailyCountDto>();
        for (var day = 0; day < 7; day++)
        {
            var date = sevenDaysAgo.AddDays(day);
            var dateOnly = DateOnly.FromDateTime(date);
            var count = dailyCounts.FirstOrDefault(d => d.Date == date)?.Count ?? 0;
            messagesLast7Days.Add(new DailyCountDto(dateOnly, count));
        }

        return new ChatAnalyticsDto(
            TotalThreads: totalThreads,
            TotalMessages: totalMessages,
            AvgMessagesPerThread: Math.Round(avgMessagesPerThread, 2),
            MessagesLast7Days: messagesLast7Days);
    }
}
