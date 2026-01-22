using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SystemConfiguration;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for UserRateLimitOverride aggregate.
/// Handles user-specific rate limit override persistence.
/// </summary>
internal sealed class UserRateLimitOverrideRepository : RepositoryBase, IUserRateLimitOverrideRepository
{
    public UserRateLimitOverrideRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<UserRateLimitOverride?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<UserRateLimitOverrideEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<UserRateLimitOverride?> GetByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var entity = await DbContext.Set<UserRateLimitOverrideEntity>()
            .AsNoTracking()
            .Where(e => e.UserId == userId &&
                       (e.ExpiresAt == null || e.ExpiresAt > now))
            .OrderByDescending(e => e.CreatedAt)
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<bool> HasActiveOverrideAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        return await DbContext.Set<UserRateLimitOverrideEntity>()
            .AnyAsync(e => e.UserId == userId &&
                          (e.ExpiresAt == null || e.ExpiresAt > now),
                     cancellationToken)
            .ConfigureAwait(false);
    }

    public async Task<IReadOnlyList<UserRateLimitOverride>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var now = DateTime.UtcNow;

        var entities = await DbContext.Set<UserRateLimitOverrideEntity>()
            .AsNoTracking()
            .Where(e => e.ExpiresAt == null || e.ExpiresAt > now)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<UserRateLimitOverride>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<UserRateLimitOverrideEntity>()
            .AsNoTracking()
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<List<UserRateLimitOverride>> GetAllByUserIdAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<UserRateLimitOverrideEntity>()
            .AsNoTracking()
            .Where(e => e.UserId == userId)
            .OrderByDescending(e => e.CreatedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(
        UserRateLimitOverride userOverride,
        CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(userOverride);
        var entity = MapToPersistence(userOverride);
        await DbContext.Set<UserRateLimitOverrideEntity>()
            .AddAsync(entity, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task UpdateAsync(UserRateLimitOverride userOverride, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(userOverride);
        var entity = MapToPersistence(userOverride);
        DbContext.Set<UserRateLimitOverrideEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(UserRateLimitOverride userOverride, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(userOverride);
        var entity = MapToPersistence(userOverride);
        DbContext.Set<UserRateLimitOverrideEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<UserRateLimitOverrideEntity>()
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    private static UserRateLimitOverride MapToDomain(UserRateLimitOverrideEntity entity)
    {
        var cooldown = entity.CooldownAfterRejectionSeconds.HasValue
            ? TimeSpan.FromSeconds(entity.CooldownAfterRejectionSeconds.Value)
            : (TimeSpan?)null;

        return new UserRateLimitOverride(
            entity.Id,
            entity.UserId,
            entity.MaxPendingRequests,
            entity.MaxRequestsPerMonth,
            cooldown,
            entity.ExpiresAt,
            entity.Reason,
            entity.CreatedByAdminId,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static UserRateLimitOverrideEntity MapToPersistence(UserRateLimitOverride domain)
    {
        return new UserRateLimitOverrideEntity
        {
            Id = domain.Id,
            UserId = domain.UserId,
            MaxPendingRequests = domain.MaxPendingRequests,
            MaxRequestsPerMonth = domain.MaxRequestsPerMonth,
            CooldownAfterRejectionSeconds = domain.CooldownAfterRejection.HasValue
                ? (long)domain.CooldownAfterRejection.Value.TotalSeconds
                : null,
            ExpiresAt = domain.ExpiresAt,
            Reason = domain.Reason,
            CreatedByAdminId = domain.CreatedByAdminId,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt
        };
    }
}
