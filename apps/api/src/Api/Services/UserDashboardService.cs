using Api.BoundedContexts.Administration.Application.DTOs;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.Infrastructure;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;

namespace Api.Services;

/// <summary>
/// Service for retrieving user dashboard data (Issue #2854).
/// Aggregates data from multiple bounded contexts with HybridCache optimization.
/// </summary>
internal class UserDashboardService : IUserDashboardService
{
    private readonly IMediator _mediator;
    private readonly MeepleAiDbContext _dbContext;
    private readonly HybridCache _cache;
    private readonly ILogger<UserDashboardService> _logger;

    private static readonly TimeSpan CacheDuration = TimeSpan.FromMinutes(5); // Issue #2854: 5 min TTL
    private const int MaxRecentGames = 5;
    private const int MaxRecentChats = 5;

    // Issue #2854: Library quota default (configuration-based limit planned for future)
    private const int DefaultLibraryQuota = 1000;

    public UserDashboardService(
        IMediator mediator,
        MeepleAiDbContext dbContext,
        HybridCache cache,
        ILogger<UserDashboardService> logger)
    {
        _mediator = mediator;
        _dbContext = dbContext;
        _cache = cache;
        _logger = logger;
    }

    public async Task<UserDashboardDto> GetUserDashboardAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var cacheKey = $"user-dashboard:{userId}";

        return await _cache.GetOrCreateAsync<UserDashboardDto>(
            cacheKey,
            async cancel =>
            {
                _logger.LogInformation("Cache miss for user dashboard {UserId}, generating fresh data", userId);

                // Parallel execution of independent queries for performance (Issue #2854)
                var recentGamesTask = GetRecentGamesAsync(userId, cancel);
                var activeSessionsTask = GetActiveSessionsAsync(userId, cancel);
                var recentChatsTask = GetRecentChatsAsync(userId, cancel);
                var libraryQuotaTask = GetLibraryQuotaAsync(userId, cancel);

                await Task.WhenAll(recentGamesTask, activeSessionsTask, recentChatsTask, libraryQuotaTask)
                    .ConfigureAwait(false);

                return new UserDashboardDto(
                    RecentGames: await recentGamesTask.ConfigureAwait(false),
                    ActiveSessions: await activeSessionsTask.ConfigureAwait(false),
                    RecentChats: await recentChatsTask.ConfigureAwait(false),
                    LibraryQuota: await libraryQuotaTask.ConfigureAwait(false)
                );
            },
            new HybridCacheEntryOptions
            {
                Expiration = CacheDuration,
                LocalCacheExpiration = TimeSpan.FromMinutes(1), // L1 cache: 1 min
                Flags = HybridCacheEntryFlags.DisableCompression // User-specific data, small size
            },
            tags: new[] { "user-dashboard", $"user:{userId}" },
            cancellationToken: cancellationToken
        ).ConfigureAwait(false);
    }

    /// <summary>
    /// Get recent games from user library with optimized includes (avoid N+1).
    /// </summary>
    private async Task<IReadOnlyList<RecentGameDto>> GetRecentGamesAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var entries = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .Include(e => e.SharedGame) // Avoid N+1 query
                .Where(e => e.UserId == userId && e.SharedGame != null) // Filter null refs (soft-deleted games)
                .OrderByDescending(e => e.AddedAt)
                .Take(MaxRecentGames)
                .Select(e => new RecentGameDto(
                    e.GameId,
                    e.SharedGame!.Title, // Safe after null filter in Where clause
                    e.SharedGame!.ImageUrl,
                    e.AddedAt,
                    e.IsFavorite
                ))
                .ToListAsync(cancellationToken)
                .ConfigureAwait(false);

            return entries;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch recent games for user {UserId}", userId);
            return Array.Empty<RecentGameDto>();
        }
    }

    /// <summary>
    /// Get active game sessions with optimized query.
    /// Issue #2854: GameSession doesn't have UserId - returning empty for now.
    /// Note: Sessions are global, not user-specific. User dashboard may need different data source.
    /// </summary>
    private async Task<IReadOnlyList<ActiveSessionDto>> GetActiveSessionsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            // Issue #2854: GameSessionEntity lacks UserId field
            // Sessions appear to be global, not user-specific
            // Returning empty collection until user session tracking is implemented
            await Task.CompletedTask.ConfigureAwait(false);
            _logger.LogWarning(
                "Active sessions requested for user {UserId}, but GameSession lacks UserId tracking",
                userId);

            return Array.Empty<ActiveSessionDto>();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch active sessions for user {UserId}", userId);
            return Array.Empty<ActiveSessionDto>();
        }
    }

    /// <summary>
    /// Get recent chat history via mediator (CQRS pattern).
    /// </summary>
    private async Task<IReadOnlyList<RecentChatDto>> GetRecentChatsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var query = new GetMyChatHistoryQuery(
                UserId: userId,
                Skip: 0,
                Take: MaxRecentChats
            );

            var result = await _mediator.Send(query, cancellationToken).ConfigureAwait(false);

            return result.Chats
                .Select(c => new RecentChatDto(
                    ChatId: c.Id,
                    GameName: c.GameName,
                    Title: c.Title,
                    LastMessage: c.LastMessageContent,
                    LastMessageAt: c.LastMessageAt
                ))
                .ToList();
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to fetch recent chats for user {UserId}", userId);
            return Array.Empty<RecentChatDto>();
        }
    }

    /// <summary>
    /// Calculate library quota (current count vs max allowed).
    /// </summary>
    private async Task<LibraryQuotaDto> GetLibraryQuotaAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        try
        {
            var currentCount = await _dbContext.UserLibraryEntries
                .AsNoTracking()
                .Where(e => e.UserId == userId)
                .CountAsync(cancellationToken)
                .ConfigureAwait(false);

            var percentage = currentCount * 100 / DefaultLibraryQuota;

            return new LibraryQuotaDto(
                CurrentCount: currentCount,
                MaxAllowed: DefaultLibraryQuota,
                Percentage: Math.Min(percentage, 100) // Cap at 100%
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to calculate library quota for user {UserId}", userId);
            return new LibraryQuotaDto(0, DefaultLibraryQuota, 0);
        }
    }
}
