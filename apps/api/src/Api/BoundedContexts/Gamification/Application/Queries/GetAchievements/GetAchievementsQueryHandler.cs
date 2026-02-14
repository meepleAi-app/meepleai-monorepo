using Api.BoundedContexts.Gamification.Application.DTOs;
using Api.BoundedContexts.Gamification.Domain.Repositories;
using Api.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Gamification.Application.Queries.GetAchievements;

/// <summary>
/// Handler for GetAchievementsQuery.
/// Returns all active achievements with user's unlock status and progress.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal sealed class GetAchievementsQueryHandler : IRequestHandler<GetAchievementsQuery, List<AchievementDto>>
{
    private const string CacheKeyPrefix = "achievements:user:";
    private static readonly TimeSpan CacheTtl = TimeSpan.FromMinutes(10);

    private readonly IAchievementRepository _achievementRepository;
    private readonly IUserAchievementRepository _userAchievementRepository;
    private readonly IHybridCacheService _cache;
    private readonly ILogger<GetAchievementsQueryHandler> _logger;

    public GetAchievementsQueryHandler(
        IAchievementRepository achievementRepository,
        IUserAchievementRepository userAchievementRepository,
        IHybridCacheService cache,
        ILogger<GetAchievementsQueryHandler> logger)
    {
        _achievementRepository = achievementRepository ?? throw new ArgumentNullException(nameof(achievementRepository));
        _userAchievementRepository = userAchievementRepository ?? throw new ArgumentNullException(nameof(userAchievementRepository));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<AchievementDto>> Handle(
        GetAchievementsQuery query,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var cacheKey = $"{CacheKeyPrefix}{query.UserId}";

        var result = await _cache.GetOrCreateAsync(
            cacheKey,
            async ct => await ComputeAchievementsAsync(query.UserId, ct).ConfigureAwait(false),
            tags: ["achievements", $"user:{query.UserId}"],
            expiration: CacheTtl,
            ct: cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Retrieved {Count} achievements for user {UserId}",
            result.Count,
            query.UserId);

        return result;
    }

    private async Task<List<AchievementDto>> ComputeAchievementsAsync(
        Guid userId,
        CancellationToken cancellationToken)
    {
        var achievements = await _achievementRepository.GetActiveAsync(cancellationToken).ConfigureAwait(false);
        var userAchievements = await _userAchievementRepository.GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        var userAchievementMap = userAchievements.ToDictionary(ua => ua.AchievementId);

        return achievements.Select(a =>
        {
            userAchievementMap.TryGetValue(a.Id, out var ua);
            return new AchievementDto
            {
                Id = a.Id,
                Code = a.Code,
                Name = a.Name,
                Description = a.Description,
                IconUrl = a.IconUrl,
                Points = a.Points,
                Rarity = a.Rarity.ToString(),
                Category = a.Category.ToString(),
                Threshold = a.Threshold,
                Progress = ua?.Progress,
                IsUnlocked = ua?.IsUnlocked ?? false,
                UnlockedAt = ua?.UnlockedAt
            };
        }).ToList();
    }
}
