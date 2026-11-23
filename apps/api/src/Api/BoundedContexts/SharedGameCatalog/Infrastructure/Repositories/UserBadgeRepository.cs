using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for UserBadge entity.
/// ISSUE-2731: Infrastructure - EF Core Migrations e Repository
/// </summary>
internal sealed class UserBadgeRepository : RepositoryBase, IUserBadgeRepository
{

    public UserBadgeRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task AddAsync(UserBadge userBadge, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userBadge);
        // #5110: BadgeEarnedEvent reaches its handlers only through the collector, which SaveChangesAsync reads.
        CollectDomainEvents(userBadge);
        var entity = MapToEntity(userBadge);
        await DbContext.Set<UserBadgeEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task UpdateAsync(UserBadge userBadge, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userBadge);

        // #5116: the reads are AsNoTracking and map to the domain, so a revocation reaches the row only here. The
        // row is loaded tracked and gets the fields a revocation or a reinstatement changes; the others keep theirs.
        // AsTracking, not FindAsync: the context defaults to NoTracking, and FindAsync would hand back a row no
        // SaveChanges writes (measured: the first draft of this method saved nothing). The local entries come
        // first: a badge added in this unit of work is not in the database yet.
        var entity = DbContext.Set<UserBadgeEntity>().Local.FirstOrDefault(e => e.Id == userBadge.Id)
            ?? await DbContext.Set<UserBadgeEntity>()
                .AsTracking()
                .FirstOrDefaultAsync(e => e.Id == userBadge.Id, cancellationToken)
                .ConfigureAwait(false)
            ?? throw new NotFoundException("UserBadge", userBadge.Id.ToString());

        entity.IsDisplayed = userBadge.IsDisplayed;
        entity.RevokedAt = userBadge.RevokedAt;
        entity.RevocationReason = userBadge.RevocationReason;

        CollectDomainEvents(userBadge);
    }

    public async Task<HashSet<Guid>> GetBadgeIdsByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var badgeIds = await DbContext.Set<UserBadgeEntity>()
            .AsNoTracking()
            .Where(e => e.UserId == userId && e.RevokedAt == null)
            .Select(e => e.BadgeId)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return badgeIds.ToHashSet();
    }

    public async Task<List<UserBadge>> GetByUserIdAsync(
        Guid userId,
        bool includeHidden = false,
        CancellationToken cancellationToken = default)
    {
        var query = DbContext.Set<UserBadgeEntity>()
            .AsNoTracking()
            .Include(e => e.Badge)
            .Where(e => e.UserId == userId && e.RevokedAt == null);

        if (!includeHidden)
            query = query.Where(e => e.IsDisplayed);

        var entities = await query
            .OrderByDescending(e => e.EarnedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<List<UserBadge>> GetUsersByBadgeCodeAsync(
        string badgeCode,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(badgeCode))
            return new List<UserBadge>();

        var normalizedCode = badgeCode.ToUpperInvariant();

        var entities = await DbContext.Set<UserBadgeEntity>()
            .AsNoTracking()
            .Include(e => e.Badge)
            .Where(e => e.Badge != null && e.Badge.Code == normalizedCode && e.RevokedAt == null)
            .OrderByDescending(e => e.EarnedAt)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<UserBadge?> GetByUserAndBadgeAsync(
        Guid userId,
        Guid badgeId,
        CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<UserBadgeEntity>()
            .AsNoTracking()
            .Include(e => e.Badge)
            .FirstOrDefaultAsync(
                e => e.UserId == userId && e.BadgeId == badgeId,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<List<UserBadge>> GetTopBadgesByUserAsync(
        Guid userId,
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
            return new List<UserBadge>();

        var entities = await DbContext.Set<UserBadgeEntity>()
            .AsNoTracking()
            .Include(e => e.Badge)
            .Where(e => e.UserId == userId && e.RevokedAt == null && e.IsDisplayed)
            .OrderByDescending(e => e.Badge!.Tier)
            .ThenByDescending(e => e.EarnedAt)
            .Take(count)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<List<Guid>> GetAllDistinctUserIdsAsync(CancellationToken cancellationToken = default)
    {
        // Revoked rows included (#5116): a user whose only badge was revoked must still be evaluated, or the full
        // recalculation would never give it back.
        return await DbContext.Set<UserBadgeEntity>()
            .AsNoTracking()
            .Select(e => e.UserId)
            .Distinct()
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);
    }

    #region Mapping Methods

    private static UserBadge MapToDomain(UserBadgeEntity entity)
    {
        return new UserBadge(
            entity.Id,
            entity.UserId,
            entity.BadgeId,
            entity.EarnedAt,
            entity.TriggeringShareRequestId,
            entity.IsDisplayed,
            entity.RevokedAt,
            entity.RevocationReason);
    }

    private static UserBadgeEntity MapToEntity(UserBadge userBadge)
    {
        return new UserBadgeEntity
        {
            Id = userBadge.Id,
            UserId = userBadge.UserId,
            BadgeId = userBadge.BadgeId,
            EarnedAt = userBadge.EarnedAt,
            TriggeringShareRequestId = userBadge.TriggeringShareRequestId,
            IsDisplayed = userBadge.IsDisplayed,
            RevokedAt = userBadge.RevokedAt,
            RevocationReason = userBadge.RevocationReason
        };
    }

    #endregion
}
