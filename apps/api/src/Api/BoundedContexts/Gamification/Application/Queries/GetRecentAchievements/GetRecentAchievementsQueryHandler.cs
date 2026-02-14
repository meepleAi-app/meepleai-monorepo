using Api.BoundedContexts.Gamification.Application.DTOs;
using Api.BoundedContexts.Gamification.Domain.Repositories;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Gamification.Application.Queries.GetRecentAchievements;

/// <summary>
/// Handler for GetRecentAchievementsQuery.
/// Returns the most recently unlocked achievements for a user.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal sealed class GetRecentAchievementsQueryHandler : IRequestHandler<GetRecentAchievementsQuery, List<RecentAchievementDto>>
{
    private const string CacheKeyPrefix = "achievements:recent:";
    private const int MaxCacheLimit = 20;
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(5);

    private readonly IAchievementRepository _achievementRepository;
    private readonly IUserAchievementRepository _userAchievementRepository;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetRecentAchievementsQueryHandler> _logger;

    public GetRecentAchievementsQueryHandler(
        IAchievementRepository achievementRepository,
        IUserAchievementRepository userAchievementRepository,
        IHybridCacheService cache,
        ILogger<GetRecentAchievementsQueryHandler> logger)
    {
        _achievementRepository = achievementRepository ?? throw new ArgumentNullException(nameof(achievementRepository));
        _userAchievementRepository = userAchievementRepository ?? throw new ArgumentNullException(nameof(userAchievementRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<RecentAchievementDto>> Handle(
        GetRecentAchievementsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var cacheKey = $"{CacheKeyPrefix}{query.UserId}";

        // Cache full set, trim to requested limit
        var allRecent = await _cache.GetOrCreateAsync(
            cacheKey,
            async ct => await ComputeRecentAsync(query.UserId, MaxCacheLimit, ct).ConfigureAwait(false),
            tags: ["achievements", $"user:{query.UserId}"],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);

        var result = allRecent.Take(query.Limit).ToList();

        _logger.LogInformation(
            "Retrieved {Count} recent achievements for user {UserId}",
            result.Count,
            query.UserId);

        return result;
    }

    private async Task<List<RecentAchievementDto>> ComputeRecentAsync(
        Guid userId,
        int limit,
        CancellationToken cancellationToken)
    {
        var recentUnlocked = await _userAchievementRepository
            .GetRecentUnlockedAsync(userId, limit, cancellationToken)
            .ConfigureAwait(false);

        if (recentUnlocked.Count == 0)
            return new List<RecentAchievementDto>();

        // Fetch achievement details
        var achievements = await _achievementRepository.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        var achievementMap = achievements.ToDictionary(a => a.Id);

        return recentUnlocked
            .Where(ua => achievementMap.ContainsKey(ua.AchievementId))
            .Select(ua =>
            {
                var achievement = achievementMap[ua.AchievementId];
                return new RecentAchievementDto
                {
                    AchievementId = achievement.Id,
                    Code = achievement.Code,
                    Name = achievement.Name,
                    Description = achievement.Description,
                    IconUrl = achievement.IconUrl,
                    Points = achievement.Points,
                    Rarity = achievement.Rarity.ToString(),
                    UnlockedAt = ua.UnlockedAt!.Value
                };
            })
            .ToList();
    }
}
