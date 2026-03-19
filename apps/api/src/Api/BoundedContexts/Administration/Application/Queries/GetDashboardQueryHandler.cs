using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.Administration.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.BoundedContexts.Administration.Application.Queries;

/// <summary>
/// Handler for GetDashboardQuery (Issue #3972).
/// Aggregates data from multiple bounded contexts with HybridCache optimization.
/// Target latency: under 500ms (p99) with 5-minute Redis cache.
/// Response schema aligned with frontend DashboardData type.
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

                // Parallel execution for performance (Issue #3972: under 500ms p99)
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
                    ChatHistory: await chatsTask.ConfigureAwait(false)
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
                .Select(u => new { u.Id, u.DisplayName, u.Email })
                .FirstOrDefaultAsync(ct)
                .ConfigureAwait(false);

            return new DashboardUserDto(
                Id: user?.Id ?? userId,
                Username: user?.DisplayName ?? "User",
                Email: user?.Email ?? string.Empty
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch user info for {UserId}", userId);
            return new DashboardUserDto(userId, "User", string.Empty);
        }
    }

    private async Task<DashboardUserStatsDto> GetStatsAsync(Guid userId, CancellationToken ct)
    {
        try
        {
            // Library count
            var libraryCount = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .CountAsync(e => e.UserId == userId, ct)
                .ConfigureAwait(false);

            // Games played in last 30 days (sessions)
            var thirtyDaysAgo = DateTime.UtcNow.AddDays(-30);
            var playedLast30Days = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .SelectMany(e => e.Sessions)
                .CountAsync(s => s.PlayedAt >= thirtyDaysAgo, ct)
                .ConfigureAwait(false);

            // Chat count
            var chatCount = await _dbContext.ChatThreads
                .AsNoTracking()
                .CountAsync(c => c.UserId == userId, ct)
                .ConfigureAwait(false);

            // Wishlist is not available in current schema, returning 0
            var wishlistCount = 0;

            // Current streak: consecutive days with sessions (simplified)
            var currentStreak = 0;

            return new DashboardUserStatsDto(
                LibraryCount: libraryCount,
                PlayedLast30Days: playedLast30Days,
                ChatCount: chatCount,
                WishlistCount: wishlistCount,
                CurrentStreak: currentStreak
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch stats for {UserId}", userId);
            return new DashboardUserStatsDto(0, 0, 0, 0, 0);
        }
    }

    private async Task<IReadOnlyList<DashboardSessionDto>> GetActiveSessionsAsync(Guid userId, CancellationToken ct)
    {
        try
        {
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
                        SharedGameId = e.SharedGameId
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

                var durationStr = s.Session.DurationMinutes > 0
                    ? $"{s.Session.DurationMinutes}min"
                    : "0min";

                return new DashboardSessionDto(
                    Id: s.Session.Id,
                    GameName: s.Game?.Title ?? "Unknown Game",
                    GameId: s.SharedGameId ?? Guid.Empty,
                    CoverUrl: s.Game?.ImageUrl,
                    Players: new DashboardPlayersDto(playerCount, playerCount),
                    Progress: new DashboardProgressDto(0, durationStr),
                    LastActivity: s.Session.PlayedAt
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
            var used = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .CountAsync(e => e.UserId == userId, ct)
                .ConfigureAwait(false);

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
                Quota: new DashboardQuotaDto(used, DefaultLibraryQuota),
                TopGames: topGames
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch library snapshot for {UserId}", userId);
            return new DashboardLibrarySnapshotDto(
                Quota: new DashboardQuotaDto(0, DefaultLibraryQuota),
                TopGames: Array.Empty<DashboardTopGameDto>()
            );
        }
    }

    private async Task<IReadOnlyList<DashboardActivityDto>> GetRecentActivityAsync(Guid userId, CancellationToken ct)
    {
        try
        {
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
                    (e.SharedGameId ?? Guid.Empty).ToString(),
                    e.SharedGame!.Title,
                    null,
                    null,
                    null,
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
                null,
                s.GameTitle,
                s.Session.Id.ToString(),
                null,
                null,
                s.Session.PlayedAt
            )));

            // Recent chats
            var recentChats = await _dbContext.ChatThreads
                .AsNoTracking()
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.LastMessageAt)
                .Take(5)
                .Select(c => new DashboardActivityDto(
                    $"chat-{c.Id}",
                    "chat_saved",
                    null,
                    null,
                    null,
                    c.Id.ToString(),
                    c.Title ?? "Chat salvata",
                    c.LastMessageAt
                ))
                .ToListAsync(ct)
                .ConfigureAwait(false);

            activities.AddRange(recentChats);

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
                    Topic: c.Title ?? c.GameName ?? "Chat",
                    MessageCount: c.MessageCount,
                    Timestamp: c.LastMessageAt
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
