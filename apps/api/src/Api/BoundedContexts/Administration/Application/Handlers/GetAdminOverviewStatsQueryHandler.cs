using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GetAdminOverviewStatsQuery.
/// Issue #4198: Lightweight overview stats for StatsOverview component.
/// Issue #113: Added ActiveAiUsers for MAU-AI monitoring.
/// </summary>
internal class GetAdminOverviewStatsQueryHandler : IQueryHandler<GetAdminOverviewStatsQuery, AdminOverviewStatsDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly HybridCache _cache;
    private readonly TimeProvider _timeProvider;
    private readonly ILlmRequestLogRepository _llmRequestLogRepository;
    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(1);

    public GetAdminOverviewStatsQueryHandler(
        MeepleAiDbContext dbContext,
        HybridCache cache,
        ILlmRequestLogRepository llmRequestLogRepository,
        TimeProvider? timeProvider = null)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _llmRequestLogRepository = llmRequestLogRepository ?? throw new ArgumentNullException(nameof(llmRequestLogRepository));
        _timeProvider = timeProvider ?? TimeProvider.System;
    }

    public async Task<AdminOverviewStatsDto> Handle(GetAdminOverviewStatsQuery query, CancellationToken cancellationToken)
    {
        return await _cache.GetOrCreateAsync<AdminOverviewStatsDto>(
            "admin:overview-stats",
            async cancel =>
            {
                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var thirtyDaysAgo = now.AddDays(-30);
                var sevenDaysAgo = now.AddDays(-7);

                // Game counts
                var totalGames = await _dbContext.Games
                    .AsNoTracking()
                    .CountAsync(cancel).ConfigureAwait(false);

                var publishedGames = await _dbContext.Games
                    .AsNoTracking()
                    .Where(g => g.IsPublished)
                    .CountAsync(cancel).ConfigureAwait(false);

                // User counts
                var totalUsers = await _dbContext.Users
                    .AsNoTracking()
                    .CountAsync(cancel).ConfigureAwait(false);

                // Active users: users with a session seen in the last 30 days
                var activeUsers = await _dbContext.UserSessions
                    .AsNoTracking()
                    .Where(s => s.RevokedAt == null && s.LastSeenAt != null && s.LastSeenAt >= thirtyDaysAgo)
                    .Select(s => s.UserId)
                    .Distinct()
                    .CountAsync(cancel).ConfigureAwait(false);

                // Issue #113: Active AI users — distinct users with >=1 LLM request in last 30 days
                var activeAiUsers = await _llmRequestLogRepository
                    .GetActiveAiUserCountAsync(thirtyDaysAgo, cancel).ConfigureAwait(false);

                // Share request / approval stats
                var shareRequests = _dbContext.Set<ShareRequestEntity>();

                var totalRequests = await shareRequests
                    .AsNoTracking()
                    .CountAsync(cancel).ConfigureAwait(false);

                var approvedRequests = await shareRequests
                    .AsNoTracking()
                    .Where(sr => sr.Status == 3) // 3 = Approved
                    .CountAsync(cancel).ConfigureAwait(false);

                var pendingApprovals = await shareRequests
                    .AsNoTracking()
                    .Where(sr => sr.Status == 0 || sr.Status == 1) // 0=Pending, 1=InReview
                    .CountAsync(cancel).ConfigureAwait(false);

                var recentSubmissions = await shareRequests
                    .AsNoTracking()
                    .Where(sr => sr.CreatedAt >= sevenDaysAgo)
                    .CountAsync(cancel).ConfigureAwait(false);

                var approvalRate = totalRequests > 0
                    ? Math.Round((double)approvedRequests / totalRequests * 100, 1)
                    : 0.0;

                return new AdminOverviewStatsDto(
                    TotalGames: totalGames,
                    PublishedGames: publishedGames,
                    TotalUsers: totalUsers,
                    ActiveUsers: activeUsers,
                    ActiveAiUsers: activeAiUsers,
                    ApprovalRate: approvalRate,
                    PendingApprovals: pendingApprovals,
                    RecentSubmissions: recentSubmissions);
            },
            new HybridCacheEntryOptions
            {
                Expiration = CacheDuration,
                LocalCacheExpiration = TimeSpan.FromSeconds(30)
            },
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }
}
