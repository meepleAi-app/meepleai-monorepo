using Api.BoundedContexts.KnowledgeBase.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Handler for GetChatAnalyticsQuery.
/// Issue #3714: Aggregated chat analytics from ChatThreads table.
/// Messages stored as JSON so message counts use string parsing in-memory.
/// </summary>
internal class GetChatAnalyticsQueryHandler : IRequestHandler<GetChatAnalyticsQuery, ChatAnalyticsDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetChatAnalyticsQueryHandler> _logger;

    public GetChatAnalyticsQueryHandler(MeepleAiDbContext dbContext, ILogger<GetChatAnalyticsQueryHandler> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    public async Task<ChatAnalyticsDto> Handle(GetChatAnalyticsQuery request, CancellationToken cancellationToken)
    {
        var fromDate = DateTime.UtcNow.AddDays(-request.TimeRangeDays);

        var baseQuery = _dbContext.ChatThreads.AsNoTracking().AsQueryable();
        var timeFilteredQuery = baseQuery.Where(t => t.CreatedAt >= fromDate);

        // Thread counts
        var totalThreads = await timeFilteredQuery.CountAsync(cancellationToken).ConfigureAwait(false);
        var activeThreads = await timeFilteredQuery.CountAsync(t => t.Status == "active", cancellationToken).ConfigureAwait(false);
        var closedThreads = await timeFilteredQuery.CountAsync(t => t.Status == "closed", cancellationToken).ConfigureAwait(false);

        // Unique users
        var uniqueUsers = await timeFilteredQuery
            .Select(t => t.UserId)
            .Distinct()
            .CountAsync(cancellationToken).ConfigureAwait(false);

        // Threads by agent type
        var agentTypeGroups = await timeFilteredQuery
            .GroupBy(t => t.AgentType ?? "unknown")
            .Select(g => new { AgentType = g.Key, Count = g.Count() })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var threadsByAgentType = agentTypeGroups.ToDictionary(
            g => g.AgentType,
            g => g.Count,
            StringComparer.Ordinal);

        // Message counts - fetch MessagesJson lengths in memory
        // MessagesJson is a JSON array like [{"Id":"..."},{"Id":"..."}]
        // Count array elements by counting top-level objects
        var messagesData = await timeFilteredQuery
            .Select(t => t.MessagesJson)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var totalMessages = 0;
        foreach (var json in messagesData)
        {
            totalMessages += CountJsonArrayElements(json);
        }

        var avgMessagesPerThread = totalThreads > 0 ? (double)totalMessages / totalThreads : 0;

        // Daily stats - threads by day with message counts
        var threadsByDayRaw = await timeFilteredQuery
            .GroupBy(t => t.CreatedAt.Date)
            .Select(g => new
            {
                Date = g.Key,
                TotalCount = g.Count(),
                ActiveCount = g.Count(t => t.Status == "active"),
                ClosedCount = g.Count(t => t.Status == "closed"),
            })
            .OrderBy(d => d.Date)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        // For daily message counts, get per-day data
        var threadsByDayWithMessages = await timeFilteredQuery
            .Select(t => new { t.CreatedAt, t.MessagesJson })
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        var dailyMessageCounts = threadsByDayWithMessages
            .GroupBy(t => DateOnly.FromDateTime(t.CreatedAt))
            .ToDictionary(
                g => g.Key,
                g => g.Sum(t => CountJsonArrayElements(t.MessagesJson)));

        var threadsByDay = threadsByDayRaw.Select(d =>
        {
            var dateOnly = DateOnly.FromDateTime(d.Date);
            dailyMessageCounts.TryGetValue(dateOnly, out var msgCount);
            return new DailyChatStats
            {
                Date = dateOnly,
                TotalCount = d.TotalCount,
                ActiveCount = d.ActiveCount,
                ClosedCount = d.ClosedCount,
                MessageCount = msgCount
            };
        }).ToList();

        _logger.LogInformation(
            "Chat Analytics: {Total} threads, {Messages} messages, {Users} users in last {Days} days",
            totalThreads, totalMessages, uniqueUsers, request.TimeRangeDays);

        return new ChatAnalyticsDto
        {
            TotalThreads = totalThreads,
            ActiveThreads = activeThreads,
            ClosedThreads = closedThreads,
            TotalMessages = totalMessages,
            AvgMessagesPerThread = Math.Round(avgMessagesPerThread, 1),
            UniqueUsers = uniqueUsers,
            ThreadsByAgentType = threadsByAgentType,
            ThreadsByDay = threadsByDay
        };
    }

    /// <summary>
    /// Count elements in a JSON array string without full deserialization.
    /// Counts occurrences of top-level "Id" properties as a proxy for array elements.
    /// Falls back to counting opening braces for simple estimation.
    /// </summary>
    private static int CountJsonArrayElements(string? json)
    {
        if (string.IsNullOrWhiteSpace(json) || string.Equals(json, "[]", StringComparison.Ordinal))
            return 0;

        // Simple approach: count occurrences of "Id" (each message has an Id)
        var count = 0;
        var searchTerm = "\"Id\"";
        var index = 0;
        while (index < json.Length)
        {
            var found = json.IndexOf(searchTerm, index, StringComparison.Ordinal);
            if (found < 0) break;
            count++;
            index = found + searchTerm.Length;
        }

        return count;
    }
}
