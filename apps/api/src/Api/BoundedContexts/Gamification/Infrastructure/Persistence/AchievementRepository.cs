using Api.BoundedContexts.Gamification.Domain.Entities;
using Api.BoundedContexts.Gamification.Domain.Enums;
using Api.BoundedContexts.Gamification.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.Gamification;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.Gamification.Infrastructure.Persistence;

/// <summary>
/// EF Core implementation of Achievement repository.
/// Issue #3922: Achievement System and Badge Engine.
/// </summary>
internal class AchievementRepository : RepositoryBase, IAchievementRepository
{
    public AchievementRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<Achievement?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<AchievementEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<Achievement>> GetAllAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<AchievementEntity>()
            .AsNoTracking()
            .OrderBy(a => a.Category).ThenBy(a => a.Points)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<Achievement>> GetActiveAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<AchievementEntity>()
            .AsNoTracking()
            .Where(a => a.IsActive)
            .OrderBy(a => a.Category).ThenBy(a => a.Points)
            .ToListAsync(cancellationToken).ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<Achievement?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<AchievementEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(a => a.Code == code, cancellationToken).ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task AddAsync(Achievement achievement, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(achievement);
        var entity = MapToPersistence(achievement);
        await DbContext.Set<AchievementEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(Achievement achievement, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(achievement);
        var entity = MapToPersistence(achievement);
        DbContext.Set<AchievementEntity>().Update(entity);
        await Task.CompletedTask.ConfigureAwait(false);
    }

    public async Task DeleteAsync(Achievement achievement, CancellationToken cancellationToken = default)
    {
        await DbContext.Set<AchievementEntity>()
            .Where(a => a.Id == achievement.Id)
            .ExecuteDeleteAsync(cancellationToken).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<AchievementEntity>()
            .AsNoTracking()
            .AnyAsync(a => a.Id == id, cancellationToken).ConfigureAwait(false);
    }

    private static AchievementEntity MapToPersistence(Achievement a)
    {
        return new AchievementEntity
        {
            Id = a.Id,
            Code = a.Code,
            Name = a.Name,
            Description = a.Description,
            IconUrl = a.IconUrl,
            Points = a.Points,
            Rarity = (int)a.Rarity,
            Category = (int)a.Category,
            Threshold = a.Threshold,
            IsActive = a.IsActive,
            CreatedAt = a.CreatedAt
        };
    }

    private static Achievement MapToDomain(AchievementEntity e)
    {
        return Achievement.Reconstitute(
            e.Id,
            e.Code,
            e.Name,
            e.Description,
            e.IconUrl,
            e.Points,
            (AchievementRarity)e.Rarity,
            (AchievementCategory)e.Category,
            e.Threshold,
            e.IsActive,
            e.CreatedAt);
    }
}
