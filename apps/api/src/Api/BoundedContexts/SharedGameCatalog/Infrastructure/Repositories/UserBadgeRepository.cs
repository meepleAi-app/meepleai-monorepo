using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for UserBadge entity.
/// ISSUE-2731: Infrastructure - EF Core Migrations e Repository
/// </summary>
internal sealed class UserBadgeRepository : IUserBadgeRepository
{
    private readonly MeepleAiDbContext _context;

    public UserBadgeRepository(MeepleAiDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task AddAsync(UserBadge userBadge, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(userBadge);
        var entity = MapToEntity(userBadge);
        await _context.Set<UserBadgeEntity>().AddAsync(entity, cancellationToken).ConfigureAwait(false);
    }

    public async Task<HashSet<Guid>> GetBadgeIdsByUserAsync(
        Guid userId,
        CancellationToken cancellationToken = default)
    {
        var badgeIds = await _context.Set<UserBadgeEntity>()
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
        var query = _context.Set<UserBadgeEntity>()
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

        var entities = await _context.Set<UserBadgeEntity>()
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
        var entity = await _context.Set<UserBadgeEntity>()
            .AsNoTracking()
            .Include(e => e.Badge)
            .FirstOrDefaultAsync(
                e => e.UserId == userId && e.BadgeId == badgeId,
                cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
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
