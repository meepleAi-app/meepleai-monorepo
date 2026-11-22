using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.SharedKernel.Infrastructure;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.SharedGameCatalog.Infrastructure.Repositories;

/// <summary>
/// Repository implementation for Badge entity.
/// ISSUE-2731: Infrastructure - EF Core Migrations e Repository
/// </summary>
internal sealed class BadgeRepository : RepositoryBase, IBadgeRepository
{

    public BadgeRepository(MeepleAiDbContext dbContext, IDomainEventCollector eventCollector)
        : base(dbContext, eventCollector)
    {
    }

    public async Task<List<Badge>> GetAllActiveAsync(CancellationToken cancellationToken = default)
    {
        var entities = await DbContext.Set<BadgeEntity>()
            .AsNoTracking()
            .Where(e => e.IsActive)
            .OrderBy(e => e.DisplayOrder)
            .ThenBy(e => e.Tier)
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return entities.Select(MapToDomain).ToList();
    }

    public async Task<Badge?> GetByCodeAsync(string code, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(code))
            return null;

        var normalizedCode = code.ToUpperInvariant();

        var entity = await DbContext.Set<BadgeEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Code == normalizedCode, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    public async Task<Badge?> GetByIdAsync(Guid id, CancellationToken cancellationToken = default)
    {
        var entity = await DbContext.Set<BadgeEntity>()
            .AsNoTracking()
            .FirstOrDefaultAsync(e => e.Id == id, cancellationToken)
            .ConfigureAwait(false);

        return entity is null ? null : MapToDomain(entity);
    }

    #region Mapping Methods

    private static Badge MapToDomain(BadgeEntity entity)
    {
        var badgeRequirement = BadgeRequirementJson.Deserialize(entity.RequirementJson, entity.Id);

        return new Badge(
            entity.Id,
            entity.Code,
            entity.Name,
            entity.Description,
            entity.IconUrl,
            (BadgeTier)entity.Tier,
            (BadgeCategory)entity.Category,
            entity.IsActive,
            entity.DisplayOrder,
            badgeRequirement,
            entity.CreatedAt,
            entity.ModifiedAt);
    }

    #endregion
}
