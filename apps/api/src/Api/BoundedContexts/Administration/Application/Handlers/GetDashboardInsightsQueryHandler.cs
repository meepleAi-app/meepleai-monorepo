using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GetDashboardInsightsQuery (Issue #3319).
/// Generates personalized AI insights based on user's library, play history, and activity.
/// Uses HybridCache with 15-minute TTL for performance.
/// </summary>
internal sealed class GetDashboardInsightsQueryHandler : IRequestHandler<GetDashboardInsightsQuery, DashboardInsightsResponseDto>
{
    private readonly MeepleAiDbContext _dbContext;
    private readonly HybridCache _cache;
    private readonly ILogger<GetDashboardInsightsQueryHandler> _logger;
    private readonly TimeProvider _timeProvider;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(15);
    private const int MaxInsights = 5;
    private const int BacklogThresholdDays = 30;
    private const int RecentRulesThresholdDays = 7;

    public GetDashboardInsightsQueryHandler(
        MeepleAiDbContext dbContext,
        HybridCache cache,
        ILogger<GetDashboardInsightsQueryHandler> logger,
        TimeProvider timeProvider)
    {
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
        _timeProvider = timeProvider;
    }

    public async Task<DashboardInsightsResponseDto> Handle(
        GetDashboardInsightsQuery query,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"dashboard-insights:{query.UserId}";

        return await _cache.GetOrCreateAsync<DashboardInsightsResponseDto>(
            cacheKey,
            async cancel =>
            {
                _logger.LogInformation("Cache miss for dashboard insights {UserId}, generating fresh insights", query.UserId);

                // Parallel execution for performance
                var backlogTask = GetBacklogInsightsAsync(query.UserId, cancel);
                var rulesTask = GetRulesReminderInsightsAsync(query.UserId, cancel);
                var recommendationTask = GetRecommendationInsightsAsync(query.UserId, cancel);
                var streakTask = GetStreakInsightsAsync(query.UserId, cancel);
                var achievementTask = GetAchievementInsightsAsync(query.UserId, cancel);

                await Task.WhenAll(backlogTask, rulesTask, recommendationTask, streakTask, achievementTask)
                    .ConfigureAwait(false);

                var allInsights = new List<DashboardInsightDto>();
                allInsights.AddRange(await backlogTask.ConfigureAwait(false));
                allInsights.AddRange(await rulesTask.ConfigureAwait(false));
                allInsights.AddRange(await recommendationTask.ConfigureAwait(false));
                allInsights.AddRange(await streakTask.ConfigureAwait(false));
                allInsights.AddRange(await achievementTask.ConfigureAwait(false));

                // Sort by priority and limit
                var sortedInsights = allInsights
                    .OrderBy(i => i.Priority)
                    .Take(MaxInsights)
                    .ToList();

                var now = _timeProvider.GetUtcNow().UtcDateTime;
                return new DashboardInsightsResponseDto(
                    Insights: sortedInsights,
                    GeneratedAt: now,
                    NextRefresh: now.Add(CacheDuration)
                );
            },
            new HybridCacheEntryOptions
            {
                Expiration = CacheDuration,
                LocalCacheExpiration = TimeSpan.FromMinutes(5),
                Flags = HybridCacheEntryFlags.DisableCompression
            },
            tags: ["dashboard-insights", $"user:{query.UserId}"],
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// Get insights for games not played in 30+ days.
    /// </summary>
    private async Task<IReadOnlyList<DashboardInsightDto>> GetBacklogInsightsAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            var thresholdDate = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-BacklogThresholdDays);

            // Get games not played recently (LastPlayed property per entity definition)
            var unplayedGames = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .Include(e => e.SharedGame)
                .Where(e => e.UserId == userId && e.SharedGame != null)
                .Where(e => e.LastPlayed == null || e.LastPlayed < thresholdDate)
                .OrderBy(e => e.LastPlayed ?? e.AddedAt)
                .Take(10)
                .Select(e => new { e.SharedGame!.Title, e.LastPlayed, e.AddedAt })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (unplayedGames.Count == 0)
                return Array.Empty<DashboardInsightDto>();

            var count = unplayedGames.Count;
            var sampleGames = string.Join(", ", unplayedGames.Take(3).Select(g => g.Title));
            var description = count > 3
                ? $"{sampleGames} e altri {count - 3} aspettano di essere giocati"
                : $"{sampleGames} aspettano di essere giocati";

            return
            [
                new DashboardInsightDto(
                    Id: $"backlog-{userId}-{_timeProvider.GetUtcNow().UtcDateTime:yyyyMMdd}",
                    Type: InsightType.Backlog,
                    Icon: "🎯",
                    Title: $"{count} giochi non giocati da 30+ giorni",
                    Description: description,
                    ActionUrl: "/library?filter=unplayed",
                    ActionLabel: "Scopri",
                    Priority: 1
                )
            ];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get backlog insights for {UserId}", userId);
            return Array.Empty<DashboardInsightDto>();
        }
    }

    /// <summary>
    /// Get insights for recently saved chat rules.
    /// </summary>
    private async Task<IReadOnlyList<DashboardInsightDto>> GetRulesReminderInsightsAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            var thresholdDate = _timeProvider.GetUtcNow().UtcDateTime.AddDays(-RecentRulesThresholdDays);

            // Get recent chats (with game association, indicating rules discussion)
            var recentChats = await _dbContext.ChatThreads
                .AsNoTracking()
                .Where(c => c.UserId == userId && c.GameId != null && c.CreatedAt >= thresholdDate)
                .OrderByDescending(c => c.CreatedAt)
                .Take(3)
                .Select(c => new { c.Id, c.Title, c.GameId, c.CreatedAt })
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (recentChats.Count == 0)
                return Array.Empty<DashboardInsightDto>();

            var mostRecent = recentChats[0];
            var daysAgo = (_timeProvider.GetUtcNow().UtcDateTime - mostRecent.CreatedAt).Days;
            var daysText = daysAgo == 0 ? "oggi" : daysAgo == 1 ? "ieri" : $"{daysAgo} giorni fa";

            return
            [
                new DashboardInsightDto(
                    Id: $"rules-{mostRecent.Id}",
                    Type: InsightType.RulesReminder,
                    Icon: "📖",
                    Title: $"Regole di \"{mostRecent.Title ?? "un gioco"}\" salvate",
                    Description: $"Hai salvato le regole {daysText}",
                    ActionUrl: $"/chat/{mostRecent.Id}",
                    ActionLabel: "Rivedi",
                    Priority: 2
                )
            ];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get rules reminder insights for {UserId}", userId);
            return Array.Empty<DashboardInsightDto>();
        }
    }

    /// <summary>
    /// Get game recommendations based on user's preferences.
    /// </summary>
    private async Task<IReadOnlyList<DashboardInsightDto>> GetRecommendationInsightsAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            // Get user's most played game for similarity
            var topGame = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .Include(e => e.SharedGame)
                .Where(e => e.UserId == userId && e.SharedGame != null)
                .OrderByDescending(e => e.TimesPlayed)
                .Select(e => new { e.SharedGame!.Id, e.SharedGame!.Title })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            if (topGame == null)
                return Array.Empty<DashboardInsightDto>();

            // Get user's existing game IDs
            var existingGameIds = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .Select(e => e.GameId)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            // Find similar games not in library (based on same categories/mechanics)
            // This is a simplified version - in production would use RAG/vector similarity
            var similarCount = await _dbContext.SharedGames
                .AsNoTracking()
                .Where(g => !existingGameIds.Contains(g.Id))
                .Take(10)
                .CountAsync(ct)
                .ConfigureAwait(false);

            if (similarCount == 0)
                return Array.Empty<DashboardInsightDto>();

            var displayCount = Math.Min(similarCount, 3);

            return
            [
                new DashboardInsightDto(
                    Id: $"recommendation-{userId}-{_timeProvider.GetUtcNow().UtcDateTime:yyyyMMdd}",
                    Type: InsightType.Recommendation,
                    Icon: "🆕",
                    Title: $"{displayCount} giochi simili a \"{topGame.Title}\"",
                    Description: "Scopri nuovi giochi basati sui tuoi gusti",
                    ActionUrl: $"/games/recommendations?based-on={topGame.Id}",
                    ActionLabel: "Esplora",
                    Priority: 3,
                    Metadata: new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["basedOnGameId"] = topGame.Id,
                        ["basedOnGameTitle"] = topGame.Title
                    }
                )
            ];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get recommendation insights for {UserId}", userId);
            return Array.Empty<DashboardInsightDto>();
        }
    }

    /// <summary>
    /// Get streak-related insights to encourage engagement.
    /// </summary>
    private async Task<IReadOnlyList<DashboardInsightDto>> GetStreakInsightsAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            // Calculate current streak (consecutive days with activity)
            var recentSessions = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .SelectMany(e => e.Sessions)
                .Where(s => s.PlayedAt >= _timeProvider.GetUtcNow().UtcDateTime.AddDays(-30))
                .Select(s => s.PlayedAt.Date)
                .Distinct()
                .OrderByDescending(d => d)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            if (recentSessions.Count == 0)
                return Array.Empty<DashboardInsightDto>();

            // Calculate streak
            var streak = 0;
            var expectedDate = _timeProvider.GetUtcNow().UtcDateTime.Date;

            foreach (var sessionDate in recentSessions)
            {
                if (sessionDate == expectedDate || sessionDate == expectedDate.AddDays(-1))
                {
                    streak++;
                    expectedDate = sessionDate.AddDays(-1);
                }
                else
                {
                    break;
                }
            }

            if (streak < 2)
                return Array.Empty<DashboardInsightDto>();

            var playedToday = recentSessions.Contains(_timeProvider.GetUtcNow().UtcDateTime.Date);
            var description = playedToday
                ? "Ottimo lavoro! Continua così!"
                : "Gioca oggi per mantenere la tua serie";

            return
            [
                new DashboardInsightDto(
                    Id: $"streak-{userId}-{_timeProvider.GetUtcNow().UtcDateTime:yyyyMMdd}",
                    Type: InsightType.Streak,
                    Icon: "🔥",
                    Title: $"Streak: {streak} giorni{(playedToday ? "" : " - Mantienilo!")}",
                    Description: description,
                    ActionUrl: "/stats/streak",
                    ActionLabel: "Stats",
                    Priority: playedToday ? 5 : 4,
                    Metadata: new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["currentStreak"] = streak,
                        ["playedToday"] = playedToday
                    }
                )
            ];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get streak insights for {UserId}", userId);
            return Array.Empty<DashboardInsightDto>();
        }
    }

    /// <summary>
    /// Get achievement progress insights.
    /// </summary>
    private async Task<IReadOnlyList<DashboardInsightDto>> GetAchievementInsightsAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            // Get collection size for "Collector" achievement
            var collectionCount = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .CountAsync(e => e.UserId == userId, ct)
                .ConfigureAwait(false);

            // Define achievement thresholds
            var collectorThresholds = new[] { 10, 25, 50, 100, 200 };
            var nextThreshold = collectorThresholds.FirstOrDefault(t => t > collectionCount);

            if (nextThreshold == 0 || collectionCount < 5)
                return Array.Empty<DashboardInsightDto>();

            var progress = (int)Math.Round((double)collectionCount / nextThreshold * 100);
            var remaining = nextThreshold - collectionCount;

            if (progress < 70)
                return Array.Empty<DashboardInsightDto>();

            return
            [
                new DashboardInsightDto(
                    Id: $"achievement-collector-{userId}",
                    Type: InsightType.Achievement,
                    Icon: "📊",
                    Title: $"Achievement \"Collezionista\" al {progress}%",
                    Description: $"Ti mancano solo {remaining} giochi!",
                    ActionUrl: "/achievements/collector",
                    ActionLabel: "Vedi",
                    Priority: 5,
                    Metadata: new Dictionary<string, object>(StringComparer.Ordinal)
                    {
                        ["achievementId"] = "collector",
                        ["currentProgress"] = collectionCount,
                        ["targetProgress"] = nextThreshold,
                        ["percentage"] = progress
                    }
                )
            ];
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to get achievement insights for {UserId}", userId);
            return Array.Empty<DashboardInsightDto>();
        }
    }
}
