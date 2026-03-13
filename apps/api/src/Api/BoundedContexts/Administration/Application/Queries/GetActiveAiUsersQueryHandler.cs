using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for GetActiveAiUsersQuery.
/// Aggregates user activity across AI chat, PDF uploads, and agent interactions.
/// Issue #113: MAU Monitoring Dashboard.
/// </summary>
internal sealed class GetActiveAiUsersQueryHandler
    : IQueryHandler<GetActiveAiUsersQuery, ActiveAiUsersResult>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetActiveAiUsersQueryHandler> _logger;

    public GetActiveAiUsersQueryHandler(
        MeepleAiDbContext dbContext,
        ILogger<GetActiveAiUsersQueryHandler> logger)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<ActiveAiUsersResult> Handle(
        GetActiveAiUsersQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var periodDays = query.PeriodDays is 7 or 30 or 90 ? query.PeriodDays : 30;
        var periodEnd = DateTime.UtcNow;
        var periodStart = periodEnd.AddDays(-periodDays);

        _logger.LogInformation(
            "Getting active AI users for period {PeriodDays} days ({Start} to {End})",
            periodDays, periodStart, periodEnd);

        // AI Chat users: users who sent chat messages in the period
        var aiChatUserIds = await _dbContext.AiRequestLogs
            .Where(r => r.CreatedAt >= periodStart && r.CreatedAt <= periodEnd && r.UserId != null)
            .Select(r => r.UserId!.Value)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // PDF Upload users: users who uploaded PDFs in the period
        var pdfUploadUserIds = await _dbContext.PdfDocuments
            .Where(p => p.UploadedAt >= periodStart && p.UploadedAt <= periodEnd)
            .Select(p => p.UploadedByUserId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Agent users: users who interacted with agents (via agent sessions)
        var agentUserIds = await _dbContext.AgentSessions
            .Where(s => s.StartedAt >= periodStart && s.StartedAt <= periodEnd)
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        // Total unique active users
        var allActiveUserIds = aiChatUserIds
            .Union(pdfUploadUserIds)
            .Union(agentUserIds)
            .Distinct()
            .ToList();

        // Daily breakdown for trend chart
        var dailyBreakdown = new List<DailyActiveUsersDto>();
        for (var day = periodStart.Date; day <= periodEnd.Date; day = day.AddDays(1))
        {
            var dayEnd = day.AddDays(1);

            var dayAiChatUsers = await _dbContext.AiRequestLogs
                .Where(r => r.CreatedAt >= day && r.CreatedAt < dayEnd && r.UserId != null)
                .Select(r => r.UserId!.Value)
                .Distinct()
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);

            var dayPdfUsers = await _dbContext.PdfDocuments
                .Where(p => p.UploadedAt >= day && p.UploadedAt < dayEnd)
                .Select(p => p.UploadedByUserId)
                .Distinct()
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);

            var dayTotalUsers = dayAiChatUsers + dayPdfUsers; // Approximate without dedup for perf

            dailyBreakdown.Add(new DailyActiveUsersDto(
                day,
                dayTotalUsers,
                dayAiChatUsers,
                dayPdfUsers));
        }

        return new ActiveAiUsersResult(
            allActiveUserIds.Count,
            aiChatUserIds.Count,
            pdfUploadUserIds.Count,
            agentUserIds.Count,
            periodDays,
            periodStart,
            periodEnd,
            dailyBreakdown);
    }
}
