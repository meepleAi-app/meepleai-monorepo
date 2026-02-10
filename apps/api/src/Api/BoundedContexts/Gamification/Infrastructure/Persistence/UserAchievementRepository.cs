using Api.BoundedContexts.Gamification.Domain.Entities;
using Api.BoundedContexts.Gamification.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Gamification;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Gamification.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of UserAchievement repository.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal class UserAchievementRepository : RepositoryBase, IUserAchievementRepository
{
    public UserAchievementRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<UserAchievement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<UserAchievementEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(ua => ua.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<UserAchievement>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<UserAchievementEntity>()
            .AsNoTracking()
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<UserAchievement>> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<UserAchievementEntity>()
            .AsNoTracking()
            .Where(ua => ua.UserId == userId)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<UserAchievement>> GetRecentUnlockedAsync(
        Guid userId,
        int limit = 5,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<UserAchievementEntity>()
            .AsNoTracking()
            .Where(ua => ua.UserId == userId && ua.UnlockedAt != null)
            .OrderByDescending(ua => ua.UnlockedAt)
            .Take(limit)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<UserAchievement?> GetByUserAndAchievementAsync(
        Guid userId,
        Guid achievementId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<UserAchievementEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(
                ua => ua.UserId == userId && ua.AchievementId == achievementId,
                cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<Guid>> GetDistinctUserIdsAsync(CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<UserAchievementEntity>()
            .AsNoTracking()
            .Select(ua => ua.UserId)
            .Distinct()
            .ToListAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task AddAsync(UserAchievement userAchievement, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(userAchievement);
        var entity = MapToPersistence(userAchievement);
        await DbContext.Set<UserAchievementEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(UserAchievement userAchievement, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(userAchievement);
        var entity = MapToPersistence(userAchievement);
        DbContext.Set<UserAchievementEntity>().Update(entity);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task DeleteAsync(UserAchievement userAchievement, CancellationToken cancellationToken = default)
    {
        await DbContext.Set<UserAchievementEntity>()
            .Where(ua => ua.Id == userAchievement.Id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<UserAchievementEntity>()
            .AsNoTracking()
            .AnyAsync(ua => ua.Id == id, cancellationToken).ConfigureAwait(false);
    }

    private static UserAchievementEntity MapToPersistence(UserAchievement ua)
    {
        return new UserAchievementEntity
        {
            Id = ua.Id,
            UserId = ua.UserId,
            AchievementId = ua.AchievementId,
            Progress = ua.Progress,
            UnlockedAt = ua.UnlockedAt,
            CreatedAt = ua.CreatedAt,
            UpdatedAt = ua.UpdatedAt
        };
    }

    private static UserAchievement MapToDomain(UserAchievementEntity entity)
    {
        return UserAchievement.Reconstitute(
            entity.Id,
            entity.UserId,
            entity.AchievementId,
            entity.Progress,
            entity.UnlockedAt,
            entity.CreatedAt,
            entity.UpdatedAt);
    }
}
