using System.Diagnostics;
using Api.BoundedContexts.BusinessSimulations.Application.DTOs;
using Api.BoundedContexts.BusinessSimulations.Application.Queries;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.BusinessSimulations.Application.Queries;

/// <summary>
/// Handler for GetAppUsageStatsQuery - calculates app usage statistics.
/// Issue #4562: App Usage Stats API (Epic #3688)
/// Uses HybridCache for performance (15min TTL).
/// </summary>
internal sealed class GetAppUsageStatsQueryHandler : IRequestHandler<GetAppUsageStatsQuery, AppUsageStatsDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly HybridCache _cache;
    private readonly ILogger<GetAppUsageStatsQueryHandler> _logger;
    private readonly TimeProvider _timeProvider;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);

    public GetAppUsageStatsQueryHandler(
        MeepleAiDbContext dbContext,
        HybridCache cache,
        ILogger<GetAppUsageStatsQueryHandler> logger,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _timeProvider = timeProvider ?? throw new ArgumentNullException(nameof(timeProvider));
    }

    public async Task<AppUsageStatsDto> Handle(GetAppUsageStatsQuery query, CancellationToken cancellationToken)
    {
        var cacheKey = $"app-usage-stats:{query.Period}";
        var stopwatch = Stopwatch.StartNew();

        var result = await _cache.GetOrCreateAsync(
            cacheKey,
            async cancel =>
            {
                _logger.LogInformation("Generating app usage stats for period {Period} days", query.Period);

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                var periodStart = now.AddDays(-query.Period);
                var previousPeriodStart = periodStart.AddDays(-query.Period);

                // Parallel execution for performance
                var dauTask = CalculateDauAsync(now, periodStart, previousPeriodStart, cancel);
                var mauTask = CalculateMauAsync(now, periodStart, previousPeriodStart, cancel);
                var sessionsTask = CalculateSessionAnalyticsAsync(periodStart, now, cancel);
                var retentionTask = CalculateRetentionCohortsAsync(now, cancel);
                var funnelTask = CalculateFeatureFunnelAsync(cancel);
                var geoTask = CalculateGeoDistributionAsync(periodStart, now, cancel);

                await Task.WhenAll(dauTask, mauTask, sessionsTask, retentionTask, funnelTask, geoTask)
                    .ConfigureAwait(false);

                return new AppUsageStatsDto(
                    DailyActiveUsers: await dauTask.ConfigureAwait(false),
                    MonthlyActiveUsers: await mauTask.ConfigureAwait(false),
                    Sessions: await sessionsTask.ConfigureAwait(false),
                    Retention: await retentionTask.ConfigureAwait(false),
                    FeatureFunnel: await funnelTask.ConfigureAwait(false),
                    GeoDistribution: await geoTask.ConfigureAwait(false),
                    GeneratedAt: now);
            },
            new HybridCacheEntryOptions { Expiration = CacheDuration },
            cancellationToken: cancellationToken).ConfigureAwait(false);

        stopwatch.Stop();
        _logger.LogInformation(
            "App usage stats retrieved in {ElapsedMs}ms (period: {Period} days)",
            stopwatch.ElapsedMilliseconds, query.Period);

        return result;
    }

    private async Task<DauMauStatsDto> CalculateDauAsync(
        DateTime now,
        DateTime periodStart,
        DateTime previousPeriodStart,
        CancellationToken cancellationToken)
    {
        // DAU = Distinct active users in last 24 hours
        var oneDayAgo = now.AddDays(-1);
        var twoDaysAgo = now.AddDays(-2);

        var currentDau = await _dbContext.UserSessions
            .Where(s => s.CreatedAt >= oneDayAgo && s.CreatedAt < now)
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var previousDau = await _dbContext.UserSessions
            .Where(s => s.CreatedAt >= twoDaysAgo && s.CreatedAt < oneDayAgo)
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var changePercentage = previousDau == 0 ? 0 : ((currentDau - previousDau) / (double)previousDau) * 100;

        return new DauMauStatsDto(currentDau, previousDau, Math.Round(changePercentage, 1));
    }

    private async Task<DauMauStatsDto> CalculateMauAsync(
        DateTime now,
        DateTime periodStart,
        DateTime previousPeriodStart,
        CancellationToken cancellationToken)
    {
        // MAU = Distinct active users in last 30 days
        var thirtyDaysAgo = now.AddDays(-30);
        var sixtyDaysAgo = now.AddDays(-60);

        var currentMau = await _dbContext.UserSessions
            .Where(s => s.CreatedAt >= thirtyDaysAgo && s.CreatedAt < now)
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var previousMau = await _dbContext.UserSessions
            .Where(s => s.CreatedAt >= sixtyDaysAgo && s.CreatedAt < thirtyDaysAgo)
            .Select(s => s.UserId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        var changePercentage = previousMau == 0 ? 0 : ((currentMau - previousMau) / (double)previousMau) * 100;

        return new DauMauStatsDto(currentMau, previousMau, Math.Round(changePercentage, 1));
    }

    private async Task<SessionAnalyticsDto> CalculateSessionAnalyticsAsync(
        DateTime periodStart,
        DateTime now,
        CancellationToken cancellationToken)
    {
        var sessions = await _dbContext.UserSessions
            .Where(s => s.CreatedAt >= periodStart && s.CreatedAt < now)
            .Select(s => new
            {
                s.CreatedAt,
                s.LastSeenAt,
                s.RevokedAt
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var totalSessions = sessions.Count;
        var activeSessions = sessions.Count(s => s.RevokedAt == null);

        // Calculate average duration (only for sessions with LastSeenAt)
        var sessionsWithActivity = sessions.Where(s => s.LastSeenAt.HasValue).ToList();
        var avgDurationMinutes = sessionsWithActivity.Count > 0
            ? sessionsWithActivity.Average(s => (s.LastSeenAt!.Value - s.CreatedAt).TotalMinutes)
            : 0;

        var avgDurationFormatted = avgDurationMinutes > 0
            ? $"{(int)avgDurationMinutes}m {(int)((avgDurationMinutes % 1) * 60)}s"
            : "0m 0s";

        // Bounce rate = sessions without LastSeenAt (no activity after login)
        var bouncedSessions = sessions.Count(s => !s.LastSeenAt.HasValue);
        var bounceRate = totalSessions > 0 ? (bouncedSessions / (double)totalSessions) * 100 : 0;

        return new SessionAnalyticsDto(
            AverageDuration: avgDurationFormatted,
            BounceRatePercentage: Math.Round(bounceRate, 1),
            TotalSessions: totalSessions,
            ActiveSessions: activeSessions);
    }

    private async Task<RetentionCohortDto> CalculateRetentionCohortsAsync(
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Get users who signed up in the cohort period (90 days ago)
        var cohortStart = now.AddDays(-90);

        var cohortUsers = await _dbContext.Users
            .Where(u => u.CreatedAt >= cohortStart && u.CreatedAt < now)
            .Select(u => new { u.Id, u.CreatedAt })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (cohortUsers.Count == 0)
        {
            return new RetentionCohortDto(0, 0, 0, 0);
        }

        // Check retention at 7, 30, 90 days
        var day7Cutoff = now.AddDays(-7);
        var day30Cutoff = now.AddDays(-30);

        var activeUserIds = await _dbContext.UserSessions
            .Where(s => s.CreatedAt >= day7Cutoff)
            .Select(s => s.UserId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        var activeUserIdsSet = activeUserIds.ToHashSet();

        // Day 7 retention: users signed up 7+ days ago still active
        var day7Cohort = cohortUsers.Where(u => u.CreatedAt <= now.AddDays(-7)).ToList();
        var day7Retained = day7Cohort.Count(u => activeUserIdsSet.Contains(u.Id));
        var day7Percentage = day7Cohort.Count > 0 ? (day7Retained / (double)day7Cohort.Count) * 100 : 0;

        // Day 30 retention
        var day30Cohort = cohortUsers.Where(u => u.CreatedAt <= day30Cutoff).ToList();
        var day30Retained = day30Cohort.Count(u => activeUserIdsSet.Contains(u.Id));
        var day30Percentage = day30Cohort.Count > 0 ? (day30Retained / (double)day30Cohort.Count) * 100 : 0;

        // Day 90 retention
        var day90Cohort = cohortUsers.Where(u => u.CreatedAt <= cohortStart).ToList();
        var day90Retained = day90Cohort.Count(u => activeUserIdsSet.Contains(u.Id));
        var day90Percentage = day90Cohort.Count > 0 ? (day90Retained / (double)day90Cohort.Count) * 100 : 0;

        return new RetentionCohortDto(
            Day7Percentage: Math.Round(day7Percentage, 1),
            Day30Percentage: Math.Round(day30Percentage, 1),
            Day90Percentage: Math.Round(day90Percentage, 1),
            SignupsInPeriod: cohortUsers.Count);
    }

    private async Task<IReadOnlyList<FeatureFunnelStepDto>> CalculateFeatureFunnelAsync(
        CancellationToken cancellationToken)
    {
        // Total signups (baseline)
        var totalUsers = await _dbContext.Users.CountAsync(cancellationToken).ConfigureAwait(false);

        if (totalUsers == 0)
        {
            return Array.Empty<FeatureFunnelStepDto>();
        }

        // FirstGame: users with at least one game in library
        var usersWithGames = await _dbContext.UserLibraryEntries
            .Select(e => e.UserId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        // FirstPDF: users who uploaded at least one PDF
        var usersWithPdfs = await _dbContext.PdfDocuments
            .Select(p => p.UploadedByUserId)
            .Distinct()
            .CountAsync(cancellationToken)
            .ConfigureAwait(false);

        // Feature funnel: SignUp → FirstGame → FirstPDF
        // Note: FirstAgent skipped (AgentDefinition doesn't track creator)
        return new List<FeatureFunnelStepDto>
        {
            new("SignUp", totalUsers, 100.0),
            new("FirstGame", usersWithGames, Math.Round((usersWithGames / (double)totalUsers) * 100, 1)),
            new("FirstPDF", usersWithPdfs, Math.Round((usersWithPdfs / (double)totalUsers) * 100, 1))
        };
    }

    private async Task<IReadOnlyList<GeoDistributionDto>> CalculateGeoDistributionAsync(
        DateTime periodStart,
        DateTime now,
        CancellationToken cancellationToken)
    {
        // Get distinct users with their most recent IP address in period
        var userIpAddresses = await _dbContext.UserSessions
            .Where(s => s.CreatedAt >= periodStart && s.CreatedAt < now)
            .Where(s => s.IpAddress != null)
            .GroupBy(s => s.UserId)
            .Select(g => new
            {
                UserId = g.Key,
                IpAddress = g.OrderByDescending(s => s.CreatedAt).First().IpAddress
            })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        if (userIpAddresses.Count == 0)
        {
            return Array.Empty<GeoDistributionDto>();
        }

        // Simple country detection from IP (MVP: all marked as Unknown)
        // Future enhancement: integrate MaxMind.GeoIP2 library for accurate geo mapping
        var totalUsers = userIpAddresses.Count;

        return new List<GeoDistributionDto>
        {
            new("Unknown", "XX", totalUsers, 100.0)
        };
    }
}
