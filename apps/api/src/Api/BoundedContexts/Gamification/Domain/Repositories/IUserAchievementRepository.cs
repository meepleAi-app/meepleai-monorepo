using Api.BoundedContexts.Gamification.Domain.Entities;
using Api.SharedKernel.Infrastructure.Persistence;

namespace Api.BoundedContexts.Gamification.Domain.Repositories;

/// <summary>
/// Repository interface for UserAchievement aggregate.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal interface IUserAchievementRepository : IRepository<UserAchievement, Guid>
{
    /// <summary>
    /// Gets all achievements for a user (including progress and unlock status).
    /// </summary>
    Task<IReadOnlyList<UserAchievement>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets the most recently unlocked achievements for a user.
    /// </summary>
    Task<IReadOnlyList<UserAchievement>> GetRecentUnlockedAsync(
        Guid userId,
        int limit = 5,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets a specific user achievement by user and achievement IDs.
    /// </summary>
    Task<UserAchievement?> GetByUserAndAchievementAsync(
        Guid userId,
        Guid achievementId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Gets all user IDs that have active achievements being tracked.
    /// Used by the evaluation job.
    /// </summary>
    Task<IReadOnlyList<Guid>> GetDistinctUserIdsAsync(CancellationToken cancellationToken = default);
}
