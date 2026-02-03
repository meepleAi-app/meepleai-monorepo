using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.Administration.Application.Handlers;

/// <summary>
/// Handler for GetDashboardQuery (Issue #3314).
/// Aggregates data from multiple bounded contexts with HybridCache optimization.
/// Target latency: under 500ms (p99) with 5-minute Redis cache.
/// </summary>
internal sealed class GetDashboardQueryHandler : IRequestHandler<GetDashboardQuery, DashboardResponseDto>
{
    private readonly IMediator _mediator;
    private readonly MeepleAiDbContext _dbContext;
    private readonly HybridCache _cache;
    private readonly ILogger<GetDashboardQueryHandler> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5);
    private const int MaxTopGames = 3;
    private const int MaxRecentActivity = 10;
    private const int MaxRecentChats = 4;
    private const int DefaultLibraryQuota = 1000;

    public GetDashboardQueryHandler(
        IMediator mediator,
        MeepleAiDbContext dbContext,
        HybridCache cache,
        ILogger<GetDashboardQueryHandler> logger)
    {
        _mediator = mediator;
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<DashboardResponseDto> Handle(
        GetDashboardQuery query,
        CancellationToken cancellationToken)
    {
        var cacheKey = $"dashboard-api:{query.UserId}";

        return await _cache.GetOrCreateAsync<DashboardResponseDto>(
            cacheKey,
            async cancel =>
            {
                _logger.LogInformation("Cache miss for dashboard API {UserId}, generating fresh data", query.UserId);

                // Parallel execution for performance (Issue #3314: under 500ms p99)
                var userTask = GetUserInfoAsync(query.UserId, cancel);
                var statsTask = GetStatsAsync(query.UserId, cancel);
                var sessionsTask = GetActiveSessionsAsync(query.UserId, cancel);
                var libraryTask = GetLibrarySnapshotAsync(query.UserId, cancel);
                var activityTask = GetRecentActivityAsync(query.UserId, cancel);
                var chatsTask = GetRecentChatsAsync(query.UserId, cancel);

                await Task.WhenAll(userTask, statsTask, sessionsTask, libraryTask, activityTask, chatsTask)
                    .ConfigureAwait(false);

                return new DashboardResponseDto(
                    User: await userTask.ConfigureAwait(false),
                    Stats: await statsTask.ConfigureAwait(false),
                    ActiveSessions: await sessionsTask.ConfigureAwait(false),
                    LibrarySnapshot: await libraryTask.ConfigureAwait(false),
                    RecentActivity: await activityTask.ConfigureAwait(false),
                    RecentChats: await chatsTask.ConfigureAwait(false)
                );
            },
            new HybridCacheEntryOptions
            {
                Expiration = CacheDuration,
                LocalCacheExpiration = TimeSpan.FromMinutes(1),
                Flags = HybridCacheEntryFlags.DisableCompression
            },
            tags: ["dashboard-api", $"user:{query.UserId}"],
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }

    private async Task<DashboardUserDto> GetUserInfoAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            var user = await _dbContext.Users
                .AsNoTracking()
                .Where(u => u.Id == userId)
                .Select(u => new { u.DisplayName, u.Email, u.CreatedAt })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            return new DashboardUserDto(
                Name: user?.DisplayName ?? user?.Email ?? "User",
                LastAccess: user?.CreatedAt ?? DateTime.UtcNow
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch user info for {UserId}", userId);
            return new DashboardUserDto("User", DateTime.UtcNow);
        }
    }

    private async Task<DashboardStatsResponseDto> GetStatsAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            // Collection stats
            var collectionTotal = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .CountAsync(e => e.UserId == userId, ct)
                .ConfigureAwait(false);

            // Trend: games added in last 30 days
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var collectionTrend = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .CountAsync(e => e.UserId == userId && e.AddedAt >= thirtyDaysAgo, ct)
                .ConfigureAwait(false);

            // Played stats (sessions count)
            var sessionsPlayed = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .SelectMany(e => e.Sessions)
                .CountAsync(ct)
                .ConfigureAwait(false);

            // Chat stats (using ChatThreads)
            var chatsTotal = await _dbContext.ChatThreads
                .AsNoTracking()
                .CountAsync(c => c.UserId == userId, ct)
                .ConfigureAwait(false);

            // Wishlist is not available in current schema, returning 0
            var wishlistTotal = 0;
            var wishlistTrend = 0;

            return new DashboardStatsResponseDto(
                Collection: new DashboardStatItemDto(collectionTotal, collectionTrend),
                Played: new DashboardPlayedStatDto(sessionsPlayed, 0),
                Chats: new DashboardStatCountDto(chatsTotal),
                Wishlist: new DashboardStatItemDto(wishlistTotal, wishlistTrend)
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch stats for {UserId}", userId);
            return new DashboardStatsResponseDto(
                Collection: new DashboardStatItemDto(0, 0),
                Played: new DashboardPlayedStatDto(0, 0),
                Chats: new DashboardStatCountDto(0),
                Wishlist: new DashboardStatItemDto(0, 0)
            );
        }
    }

    private async Task<IReadOnlyList<DashboardSessionDto>> GetActiveSessionsAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            // Get recent game sessions from user's library entries
            var sessions = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .Include(e => e.Sessions)
                .Include(e => e.SharedGame)
                .Where(e => e.UserId == userId && e.Sessions.Any())
                .SelectMany(e => e.Sessions
                    .OrderByDescending(s => s.PlayedAt)
                    .Take(1)
                    .Select(s => new
                    {
                        Session = s,
                        Game = e.SharedGame,
                        GameId = e.GameId
                    }))
                .OrderByDescending(x => x.Session.PlayedAt)
                .Take(5)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return sessions.Select(s =>
            {
                var playerCount = string.IsNullOrEmpty(s.Session.Players)
                    ? 1
                    : s.Session.Players.Split(',', StringSplitOptions.RemoveEmptyEntries).Length;

                return new DashboardSessionDto(
                    Id: s.Session.Id,
                    GameName: s.Game?.Title ?? "Unknown Game",
                    GameId: s.GameId,
                    StartDate: s.Session.PlayedAt,
                    Players: new DashboardPlayersDto(playerCount, playerCount),
                    Turn: 0,
                    Duration: s.Session.DurationMinutes
                );
            }).ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch active sessions for {UserId}", userId);
            return Array.Empty<DashboardSessionDto>();
        }
    }

    private async Task<DashboardLibrarySnapshotDto> GetLibrarySnapshotAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            // Quota
            var used = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .CountAsync(e => e.UserId == userId, ct)
                .ConfigureAwait(false);

            var percentage = used * 100 / DefaultLibraryQuota;

            // Top 3 games by play count (TimesPlayed)
            var topGames = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .Include(e => e.SharedGame)
                .Where(e => e.UserId == userId && e.SharedGame != null)
                .OrderByDescending(e => e.TimesPlayed)
                .ThenByDescending(e => e.WinRate)
                .Take(MaxTopGames)
                .Select(e => new DashboardTopGameDto(
                    e.GameId,
                    e.SharedGame!.Title,
                    e.SharedGame!.ImageUrl,
                    e.WinRate ?? 0,
                    e.TimesPlayed
                ))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            return new DashboardLibrarySnapshotDto(
                Quota: new DashboardQuotaDto(used, DefaultLibraryQuota, Math.Min(percentage, 100)),
                TopGames: topGames
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch library snapshot for {UserId}", userId);
            return new DashboardLibrarySnapshotDto(
                Quota: new DashboardQuotaDto(0, DefaultLibraryQuota, 0),
                TopGames: Array.Empty<DashboardTopGameDto>()
            );
        }
    }

    private async Task<IReadOnlyList<DashboardActivityDto>> GetRecentActivityAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            // Combine activities from multiple sources
            var activities = new List<DashboardActivityDto>();

            // Recent library additions
            var recentGames = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .Include(e => e.SharedGame)
                .Where(e => e.UserId == userId && e.SharedGame != null)
                .OrderByDescending(e => e.AddedAt)
                .Take(5)
                .Select(e => new DashboardActivityDto(
                    $"game-{e.Id}",
                    "game_added",
                    $"Aggiunto \"{e.SharedGame!.Title}\"",
                    e.GameId.ToString(),
                    e.AddedAt
                ))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            activities.AddRange(recentGames);

            // Recent sessions
            var recentSessions = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .Include(e => e.Sessions)
                .Include(e => e.SharedGame)
                .Where(e => e.UserId == userId && e.SharedGame != null)
                .SelectMany(e => e.Sessions.Select(s => new
                {
                    Session = s,
                    GameTitle = e.SharedGame!.Title
                }))
                .OrderByDescending(s => s.Session.PlayedAt)
                .Take(5)
                .ToListAsync(ct)
                .ConfigureAwait(false);

            activities.AddRange(recentSessions.Select(s => new DashboardActivityDto(
                $"session-{s.Session.Id}",
                "session_completed",
                $"Giocato \"{s.GameTitle}\"",
                s.Session.Id.ToString(),
                s.Session.PlayedAt
            )));

            // Recent chats (using ChatThreads)
            var recentChats = await _dbContext.ChatThreads
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.LastMessageAt)
                .Take(5)
                .Select(c => new DashboardActivityDto(
                    $"chat-{c.Id}",
                    "chat_saved",
                    c.Title ?? "Chat salvata",
                    c.Id.ToString(),
                    c.LastMessageAt
                ))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            activities.AddRange(recentChats);

            // Sort by timestamp and take top 10
            return activities
                .OrderByDescending(a => a.Timestamp)
                .Take(MaxRecentActivity)
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch recent activity for {UserId}", userId);
            return Array.Empty<DashboardActivityDto>();
        }
    }

    private async Task<IReadOnlyList<DashboardChatDto>> GetRecentChatsAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            var query = new GetMyChatHistoryQuery(
                UserId: userId,
                Skip: 0,
                Take: MaxRecentChats
            );

            var result = await _mediator.Send(query, ct).ConfigureAwait(false);

            return result.Chats
                .Select(c => new DashboardChatDto(
                    Id: c.Id.ToString(),
                    Title: c.Title ?? c.GameName ?? "Chat",
                    LastMessageAt: c.LastMessageAt
                ))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch recent chats for {UserId}", userId);
            return Array.Empty<DashboardChatDto>();
        }
    }
}
