using Api.BoundedContexts.Gamification.Application.Services;
using Api.BoundedContexts.Gamification.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.Aggregates;
using Api.BoundedContexts.UserNotifications.Domain.Repositories;
using Api.BoundedContexts.UserNotifications.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Services;
using Api.SharedKernel.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using Quartz;

namespace Api.BoundedContexts.Gamification.Application.Jobs;

/// <summary>
/// Daily background job that evaluates achievement rules for all active users.
/// Unlocks achievements when thresholds are met and sends notifications.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
[DisallowConcurrentExecution]
internal sealed class AchievementEvaluationJob : IJob
{
    private readonly IAchievementRepository _achievementRepository;
    private readonly IUserAchievementRepository _userAchievementRepository;
    private readonly IAchievementRuleEvaluator _ruleEvaluator;
    private readonly INotificationRepository _notificationRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IHybridCacheService _cache;
    private readonly MeepleAiDbContext _context;
    private readonly ILogger<AchievementEvaluationJob> _logger;

    public AchievementEvaluationJob(
        IAchievementRepository achievementRepository,
        IUserAchievementRepository userAchievementRepository,
        IAchievementRuleEvaluator ruleEvaluator,
        INotificationRepository notificationRepository,
        IUnitOfWork unitOfWork,
        IHybridCacheService cache,
        MeepleAiDbContext context,
        ILogger<AchievementEvaluationJob> logger)
    {
        _achievementRepository = achievementRepository ?? throw new ArgumentNullException(nameof(achievementRepository));
        _userAchievementRepository = userAchievementRepository ?? throw new ArgumentNullException(nameof(userAchievementRepository));
        _ruleEvaluator = ruleEvaluator ?? throw new ArgumentNullException(nameof(ruleEvaluator));
        _notificationRepository = notificationRepository ?? throw new ArgumentNullException(nameof(notificationRepository));
        _unitOfWork = unitOfWork ?? throw new ArgumentNullException(nameof(unitOfWork));
        _cache = cache ?? throw new ArgumentNullException(nameof(cache));
        _context = context ?? throw new ArgumentNullException(nameof(context));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task Execute(IJobExecutionContext context)
    {
        _logger.LogInformation(
            "AchievementEvaluationJob started: FireTime={FireTime}",
            context.FireTimeUtc);

        try
        {
            var achievements = await _achievementRepository.GetActiveAsync(context.CancellationToken).ConfigureAwait(false);
            if (achievements.Count == 0)
            {
                _logger.LogInformation("No active achievements to evaluate");
                return;
            }

            // Get all active user IDs
            var userIds = await _context.Users
                .AsNoTracking()
                .Where(u => !u.IsSuspended)
                .Select(u => u.Id)
                .ToListAsync(context.CancellationToken)
                .ConfigureAwait(false);

            _logger.LogInformation("Evaluating {AchievementCount} achievements for {UserCount} users",
                achievements.Count, userIds.Count);

            var totalUnlocks = 0;

            foreach (var userId in userIds)
            {
                var unlocks = await EvaluateUserAchievementsAsync(
                    userId, achievements, context.CancellationToken).ConfigureAwait(false);

                totalUnlocks += unlocks;
            }

            await _unitOfWork.SaveChangesAsync(context.CancellationToken).ConfigureAwait(false);

            // Invalidate cache for affected users
            await _cache.RemoveByTagAsync("achievements", context.CancellationToken).ConfigureAwait(false);

            _logger.LogInformation(
                "AchievementEvaluationJob completed: {UserCount} users evaluated, {UnlockCount} new unlocks",
                userIds.Count, totalUnlocks);

            context.Result = new
            {
                Success = true,
                UsersEvaluated = userIds.Count,
                NewUnlocks = totalUnlocks
            };
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "AchievementEvaluationJob failed");
            context.Result = new { Success = false, Error = ex.Message };
        }
#pragma warning restore CA1031
    }

    private async Task<int> EvaluateUserAchievementsAsync(
        Guid userId,
        IReadOnlyList<Domain.Entities.Achievement> achievements,
        CancellationToken cancellationToken)
    {
        var userAchievements = await _userAchievementRepository
            .GetByUserIdAsync(userId, cancellationToken).ConfigureAwait(false);

        var userAchievementMap = userAchievements.ToDictionary(ua => ua.AchievementId);
        var unlockCount = 0;

        foreach (var achievement in achievements)
        {
            var progress = await _ruleEvaluator.EvaluateProgressAsync(
                achievement.Code,
                achievement.Threshold,
                userId,
                cancellationToken).ConfigureAwait(false);

            if (userAchievementMap.TryGetValue(achievement.Id, out var existing))
            {
                if (existing.IsUnlocked) continue; // Already unlocked

                var newlyUnlocked = existing.UpdateProgress(progress);
                await _userAchievementRepository.UpdateAsync(existing, cancellationToken).ConfigureAwait(false);

                if (newlyUnlocked)
                {
                    await SendAchievementNotificationAsync(userId, achievement, cancellationToken).ConfigureAwait(false);
                    unlockCount++;
                }
            }
            else
            {
                // First time tracking this achievement
                var ua = Domain.Entities.UserAchievement.Create(userId, achievement.Id);
                var newlyUnlocked = ua.UpdateProgress(progress);
                await _userAchievementRepository.AddAsync(ua, cancellationToken).ConfigureAwait(false);

                if (newlyUnlocked)
                {
                    await SendAchievementNotificationAsync(userId, achievement, cancellationToken).ConfigureAwait(false);
                    unlockCount++;
                }
            }
        }

        return unlockCount;
    }

    private async Task SendAchievementNotificationAsync(
        Guid userId,
        Domain.Entities.Achievement achievement,
        CancellationToken cancellationToken)
    {
        var notification = new Notification(
            id: Guid.NewGuid(),
            userId: userId,
            type: NotificationType.FromString("achievement_unlocked"),
            severity: NotificationSeverity.FromString("info"),
            title: $"Achievement Unlocked: {achievement.Name}!",
            message: $"You earned {achievement.Points} points! {achievement.Description}",
            link: "/achievements",
            metadata: System.Text.Json.JsonSerializer.Serialize(new
            {
                achievementId = achievement.Id,
                code = achievement.Code,
                points = achievement.Points,
                rarity = achievement.Rarity.ToString()
            }));

        await _notificationRepository.AddAsync(notification, cancellationToken).ConfigureAwait(false);

        _logger.LogInformation(
            "Achievement unlocked: {Code} for user {UserId} (+{Points} pts)",
            achievement.Code, userId, achievement.Points);
    }
}
