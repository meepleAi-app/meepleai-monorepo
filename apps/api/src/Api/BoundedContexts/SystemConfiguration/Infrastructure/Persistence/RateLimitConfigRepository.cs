using Api.BoundedContexts.SystemConfiguration.Domain.Entities;
using Api.BoundedContexts.SystemConfiguration.Domain.Repositories;
using Api.BoundedContexts.SystemConfiguration.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SystemConfiguration;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SystemConfiguration.Infrastructure.Persistence;

/// <summary>
/// Repository implementation for ShareRequestLimitConfig aggregate.
/// Handles tier-based rate limit configuration persistence.
/// </summary>
internal sealed class RateLimitConfigRepository : RepositoryBase, IRateLimitConfigRepository
{
    public RateLimitConfigRepository(
        MeepleAiDbContext dbContext,
        IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<ShareRequestLimitConfig?> GetByIdAsync(
        Guid id,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<ShareRequestLimitConfigEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<ShareRequestLimitConfig?> GetByTierAsync(
        UserTier tier,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<ShareRequestLimitConfigEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Tier == (int)tier && e.IsActive, cancellationToken)
            .ConfigureAwait(false);

        return entity != null ? MapToDomain(entity) : null;
    }

    public async Task<IReadOnlyList<ShareRequestLimitConfig>> GetAllActiveAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<ShareRequestLimitConfigEntity>()
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.Tier)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<IReadOnlyList<ShareRequestLimitConfig>> GetAllAsync(
        CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<ShareRequestLimitConfigEntity>()
            .AsNoTracking()
            .OrderBy(e => e.Tier)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task AddAsync(
        ShareRequestLimitConfig config,
        CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(config);
        var entity = MapToPersistence(config);
        await DbContext.Set<ShareRequestLimitConfigEntity>()
            .AddAsync(entity, cancellationToken)
            .ConfigureAwait(false);
    }

    public Task UpdateAsync(ShareRequestLimitConfig config, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(config);
        var entity = MapToPersistence(config);
        DbContext.Set<ShareRequestLimitConfigEntity>().Update(entity);
        return Task.CompletedTask;
    }

    public Task DeleteAsync(ShareRequestLimitConfig config, CancellationToken cancellationToken = default)
    {
        CollectDomainEvents(config);
        var entity = MapToPersistence(config);
        DbContext.Set<ShareRequestLimitConfigEntity>().Remove(entity);
        return Task.CompletedTask;
    }

    public async Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken = default)
    {
        return await DbContext.Set<ShareRequestLimitConfigEntity>()
            .AnyAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);
    }

    private static ShareRequestLimitConfig MapToDomain(ShareRequestLimitConfigEntity entity)
    {
        var cooldown = TimeSpan.FromSeconds(entity.CooldownAfterRejectionSeconds);

        return new ShareRequestLimitConfig(
            entity.Id,
            (UserTier)entity.Tier,
            entity.MaxPendingRequests,
            entity.MaxRequestsPerMonth,
            cooldown,
            entity.IsActive,
            entity.CreatedAt,
            entity.UpdatedAt);
    }

    private static ShareRequestLimitConfigEntity MapToPersistence(ShareRequestLimitConfig domain)
    {
        return new ShareRequestLimitConfigEntity
        {
            Id = domain.Id,
            Tier = (int)domain.Tier,
            MaxPendingRequests = domain.MaxPendingRequests,
            MaxRequestsPerMonth = domain.MaxRequestsPerMonth,
            CooldownAfterRejectionSeconds = (long)domain.CooldownAfterRejection.TotalSeconds,
            IsActive = domain.IsActive,
            CreatedAt = domain.CreatedAt,
            UpdatedAt = domain.UpdatedAt
        };
    }
}
